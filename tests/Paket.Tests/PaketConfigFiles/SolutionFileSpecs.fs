module Paket.SolutionFileSpecs

open System
open System.IO
open System.Text
open System.Xml.Linq
open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers

let private withSlnxBytes (bytes: byte []) (test: string -> unit) =
    withTempDir (fun root ->
        let fileName = Path.Combine(root, "App.slnx")
        File.WriteAllBytes(fileName, bytes)
        test fileName)

let private withSlnx (content: string) test = withSlnxBytes (UTF8Encoding(false).GetBytes content) test

let private folderFiles name (fileName: string) =
    XDocument.Load(fileName).Root.Elements(XName.Get "Folder")
    |> Seq.filter (fun f -> f.Attribute(XName.Get "Name").Value = name)
    |> Seq.map (fun f -> f.Elements(XName.Get "File") |> Seq.map (fun file -> file.Attribute(XName.Get "Path").Value) |> Seq.toList)
    |> Seq.toList

/// The files of the .nuget folder of the root directory that convert-from-nuget deletes
let private nugetFilesOf root =
    [ "NuGet.Config"; "NuGet.exe"; "NuGet.targets" ] |> List.map (fun name -> Path.Combine(root, ".nuget", name))

/// Removes the NuGet entries of the .slnx, makes the given change and saves it
let private update (fileName: string) (change: SlnxSolutionFile -> unit) =
    let solution = SlnxSolutionFile fileName
    solution.RemoveNuGetEntries(nugetFilesOf (Path.GetDirectoryName fileName))
    change solution
    solution.Save()

let private removeNuGetEntries (fileName: string) = update fileName ignore

let private convert (fileName: string) = update fileName (fun solution -> solution.AddPaketFolder("paket.dependencies", None))

/// A <Solution /> after convert
let private expectedAfterConvert = "<Solution>\n  <Folder Name=\"/.paket/\">\n    <File Path=\"paket.dependencies\" />\n  </Folder>\n</Solution>"

// The .paket folder

[<Test>]
let ``should add a .paket folder to a slnx without one``() =
    let content = """<Solution>
  <Project Path="src/App/App.csproj" />
</Solution>
"""
    withSlnx content (fun fileName ->
        convert fileName

        File.ReadAllText fileName
        |> shouldEqual """<Solution>
  <Folder Name="/.paket/">
    <File Path="paket.dependencies" />
  </Folder>
  <Project Path="src/App/App.csproj" />
</Solution>
""")

[<Test>]
let ``should add the .paket folder after the configurations of a slnx and keep the rest unchanged``() =
    let content = """<Solution>
  <Configurations>
    <Platform Name="Any CPU" />
    <Platform Name="x64" />
  </Configurations>
  <Folder Name="/.nuget/">
    <File Path=".nuget/NuGet.Config" />
    <File Path=".nuget/NuGet.exe" />
  </Folder>
  <Folder Name="/src/" />
  <Project Path="proj.fsproj">
    <Platform Solution="*|x64" Project="x64" />
  </Project>
</Solution>
"""
    withSlnx content (fun fileName ->
        convert fileName

        File.ReadAllText fileName
        |> shouldEqual """<Solution>
  <Configurations>
    <Platform Name="Any CPU" />
    <Platform Name="x64" />
  </Configurations>
  <Folder Name="/.paket/">
    <File Path="paket.dependencies" />
  </Folder>
  <Folder Name="/src/" />
  <Project Path="proj.fsproj">
    <Platform Solution="*|x64" Project="x64" />
  </Project>
</Solution>
""")

[<Test>]
let ``should complete an existing .paket folder of a slnx without duplicating its files``() =
    let content = """<Solution>
  <Folder Name="/.Paket/">
    <File Path="SomeFile" />
    <File Path="./paket.dependencies" />
  </Folder>
</Solution>
"""
    withSlnx content (fun fileName ->
        let solution = SlnxSolutionFile fileName
        solution.AddPaketFolder("paket.dependencies", Some "paket.lock")
        solution.Save()

        folderFiles "/.Paket/" fileName |> shouldEqual [ [ "SomeFile"; "./paket.dependencies"; "paket.lock" ] ])

[<Test>]
let ``should fill an empty .paket folder of a slnx``() =
    let content = """<Solution>
  <Folder Name="/.paket/">
  </Folder>
</Solution>
"""
    withSlnx content (fun fileName ->
        convert fileName

        File.ReadAllText fileName
        |> shouldEqual """<Solution>
  <Folder Name="/.paket/">
    <File Path="paket.dependencies" />
  </Folder>
</Solution>
""")

[<Test>]
let ``should write the paths of a slnx with forward slashes``() =
    withSlnx "<Solution />" (fun fileName ->
        let solution = SlnxSolutionFile fileName
        solution.AddPaketFolder("..\\paket.dependencies", None)
        solution.Save()

        folderFiles "/.paket/" fileName |> shouldEqual [ [ "../paket.dependencies" ] ])

// The formatting

[<Test>]
let ``should keep the line breaks, indentation, comments and declaration of a slnx``() =
    let content = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<Solution>\r\n\t<!-- the app -->\r\n\t<Project Path=\"App.csproj\" />\r\n\r\n</Solution>"
    withSlnx content (fun fileName ->
        convert fileName

        File.ReadAllText fileName
        |> shouldEqual "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<Solution>\r\n\t<Folder Name=\"/.paket/\">\r\n\t\t<File Path=\"paket.dependencies\" />\r\n\t</Folder>\r\n\t<!-- the app -->\r\n\t<Project Path=\"App.csproj\" />\r\n\r\n</Solution>")

[<TestCase("<?xml version='1.0'?>")>]
[<TestCase("<?xml version='1.0' encoding='UTF-8'?>")>]
[<TestCase("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")>]
let ``should keep the declaration of a slnx as it is``(declaration: string) =
    withSlnx (declaration + "\n<Solution />") (fun fileName ->
        convert fileName

        File.ReadAllText fileName |> shouldEqual (declaration + "\n" + expectedAfterConvert))

[<Test>]
let ``should write the line breaks most of a slnx has``() =
    // the first line break is a CRLF, most of them are LF
    withSlnx "<Solution>\r\n  <!-- the app -->\n  <Project Path=\"App.csproj\" />\n</Solution>\n" (fun fileName ->
        convert fileName

        File.ReadAllText fileName
        |> shouldEqual "<Solution>\n  <Folder Name=\"/.paket/\">\n    <File Path=\"paket.dependencies\" />\n  </Folder>\n  <!-- the app -->\n  <Project Path=\"App.csproj\" />\n</Solution>\n")

[<Test>]
let ``should not rewrite an unchanged slnx``() =
    // XmlWriter would write these single quotes and this "/>" differently, so a rewrite shows
    let content = """<Solution>
    <Folder Name='/.paket/'><File Path='paket.dependencies'/></Folder>
</Solution>"""
    withSlnx content (fun fileName ->
        convert fileName

        File.ReadAllText fileName |> shouldEqual content)

// The NuGet entries of a .slnx

[<Test>]
let ``should remove the NuGet entries and the empty .nuget folder of a slnx``() =
    let content = """<Solution>
  <Folder Name="/.nuget/">
    <File Path=".nuget/NuGet.Config" />
    <File Path=".nuget\NuGet.exe" />
    <File Path="./.nuget/NuGet.targets" />
  </Folder>
  <Project Path="App.csproj" />
</Solution>
"""
    withSlnx content (fun fileName ->
        removeNuGetEntries fileName

        File.ReadAllText fileName
        |> shouldEqual """<Solution>
  <Project Path="App.csproj" />
</Solution>
""")

[<Test>]
let ``should keep the .nuget folder of a slnx when it holds other files``() =
    let content = """<Solution>
  <Folder Name="/.nuget/">
    <File Path=".nuget/NuGet.Config" />
    <File Path=".nuget/readme.txt" />
    <File Path="my.nuget/NuGet.Config" />
  </Folder>
</Solution>
"""
    withSlnx content (fun fileName ->
        removeNuGetEntries fileName

        folderFiles "/.nuget/" fileName |> shouldEqual [ [ ".nuget/readme.txt"; "my.nuget/NuGet.Config" ] ])

[<Test>]
let ``should only remove the NuGet entries of the root .nuget folder of a slnx``() =
    let content = """<Solution>
  <Folder Name="/.nuget/">
    <File Path="src/.nuget/NuGet.Config" />
  </Folder>
  <Folder Name="/src/.nuget/">
    <File Path="src/.nuget/NuGet.Config" />
  </Folder>
</Solution>
"""
    withSlnx content (fun fileName ->
        removeNuGetEntries fileName

        File.ReadAllText fileName |> shouldEqual content)

[<Test>]
let ``should remove the NuGet entries of any folder of a slnx``() =
    let content = """<Solution>
  <Folder Name="/Solution Items/">
    <File Path=".nuget/NuGet.Config" />
  </Folder>
  <Folder Name="/tools/">
    <File Path=".nuget/NuGet.exe" />
    <File Path="build.fsx" />
  </Folder>
</Solution>
"""
    withSlnx content (fun fileName ->
        removeNuGetEntries fileName

        File.ReadAllText fileName
        |> shouldEqual """<Solution>
  <Folder Name="/tools/">
    <File Path="build.fsx" />
  </Folder>
</Solution>
""")

[<Test>]
let ``should remove the NuGet entries of a slnx below the root directory``() =
    let content = """<Solution>
  <Folder Name="/.nuget/">
    <File Path="../.nuget/NuGet.targets" />
    <File Path=".nuget/NuGet.targets" />
  </Folder>
</Solution>
"""
    withSlnx content (fun fileName ->
        let solution = SlnxSolutionFile fileName
        // the root directory is the parent of the folder of the solution
        solution.RemoveNuGetEntries(nugetFilesOf (Path.GetDirectoryName(Path.GetDirectoryName fileName)))
        solution.Save()

        folderFiles "/.nuget/" fileName |> shouldEqual [ [ ".nuget/NuGet.targets" ] ])

[<Test>]
let ``should remove the entries of the NuGet files of the root directory of a slnx``() =
    let content = """<Solution>
  <Folder Name="/Solution Items/">
    <File Path="NuGet.Config" />
    <File Path="build.fsx" />
  </Folder>
</Solution>
"""
    withSlnx content (fun fileName ->
        let solution = SlnxSolutionFile fileName
        solution.RemoveNuGetEntries [ Path.Combine(Path.GetDirectoryName fileName, "NuGet.Config") ]
        solution.Save()

        folderFiles "/Solution Items/" fileName |> shouldEqual [ [ "build.fsx" ] ])

[<Test>]
let ``should keep the rooted NuGet entries of a slnx``() =
    let content = """<Solution>
  <Folder Name="/.nuget/">
    <File Path="//server/share/.nuget/NuGet.exe" />
    <File Path="/abs/.nuget/NuGet.Config" />
  </Folder>
</Solution>
"""
    withSlnx content (fun fileName ->
        // the full paths the entries would resolve to, so that only the rooted path check keeps them
        let nugetFiles =
            [ "//server/share/.nuget/NuGet.exe"; "/abs/.nuget/NuGet.Config" ]
            |> List.map (fun path -> Path.GetFullPath(Path.Combine(Path.GetDirectoryName fileName, path)))
        let solution = SlnxSolutionFile fileName
        solution.RemoveNuGetEntries nugetFiles
        solution.Save()

        File.ReadAllText fileName |> shouldEqual content)

[<Test>]
let ``should keep the entries of the NuGet files convert-from-nuget keeps in a slnx``() =
    let content = """<Solution>
  <Folder Name="/.nuget/">
    <File Path=".nuget/nuget.config" />
    <File Path=".nuget/NuGet.Config" />
    <File Path=".nuget/packages.config" />
  </Folder>
</Solution>
"""
    // only Linux tells NuGet.Config from nuget.config
    if not isLinux then
        Assert.Ignore "The paths are compared ignoring their case"
    withSlnx content (fun fileName ->
        let nugetFolder = Path.Combine(Path.GetDirectoryName fileName, ".nuget")
        let solution = SlnxSolutionFile fileName
        solution.RemoveNuGetEntries [ Path.Combine(nugetFolder, "nuget.config") ]
        solution.Save()

        folderFiles "/.nuget/" fileName |> shouldEqual [ [ ".nuget/NuGet.Config"; ".nuget/packages.config" ] ])

// The NuGet entries of a .sln

/// Runs RemoveNuGetEntries on a .sln made of the given lines and returns the saved lines
let private removeSlnEntries (lines: string list) (nugetFiles: string -> string list) =
    withTempDir (fun root ->
        let fileName = Path.Combine(root, "App.sln")
        File.WriteAllLines(fileName, slnLines lines)

        let solution = SolutionFile fileName
        solution.RemoveNuGetEntries(nugetFiles root)
        solution.Save()
        File.ReadAllLines fileName)

[<Test>]
let ``should only remove the sln entries of the deleted files``() =
    let lines =
        slnSolutionFolder ".nuget" "6A9DE8E5-3F35-4C0F-9F6B-0E2C46E3C0A1" [ @".nuget\NuGet.Config"; @".nuget\NuGet.exe"; @".nuget\packages.config" ]
    let saved = removeSlnEntries lines (fun root -> [ Path.Combine(root, ".nuget", "NuGet.Config"); Path.Combine(root, "NuGet.exe") ])

    saved |> Array.exists (fun line -> line.Contains @".nuget\NuGet.Config") |> shouldEqual false
    // the NuGet.exe deleted is the one of the root, not the one of .nuget
    saved |> Array.exists (fun line -> line.Contains @".nuget\NuGet.exe") |> shouldEqual true
    saved |> Array.exists (fun line -> line.Contains @".nuget\packages.config") |> shouldEqual true

[<Test>]
let ``should remove the sln entries of the deleted files by their path``() =
    let lines =
        [ yield! slnSolutionFolder "Solution Items" "0C5D4E35-6C1A-4C5E-9C0A-4E8C8D2F5B11" [ "NuGet.Config"; "build.fsx" ]
          yield! slnSolutionFolder "tools" "5E1C2A4B-8D3F-4F6A-A1B2-C3D4E5F60718" [ @"tools\.nuget\NuGet.Config" ]
          yield "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"nuget.exe\", \"tools\\.nuget\\nuget.exe.csproj\", \"{7A8B9C0D-1E2F-4A3B-8C4D-5E6F7A8B9C0D}\""
          yield "EndProject" ]
    let saved = removeSlnEntries lines (fun root -> [ Path.Combine(root, "NuGet.Config"); Path.Combine(root, ".nuget", "nuget.exe") ])

    saved |> Array.exists (fun line -> line.Trim() = "NuGet.Config = NuGet.Config") |> shouldEqual false
    saved |> Array.exists (fun line -> line.Contains "build.fsx") |> shouldEqual true
    saved |> Array.exists (fun line -> line.Contains @"tools\.nuget\NuGet.Config") |> shouldEqual true
    saved |> Array.exists (fun line -> line.Contains @"tools\.nuget\nuget.exe.csproj") |> shouldEqual true

[<Test>]
let ``should remove the .nuget folder of a sln once its entries are removed``() =
    let lines = slnSolutionFolder ".nuget" "6A9DE8E5-3F35-4C0F-9F6B-0E2C46E3C0A1" [ @".nuget\NuGet.Config"; @".nuget\NuGet.targets" ]
    let saved = removeSlnEntries lines (fun root -> [ Path.Combine(root, ".nuget", "NuGet.Config"); Path.Combine(root, ".nuget", "NuGet.targets") ])

    saved |> Array.exists (fun line -> line.Contains ".nuget") |> shouldEqual false

[<Test>]
let ``should keep the non-ASCII characters of a sln without a BOM``() =
    withTempDir (fun root ->
        let fileName = Path.Combine(root, "App.sln")
        let lines = slnLines [ "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Café\", \"Café\\Café.csproj\", \"{7A8B9C0D-1E2F-4A3B-8C4D-5E6F7A8B9C0D}\""; "EndProject" ]
        File.WriteAllLines(fileName, lines, UTF8Encoding false)

        let solution = SolutionFile fileName
        solution.AddPaketFolder("paket.dependencies", None)
        solution.Save()

        let bytes = File.ReadAllBytes fileName
        bytes.[0] |> shouldEqual (byte 'M')
        StringAssert.Contains("\"Café\\Café.csproj\"", UTF8Encoding(false, true).GetString bytes))

// The encodings

[<TestCase("utf-8", false, "")>]
[<TestCase("utf-8", true, "")>]
[<TestCase("utf-16LE", true, "")>]
[<TestCase("utf-16LE", false, "<?xml version=\"1.0\" encoding=\"utf-16\"?>")>]
[<TestCase("utf-16BE", true, "")>]
[<TestCase("utf-16BE", true, "<?xml version=\"1.0\" encoding=\"utf-16\"?>")>]
[<TestCase("utf-16BE", false, "<?xml version=\"1.0\" encoding=\"utf-16BE\"?>")>]
[<TestCase("utf-32LE", true, "")>]
[<TestCase("utf-32LE", false, "")>]
[<TestCase("utf-32LE", false, "<?xml version=\"1.0\" encoding=\"utf-32\"?>")>]
[<TestCase("utf-32BE", true, "<?xml version=\"1.0\" encoding=\"utf-32BE\"?>")>]
let ``should keep the encoding and the BOM state of a Unicode slnx``(encodingName: string, withBom: bool, declaration: string) =
    let bigEndian = encodingName.EndsWith "BE"
    let encoding : Encoding =
        if encodingName.StartsWith "utf-32" then UTF32Encoding(bigEndian, withBom)
        elif encodingName.StartsWith "utf-16" then UnicodeEncoding(bigEndian, withBom)
        else UTF8Encoding withBom
    let content = declaration + "<Solution />"
    withSlnxBytes (Array.append (encoding.GetPreamble()) (encoding.GetBytes content)) (fun fileName ->
        convert fileName

        let bytes = File.ReadAllBytes fileName
        let preamble = encoding.GetPreamble()
        bytes.[0 .. preamble.Length - 1] |> shouldEqual preamble
        if not withBom then
            // the file starts with "<", without a BOM
            encoding.GetString(bytes, 0, encoding.GetByteCount "<") |> shouldEqual "<"
        let text = encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length)
        text |> shouldEqual (declaration + expectedAfterConvert))

[<Test>]
let ``should keep the declared encoding of a slnx``() =
    let latin1 = Encoding.GetEncoding "iso-8859-1"
    let content = "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?>\n<Solution>\n  <Project Path=\"café/App.csproj\" />\n</Solution>\n"
    withSlnxBytes (latin1.GetBytes content) (fun fileName ->
        convert fileName

        latin1.GetString(File.ReadAllBytes fileName)
        |> shouldEqual "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?>\n<Solution>\n  <Folder Name=\"/.paket/\">\n    <File Path=\"paket.dependencies\" />\n  </Folder>\n  <Project Path=\"café/App.csproj\" />\n</Solution>\n")

[<Test>]
let ``should write the characters a slnx encoding lacks as character references``() =
    let latin1 = Encoding.GetEncoding "iso-8859-1"
    let content = "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?>\n<Solution>\n  <Project Path=\"a&#x20AC;.fsproj\" />\n</Solution>\n"
    withSlnxBytes (latin1.GetBytes content) (fun fileName ->
        convert fileName

        StringAssert.Contains("<Project Path=\"a&#x20AC;.fsproj\" />", latin1.GetString(File.ReadAllBytes fileName)))

[<Test>]
let ``should save a slnx declared in an encoding .NET cannot create in the encoding it was read in``() =
    withSlnx "<?xml version=\"1.0\" encoding=\"ucs-4\"?>\n<Solution>\n  <Project Path=\"pé/App.csproj\" />\n</Solution>\n" (fun fileName ->
        convert fileName

        let bytes = File.ReadAllBytes fileName
        bytes.[0] |> shouldEqual (byte '<')
        UTF8Encoding(false, true).GetString bytes
        |> shouldEqual "<?xml version=\"1.0\" encoding=\"ucs-4\"?>\n<Solution>\n  <Folder Name=\"/.paket/\">\n    <File Path=\"paket.dependencies\" />\n  </Folder>\n  <Project Path=\"pé/App.csproj\" />\n</Solution>\n")

[<Test>]
let ``should reject a slnx whose BOM and declaration give different encodings``() =
    let content = UTF8Encoding(false).GetBytes "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?>\n<Solution>\n  <Project Path=\"pé/App.csproj\" />\n</Solution>\n"
    withSlnxBytes (Array.append (UTF8Encoding(true).GetPreamble()) content) (fun fileName ->
        let exn = Assert.Throws<Exception>(fun () -> SlnxSolutionFile fileName |> ignore)

        StringAssert.Contains(fileName, exn.Message)
        StringAssert.Contains("different encodings", exn.Message))

[<Test>]
let ``should reject a slnx whose UTF-32 BOM and declaration give different byte orders``() =
    // the reader reads the declaration with the BOM, then switches to the declared byte order
    let littleEndian = UTF32Encoding(false, true)
    let bytes =
        Array.concat [ littleEndian.GetPreamble()
                       littleEndian.GetBytes "<?xml version=\"1.0\" encoding=\"utf-32BE\"?>"
                       UTF32Encoding(true, false).GetBytes "<Solution />" ]
    withSlnxBytes bytes (fun fileName ->
        let exn = Assert.Throws<Exception>(fun () -> SlnxSolutionFile fileName |> ignore)

        StringAssert.Contains(fileName, exn.Message)
        StringAssert.Contains("different encodings", exn.Message))

[<TestCase("2143")>]
[<TestCase("3412")>]
let ``should reject a UCS-4 slnx in an unusual byte order``(byteOrder: string) =
    // the UTF-32 BE bytes, b0 b1 b2 b3, of each character in the given order
    let order = byteOrder |> Seq.map (fun digit -> int digit - int '1') |> Seq.toArray
    let bytes =
        UTF32Encoding(true, false).GetBytes "<Solution />"
        |> Array.chunkBySize 4
        |> Array.collect (fun character -> order |> Array.map (fun index -> character.[index]))
    withSlnxBytes bytes (fun fileName ->
        let exn = Assert.Throws<Exception>(fun () -> SlnxSolutionFile fileName |> ignore)

        StringAssert.Contains(fileName, exn.Message)
        File.ReadAllBytes fileName |> shouldEqual bytes)

[<Test>]
let ``should reject a slnx with invalid UTF-8``() =
    withSlnxBytes (Array.concat [ Encoding.ASCII.GetBytes "<Solution><Project Path=\""; [| 0xC3uy; 0x28uy |]; Encoding.ASCII.GetBytes "\" /></Solution>" ]) (fun fileName ->
        let exn = Assert.Throws<Exception>(fun () -> SlnxSolutionFile fileName |> ignore)

        StringAssert.Contains(fileName, exn.Message))

[<Test>]
let ``should reject a slnx whose content is not valid in its declared encoding``() =
    // UTF-8 bytes, which the reader would replace with "?" when decoding them as US-ASCII
    let bytes = UTF8Encoding(false).GetBytes "<?xml version=\"1.0\" encoding=\"us-ascii\"?>\n<Solution>\n  <Project Path=\"Café/Café.csproj\" />\n</Solution>\n"
    withSlnxBytes bytes (fun fileName ->
        let exn = Assert.Throws<Exception>(fun () -> SlnxSolutionFile fileName |> ignore)

        StringAssert.Contains(fileName, exn.Message)
        StringAssert.Contains("not valid us-ascii", exn.Message)
        File.ReadAllBytes fileName |> shouldEqual bytes)

// The files that cannot be read

[<Test>]
let ``should reject a slnx with a DOCTYPE without expanding its entities``() =
    let content = """<?xml version="1.0"?>
<!DOCTYPE Solution [ <!ENTITY secret SYSTEM "file:///etc/passwd"> <!ENTITY proj "App.csproj"> ]>
<Solution>
  <Project Path="&proj;">&secret;</Project>
</Solution>
"""
    withSlnx content (fun fileName ->
        let exn = Assert.Throws<Exception>(fun () -> SlnxSolutionFile fileName |> ignore)

        StringAssert.Contains(fileName, exn.Message)
        StringAssert.Contains("cannot contain a DOCTYPE", exn.Message)
        File.ReadAllText fileName |> shouldEqual content)

[<Test>]
let ``should blame the DOCTYPE of a slnx that has another error too``() =
    withSlnx "<!DOCTYPE Solution>\n<Solution>\n  <Project Path=\"App.csproj\">\n</Solution>\n" (fun fileName ->
        let exn = Assert.Throws<Exception>(fun () -> SlnxSolutionFile fileName |> ignore)

        StringAssert.Contains(fileName, exn.Message)
        StringAssert.Contains("cannot contain a DOCTYPE", exn.Message))

[<Test>]
let ``should report the real error of a malformed slnx that mentions a DOCTYPE in a comment``() =
    withSlnx "<Solution>\n  <!-- no <!DOCTYPE here -->\n  <Project Path=\"App.csproj\">\n</Solution>\n" (fun fileName ->
        let exn = Assert.Throws<Exception>(fun () -> SlnxSolutionFile fileName |> ignore)

        StringAssert.Contains(fileName, exn.Message)
        StringAssert.DoesNotContain("DOCTYPE", exn.Message))

[<Test>]
let ``should name the slnx that cannot be parsed``() =
    withSlnx "<Solution>\n  <Project Path=\"App.csproj\">\n</Solution>\n" (fun fileName ->
        let exn = Assert.Throws<Exception>(fun () -> SlnxSolutionFile fileName |> ignore)

        StringAssert.Contains(fileName, exn.Message))

[<TestCase("<settings />")>]
[<TestCase("<Solution xmlns=\"urn:x\" />")>]
let ``should reject a slnx whose root is not a Solution``(content: string) =
    withSlnx content (fun fileName ->
        let exn = Assert.Throws<Exception>(fun () -> SlnxSolutionFile fileName |> ignore)

        StringAssert.Contains(fileName, exn.Message)
        StringAssert.Contains("root element is not <Solution>", exn.Message))
