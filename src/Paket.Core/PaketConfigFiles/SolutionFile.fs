namespace Paket

open System.IO
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
