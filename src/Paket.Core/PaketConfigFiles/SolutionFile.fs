namespace Paket

open System.IO
open System.Text
open System.Xml
open System.Xml.Linq
open Paket.Xml.Linq
open Logging
open System

/// Tells the solution items that are files convert-from-nuget deletes
module internal SolutionItems =

    // the file system of Linux tells NuGet.Config from nuget.config, the ones of Windows and macOS do not
    let private pathComparison = if isLinux then StringComparison.Ordinal else StringComparison.OrdinalIgnoreCase

    /// Whether the path of an item, relative to the solution directory, is one of the given full paths
    let isOneOf (solutionDirectory: string) (files: string list) (path: string) =
        let path = path.Replace('\\', '/')
        let name = path.Substring(path.LastIndexOf '/' + 1)
        // only the paths named like one of the files are resolved, the others cannot be one
        files |> List.exists (fun file -> String.equalsIgnoreCase (Path.GetFileName file) name)
        &&
        try
            // a rooted path is never resolved: on Windows, resolving one such as //server/share/a~1 could reach that server
            not (Path.IsPathRooted path)
            && (let fullPath = Path.GetFullPath(Path.Combine(solutionDirectory, path))
                files |> List.exists (fun file -> String.Equals(file, fullPath, pathComparison)))
        with
        // on .NET Framework, a path with characters such as | is invalid
        | :? ArgumentException | :? NotSupportedException | :? PathTooLongException -> false

/// A solution file that convert-from-nuget can update, whatever its format.
type ISolutionFile =
    abstract FileName : string
    /// Removes the entries of the given NuGet files, the full paths of the ones convert-from-nuget deletes,
    /// matching them by the path they resolve to: in any solution folder of a .slnx, in any SolutionItems section of a .sln.
    abstract RemoveNuGetEntries : nugetFiles: string list -> unit
    abstract AddPaketFolder : dependenciesFile: string * lockFile: string option -> unit
    abstract Save : unit -> unit

/// Contains methods to read and manipulate solution files.
type SolutionFile(fileName: string) =
   
    let originalContent = File.ReadAllLines fileName |> Array.toList
    let originalEncoding = getFileEncoding fileName
    let content = ResizeArray( originalContent )
    
    let removeNugetSlnFolderIfEmpty() =
        match content |> Seq.tryFindIndex (fun line -> 
                line.StartsWith(sprintf "Project(\"{%s}\")" Constants.SolutionFolderProjectGuid) && line.Contains(".nuget")) with
        | Some(index) -> 
            if content.[index+1].Contains("ProjectSection(SolutionItems)") &&
               content.[index+2].Contains("EndProjectSection") &&
               content.[index+3].Contains("EndProject")
            then 
                content.RemoveRange(index, 4)
        | None -> ()

    let projectSectionLines = 
        [ "\tProjectSection(SolutionItems) = preProject";
          "\tEndProjectSection" ]

    let addPaketFolder () = 
        let lines = 
              [sprintf "Project(\"{%s}\") = \".paket\", \".paket\", \"{%s}\"" Constants.SolutionFolderProjectGuid (Guid.NewGuid().ToString("D").ToUpper())]
            @ projectSectionLines
            @ [ "EndProject" ]

        let index = 
            match content |> Seq.tryFindIndex (fun line -> line.StartsWith("Project")) with
            // insert before the first project in a solution
            | Some index -> index
            | None -> 
                // there are no project in solution
                match content |> Seq.tryFindIndex (fun line -> line.StartsWith("Global")) with
                // insert before ``global`` entry
                | Some index -> index
                // cannot find ``global`` entry, just append to the end of file
                | None -> content.Count

        content.InsertRange(index, lines)
        index, lines |> List.length

    let addPaketFiles(paketProjectIndex, length, dependenciesFile, lockFile) = 
        let projectLines = content.GetRange(paketProjectIndex, length)

        // This here is ad-hoc hacky solution to sln ambiguity.
        // The problen is that the .sln file may or may not contain a "ProjectSection" within the ".paket" "Project" (see #2161 and #2512).
        // When the "ProjectSection" is not there, we need to add it, and then add new files within it.
        // A better solution would be to have a proper parser for the .sln file, but that would take too long, and would probably be a waste anyway.
        let firstLineInPaketProject = if paketProjectIndex < content.Count-1 then content.[paketProjectIndex+1] else ""
        if not (firstLineInPaketProject.Contains "ProjectSection") then
            if firstLineInPaketProject.Contains "EndProject" then 
                // The "Project" is empty => insert "ProjectSection"
                content.InsertRange( paketProjectIndex+1, projectSectionLines )
            else 
                // The "Project" is not empty, but also doesn't contain a "ProjectSection" => no idea what this means, 
                // so just fall back to old behavior and insert new lines at +2 after "Project" start.
                ()
        
        let add s =
            if not (Seq.exists (fun line -> line = s) projectLines) then 
                content.Insert(paketProjectIndex + 2, s)

        Option.iter (fun lockFile -> 
            add (sprintf"		%s = %s" lockFile lockFile)) lockFile

        add (sprintf   "		%s = %s" dependenciesFile dependenciesFile)
        

    member __.FileName = fileName

    member __.RemoveNuGetEntries(nugetFiles: string list) =
        let solutionDirectory = Path.GetDirectoryName(Path.GetFullPath fileName)
        // the items of a solution folder are the "path = path" lines of its ProjectSection(SolutionItems)
        let nugetItems = ResizeArray()
        let mutable inSolutionItems = false
        for index in 0 .. content.Count - 1 do
            let line = content.[index].Trim()
            if line.StartsWith "ProjectSection(SolutionItems)" then inSolutionItems <- true
            elif line.StartsWith "EndProjectSection" then inSolutionItems <- false
            elif inSolutionItems then
                match line.Split([| '=' |]) with
                | [| item; _ |] when SolutionItems.isOneOf solutionDirectory nugetFiles (item.Trim()) -> nugetItems.Add index
                | _ -> ()

        for index in Seq.rev nugetItems do
            content.RemoveAt index

        removeNugetSlnFolderIfEmpty()

    member __.AddPaketFolder(dependenciesFile, lockFile) =
        let paketProjectIndex, length = 
            match content |> Seq.tryFindIndex (fun line ->
                line.StartsWith(sprintf   "Project(\"{%s}\") = \".paket\", \".paket\"" Constants.SolutionFolderProjectGuid)) with
            | Some paketProjectIndex -> 
                let length = content |> Seq.skip paketProjectIndex |> Seq.findIndex (fun line -> line = "EndProject")
                paketProjectIndex, length
            | None -> addPaketFolder()       
        
        addPaketFiles(paketProjectIndex, length, dependenciesFile, lockFile)

    member __.Save() =
        if content |> Seq.toList <> originalContent 
        then 
            File.WriteAllLines(fileName, content, originalEncoding)
            tracefn "Solution %s changed" fileName

    interface ISolutionFile with
        member this.FileName = this.FileName
        member this.RemoveNuGetEntries nugetFiles = this.RemoveNuGetEntries nugetFiles
        member this.AddPaketFolder(dependenciesFile, lockFile) = this.AddPaketFolder(dependenciesFile, lockFile)
        member this.Save() = this.Save()

/// Contains methods to read and manipulate .slnx solution files.
type SlnxSolutionFile(fileName: string) =

    let failParsing reason =
        failwith $"Parsing of %s{fileName} failed.%s{Environment.NewLine}%s{reason}"

    // A .slnx never has a DTD: prohibiting it means no entity is ever expanded or resolved.
    // The reader decodes the bytes from their BOM or from the encoding of the XML declaration.
    let bytes, (doc, readerEncoding) =
        // XmlTextReader, unlike XmlReader.Create, tells the encoding it decoded the bytes with, once it has
        // read the first node; Normalization = true gives it the line break normalization and character checks of XmlReader.Create.
        let parse dtdProcessing bytes =
            use reader = new XmlTextReader(new MemoryStream(bytes: byte []), DtdProcessing = dtdProcessing, XmlResolver = null, Normalization = true)
            reader.Read() |> ignore
            let encoding = reader.Encoding
            XDocument.Load(reader, LoadOptions.PreserveWhitespace), encoding
        let bytes = try File.ReadAllBytes fileName with exn -> failParsing exn.Message
        try
            bytes, parse DtdProcessing.Prohibit bytes
        with
        | :? XmlException as exn ->
            // Parsing again while skipping the DTD, which is just as safe, tells whether the DTD is the cause.
            let causedByDtd =
                try
                    parse DtdProcessing.Ignore bytes |> ignore
                    true
                with
                | :? XmlException as withoutDtd -> withoutDtd.Message <> exn.Message
            if causedByDtd then failParsing "A .slnx solution file cannot contain a DOCTYPE." else failParsing exn.Message
        | exn -> failParsing exn.Message

    do if doc.Root.Name <> XName.Get "Solution" then failParsing "Its root element is not <Solution>."

    // The file is saved in the encoding the reader decoded it with, with a BOM only if it had one.
    // The Unicode encodings are told apart by their preamble, as the reader may decode them with
    // internal encodings, such as the UCS-4 one of an undeclared UTF-32 file, whose code page is 0.
    // The second encoding is the same without a BOM, for the XmlWriter, as Save writes the BOM itself.
    let encoding, encodingWithoutBom =
        let unicodeEncodings : (Encoding * Encoding) list =
            // UTF-32 LE first: its BOM starts with the one of UTF-16 LE
            [ UTF32Encoding(false, true), UTF32Encoding(false, false)
              UTF32Encoding(true, true), UTF32Encoding(true, false)
              UTF8Encoding true, UTF8Encoding false
              UnicodeEncoding(false, true), UnicodeEncoding(false, false)
              UnicodeEncoding(true, true), UnicodeEncoding(true, false) ]
        let startsWithBom (withBom: Encoding) =
            let preamble = withBom.GetPreamble()
            bytes.Length >= preamble.Length && Array.sub bytes 0 preamble.Length = preamble
        let isReaderEncoding (withBom: Encoding) = withBom.GetPreamble() = readerEncoding.GetPreamble()
        match unicodeEncodings |> List.tryFind (fst >> startsWithBom) with
        | Some (withBom, withoutBom) when isReaderEncoding withBom -> withBom, withoutBom
        | Some _ -> failParsing "Its BOM and its XML declaration give different encodings."
        | None ->
            match unicodeEncodings |> List.tryFind (fst >> isReaderEncoding) with
            | Some (_, withoutBom) -> withoutBom, withoutBom
            // a single-byte encoding, such as ISO-8859-1, which has no BOM
            | None when readerEncoding.CodePage <> 0 -> readerEncoding, readerEncoding
            // an internal UCS-4 encoding of the reader, for the 2143 and 3412 byte orders, which has no encoder
            | None -> failParsing "Its encoding, UCS-4 in an unusual byte order, cannot be written back."

    // The reader replaces the bytes its encoding cannot decode, such as the UTF-8 bytes of a file declared
    // as us-ascii, and the file would be saved with the replacements: decoding without them tells such a file.
    let text =
        let strictEncoding = encoding.Clone() :?> Encoding
        strictEncoding.DecoderFallback <- DecoderFallback.ExceptionFallback
        try strictEncoding.GetString bytes
        with :? DecoderFallbackException -> failParsing $"Its content is not valid %s{encoding.WebName}."

    // The declaration is written back as it is, as the bytes keep its encoding: the XmlWriter would write
    // its own, such as encoding="utf-8" for encoding='UTF-8', or add an encoding the file did not declare.
    let declaration =
        if isNull doc.Declaration then ""
        else
            let text = text.TrimStart '﻿'
            text.Substring(0, text.IndexOf "?>" + 2)

    // The line breaks of the new elements are the ones most of the file has
    let newLine =
        let crlfCount = text.Split([| "\r\n" |], StringSplitOptions.None).Length - 1
        let lfCount = text.Split('\n').Length - 1 - crlfCount
        if crlfCount > lfCount then "\r\n" else "\n"

    let mutable changed = false

    // createRelativePath returns backslashes, while Visual Studio and `dotnet sln` write forward slashes;
    // a leading "./", as in a hand-written entry, does not change the file.
    let canonicalPath (path: string) =
        let path = path.Replace('\\', '/')
        if path.StartsWith "./" then path.Substring 2 else path

    let pathOf file = tryGetAttribute "Path" file |> Option.map (fun path -> canonicalPath path.Value)

    let tryFindFolder name =
        getElements None "Folder" doc.Root
        |> Seq.tryFind (fun folder -> tryGetAttribute "Name" folder |> Option.exists (fun n -> String.equalsIgnoreCase n.Value name))

    // The whitespace is kept as it is in the file: new elements get the indentation of their siblings,
    // and the XmlWriter turns the "\n" of the new text nodes into the line breaks of the file.
    let isWhitespace (node: XNode) =
        match node with
        | :? XText as t -> String.IsNullOrWhiteSpace t.Value
        | _ -> false

    let indentOf (node: XNode) =
        match node.PreviousNode with
        | :? XText as t when isWhitespace t -> t.Value.Substring(t.Value.LastIndexOf '\n' + 1)
        | _ -> ""

    let firstNonWhitespaceNode (parent: XElement) = parent.Nodes() |> Seq.tryFind (isWhitespace >> not)

    let indentUnit =
        match firstNonWhitespaceNode doc.Root |> Option.map indentOf with
        | Some indent when indent <> "" -> indent
        | _ -> "  "

    let appendChild (parent: XElement) (child: XElement) =
        match parent.Elements() |> Seq.tryLast with
        | Some last -> last.AddAfterSelf(XText("\n" + indentOf last), child)
        | None ->
            let indent = indentOf parent
            parent.Nodes() |> Seq.filter isWhitespace |> Seq.toList |> List.iter (fun node -> node.Remove())
            parent.Add(XText("\n" + indent + indentUnit), child, XText("\n" + indent))
        changed <- true

    let removeElement (element: XElement) =
        if isWhitespace element.PreviousNode then
            element.PreviousNode.Remove()
        element.Remove()
        changed <- true

    member __.FileName = fileName

    member __.RemoveNuGetEntries(nugetFiles: string list) =
        let solutionDirectory = Path.GetDirectoryName(Path.GetFullPath fileName)
        let nugetFileEntries =
            doc.Descendants(XName.Get "File")
            |> Seq.filter (fun file -> pathOf file |> Option.exists (SolutionItems.isOneOf solutionDirectory nugetFiles))
            |> Seq.toList

        for file in nugetFileEntries do
            let folder = file.Parent
            removeElement file
            if folder.Name = XName.Get "Folder" && Seq.isEmpty (folder.Elements()) then
                removeElement folder

    member __.AddPaketFolder(dependenciesFile: string, lockFile: string option) =
        let folder =
            match tryFindFolder "/.paket/" with
            | Some folder -> folder
            | None ->
                let folder = createElement None "Folder" [ "Name", "/.paket/" ]
                // `dotnet sln` writes the folders after the configurations
                match getElements None "Configurations" doc.Root |> Seq.tryLast, firstNonWhitespaceNode doc.Root with
                | Some configurations, _ -> configurations.AddAfterSelf(XText("\n" + indentOf configurations), folder)
                | None, Some first -> first.AddBeforeSelf(folder, XText("\n" + indentOf first))
                | None, None -> appendChild doc.Root folder
                changed <- true
                folder

        let add path =
            let path = canonicalPath path
            if not (getElements None "File" folder |> Seq.exists (fun file -> pathOf file |> Option.exists (String.equalsIgnoreCase path))) then
                appendChild folder (createElement None "File" [ "Path", path ])

        add dependenciesFile
        Option.iter add lockFile

    member __.Save() =
        if changed then
            // Given the stream, the XmlWriter encodes the text itself, and writes the characters
            // the encoding lacks, such as € in ISO-8859-1, as character references. Like Visual Studio,
            // it writes the attributes in double quotes and an empty element as <X />.
            let settings =
                XmlWriterSettings(Encoding = encodingWithoutBom, OmitXmlDeclaration = true, NewLineChars = newLine, NewLineHandling = NewLineHandling.Replace)
            // the file is only overwritten once the whole document is written, so a failing writer leaves it as it was
            use stream = new MemoryStream()
            let start = Array.append (encoding.GetPreamble()) (encoding.GetBytes declaration)
            stream.Write(start, 0, start.Length)
            do
                use writer = XmlWriter.Create(stream, settings)
                doc.Save writer
            File.WriteAllBytes(fileName, stream.ToArray())
            tracefn "Solution %s changed" fileName

    interface ISolutionFile with
        member this.FileName = this.FileName
        member this.RemoveNuGetEntries nugetFiles = this.RemoveNuGetEntries nugetFiles
        member this.AddPaketFolder(dependenciesFile, lockFile) = this.AddPaketFolder(dependenciesFile, lockFile)
        member this.Save() = this.Save()
