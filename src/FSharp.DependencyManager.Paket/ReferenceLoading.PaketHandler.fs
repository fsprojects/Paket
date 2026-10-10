/// Paket invokation for In-Script reference loading
namespace ReferenceLoading.Internals
module Logic =

    let paketVersionSortForNugetCacheFolder somethingAndNames =
        somethingAndNames 
        |> Seq.map 
            (fun (something, (name: string)) ->
                let splits = name.Split([|'-'|], 2)
                something,
                match splits with
                | [|numbers|] ->
                    let splits = numbers.Split('.')
                    [|
                      for split in splits do
                        match System.UInt64.TryParse split with
                        | false,_ -> ()
                        | true, v -> v
                    |], []

                | [|numbers;rest|] ->
                    let splits = numbers.Split('.')
                    [|
                      for split in splits do
                        match System.UInt64.TryParse split with
                        | false,_ -> ()
                        | true, v -> v
                    |], [ rest]
                | _ -> 
                    [||], []
                )
        |> Seq.groupBy (fst << snd)
        |> Seq.map (fun (number, versions) ->
            let struo =
                versions
                |> Seq.map(fun (something, (version, rest) ) -> something, rest)
                |> Seq.sortByDescending snd
            number, struo)
        |> Seq.sortByDescending fst
        //|> Seq.concat
        //|> Seq.map fst

namespace ReferenceLoading
module internal PaketHandler =

    open System
    open System.IO

    let PM_DIR = ".paket"

    let userProfile =
        let res = Environment.GetEnvironmentVariable("USERPROFILE")
        if System.String.IsNullOrEmpty res then
            Environment.GetEnvironmentVariable("HOME")
        else res

    let tweakTargetFramework =
        function
            | "net5.0" -> "net50"
            | "netcoreapp5.0" -> "net50"
            | targetFramework -> targetFramework

    let MakeDependencyManagerCommand scriptType packageManagerTargetFramework projectRootDirArgument = 
        let packageManagerTargetFramework = tweakTargetFramework packageManagerTargetFramework

        sprintf "install --generate-load-scripts --load-script-type %s --load-script-framework %s project-root \"%s\""
            scriptType packageManagerTargetFramework (System.IO.Path.GetFullPath projectRootDirArgument)

    let getDirectoryAndAllParentDirectories (directory: DirectoryInfo) =
        let rec allParents (directory: DirectoryInfo) =
            seq {
                match directory.Parent with
                | null -> ()
                | parent -> 
                    yield parent
                    yield! allParents parent
            }

        seq {
            yield directory
            yield! allParents directory
        }

    let isWindows =
        System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)

    /// The executable `dotnet tool install paket --tool-path` writes
    let toolPathExecutable = if isWindows then "paket.exe" else "paket"

    /// Walks up directory structure and tries to find paket installed with --tool-path
    let findPaketExe (prioritizedSearchPaths: string seq) (baseDir: DirectoryInfo) =
        let dirs = [
            yield! Seq.map DirectoryInfo prioritizedSearchPaths
            yield! getDirectoryAndAllParentDirectories baseDir
        ]

        // for each given directory, we look for paket and .paket/paket
        dirs
        |> Seq.collect (fun dir -> [dir; yield! dir.GetDirectories(PM_DIR)])
        |> Seq.map (fun dir -> Path.Combine(dir.FullName, toolPathExecutable))
        |> Seq.tryFind File.Exists

    let nugetPackagesFolder =
        match Environment.GetEnvironmentVariable "NUGET_PACKAGES" with
        | null | "" -> Path.Combine(userProfile, ".nuget", "packages")
        | folder -> folder

    /// paket.dll of a version of the Paket .NET tool restored into the NuGet packages folder
    let tryFindToolDll (versionFolder: DirectoryInfo) =
        let tools = DirectoryInfo(Path.Combine(versionFolder.FullName, "tools"))
        if not tools.Exists then None else
        tools.GetDirectories()
        |> Seq.map (fun frameworkFolder -> Path.Combine(frameworkFolder.FullName, "any", "paket.dll"))
        |> Seq.tryFind File.Exists

    /// The first tool manifest up from the directory that has paket, with the version of paket
    let findLocalTool (baseDir: DirectoryInfo) =
        let paketVersion =
            System.Text.RegularExpressions.Regex(
                "\"paket\"\\s*:\\s*\\{[^}]*?\"version\"\\s*:\\s*\"([^\"]+)\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        getDirectoryAndAllParentDirectories baseDir
        |> Seq.collect (fun dir ->
            [ Path.Combine(dir.FullName, ".config", "dotnet-tools.json")
              Path.Combine(dir.FullName, "dotnet-tools.json") ])
        |> Seq.filter File.Exists
        |> Seq.tryPick (fun manifest ->
            let m = paketVersion.Match(File.ReadAllText manifest)
            if m.Success then Some(manifest, m.Groups.[1].Value) else None)

    /// Runs a framework-dependent paket.dll with the dotnet that runs the script
    let dotnetCommand (paketDll: string) =
        let dotnet =
            match Environment.GetEnvironmentVariable "DOTNET_HOST_PATH" with
            | null | "" -> "dotnet"
            | path -> path
        dotnet, sprintf "\"%s\" " paketDll

    /// Resolves absolute load script location: something like
    /// baseDir/.paket/load/scriptName
    /// or
    /// baseDir/.paket/load/frameworkDir/scriptName 
    let GetPaketLoadScriptLocation baseDir optionalFrameworkDir scriptName =
        let paketLoadFolder = System.IO.Path.Combine(PM_DIR,"load")
        let frameworkDir =
            match optionalFrameworkDir with 
            | None -> paketLoadFolder 
            | Some frameworkDir -> System.IO.Path.Combine(paketLoadFolder, frameworkDir)

        System.IO.Path.Combine(baseDir, frameworkDir, scriptName)

    
    /// Resolve packages loaded into scripts using `paket:` in `#r` directives such as `#r @"paket: nuget AmazingNugetPackage"`. 
    /// <remarks>This function will throw if the resolution is not successful or the tool wasn't found</remarks>
    /// <param name="fileType">A string given to paket command to select the output language. Can be `fsx` or `csx`</param>
    /// <param name="targetFramework">A string given to paket command to fix the framework.</param>
    /// <param name="prioritizedSearchPaths">List of directories which are checked first to resolve paket installed with --tool-path.</param>
    /// <param name="scriptDir"The folder containing the script</param>
    /// <param name="scriptName">filename for the script (not necessarily existing if interactive evaluation)</param>
    /// <param name="packageManagerTextLinesFromScript">Package manager text lines from script, those are meant to be just the inner part, without `#r "paket:` prefix</param>
    let ResolveDependenciesForLanguage(fileType, targetFramework:string, prioritizedSearchPaths: string seq, scriptDir: string, scriptName: string, packageManagerTextLinesFromScript: string seq) =
        let hashString (str: string) =
          // https://andrewlock.net/why-is-string-gethashcode-different-each-time-i-run-my-program-in-net-core/
          let mutable hash1 = (5381 <<< 16) + 5381
          let mutable hash2 = hash1
          if str.Length = 0 then 
            hash2 
          else
            for i in 0 .. 2 .. str.Length do
              if i < str.Length then
                hash1 <- ((hash1 <<< 5) + hash1) ^^^ (int str.[i])
              if i + 1 < str.Length then
                hash2 <- ((hash2 <<< 5) + hash2) ^^^ (int str.[i + 1])
            hash1 + (hash2 * 1566083941)
        let scriptDirHash = abs (hashString (scriptDir + scriptName))
        let workingDir = Path.Combine(Path.GetTempPath(), "script-packages", string scriptDirHash)
        let depsFileName = "paket.dependencies"
        let workingDirSpecFile = FileInfo(Path.Combine(workingDir,depsFileName))
        if not (Directory.Exists workingDir) then
            Directory.CreateDirectory workingDir |> ignore

        let packageManagerTextLinesFromScript = 
            packageManagerTextLinesFromScript
            |> Seq.toList
            |> List.filter (not << String.IsNullOrWhiteSpace)
        
        let rootDir,packageManagerTextLines =
            let rec findSpecFile dir =
                let fi = FileInfo(Path.Combine(dir,depsFileName))
                if fi.Exists then
                    let lockfileName = "paket.lock"
                    let lockFile = FileInfo(Path.Combine(fi.Directory.FullName,lockfileName))
                    let depsFileLines = File.ReadAllLines fi.FullName
                    if lockFile.Exists then
                        let originalDepsFile = FileInfo(workingDirSpecFile.FullName + ".original")
                        if not originalDepsFile.Exists ||
                            File.ReadAllLines originalDepsFile.FullName <> depsFileLines
                        then
                            File.Copy(fi.FullName,originalDepsFile.FullName,true)
                            let targetLockFile = FileInfo(Path.Combine(workingDir,lockfileName))
                            File.Copy(lockFile.FullName,targetLockFile.FullName,true)
                    
                    let lines = 
                        if List.isEmpty packageManagerTextLinesFromScript then 
                            Array.toList depsFileLines
                        else
                            (Array.toList depsFileLines) @ ("group Main" :: packageManagerTextLinesFromScript)

                    fi.Directory.FullName, lines
                elif not (isNull fi.Directory.Parent) then
                    findSpecFile fi.Directory.Parent.FullName
                else
                    let withImplicitSource = 
                        match packageManagerTextLinesFromScript with
                        | line::_ when line.StartsWith "source" -> packageManagerTextLinesFromScript
                        | _  -> "source https://nuget.org/api/v2" :: packageManagerTextLinesFromScript
                    workingDir, ("framework: " + targetFramework) :: withImplicitSource
           
            findSpecFile scriptDir

        /// hardcoded to load the "Main" group (implicit in paket)
        let loadScript = GetPaketLoadScriptLocation workingDir (Some (tweakTargetFramework targetFramework)) ("main.group." + fileType)
        let additionalIncludeFolders() =
            [Path.Combine(workingDir,"paket-files")]
            |> List.filter Directory.Exists

        if workingDirSpecFile.Exists && 
            (File.ReadAllLines(workingDirSpecFile.FullName) |> Array.toList) = packageManagerTextLines && 
            File.Exists loadScript
        then 
            (loadScript,additionalIncludeFolders())
        else 
            let toolPath, toolArguments =
                // we try to resolve paket installed with --tool-path any place up in the folder structure from current script
                match findPaketExe prioritizedSearchPaths (DirectoryInfo scriptDir) with
                | Some paketExe -> paketExe, ""
                | None ->

                match findLocalTool (DirectoryInfo scriptDir) with
                | Some (manifest, version) ->
                    match tryFindToolDll (DirectoryInfo(Path.Combine(nugetPackagesFolder, "paket", version.ToLowerInvariant()))) with
                    | Some paketDll -> dotnetCommand paketDll
                    | None ->
                        failwithf "Paket %s, the local tool of '%s', is not restored. Please run 'dotnet tool restore'." version manifest
                | None ->

                  let globalTools =
                    [
                      Path.Combine(userProfile, ".dotnet", "tools")
                      Path.Combine(userProfile, PM_DIR)
                    ]
                    |> List.map (fun dir -> Path.Combine(dir, toolPathExecutable))

                  match globalTools |> List.tryFind File.Exists with
                  | Some paketExe -> paketExe, ""
                  | None ->

                  // the newest Paket .NET tool restored in the NuGet packages folder
                  let nugetDir = DirectoryInfo(Path.Combine(nugetPackagesFolder, "paket"))
                  let restoredTool =
                    if not nugetDir.Exists then None
                    else
                      nugetDir.GetDirectories()
                      |> Seq.map (fun d -> d, d.Name)
                      |> Internals.Logic.paketVersionSortForNugetCacheFolder
                      |> Seq.map snd
                      |> Seq.concat
                      |> Seq.map fst
                      |> Seq.tryPick tryFindToolDll

                  match restoredTool with
                  | Some paketDll -> dotnetCommand paketDll
                  | None ->
                    let foldersTried =
                      [ yield! globalTools; nugetDir.FullName ]
                      |> List.map (fun f -> sprintf " - %s" f)
                      |> String.concat Environment.NewLine
                    failwithf "Paket was not found in '%s' or a parent directory, as a local tool, or in those locations:\n\n%s\n\nPlease install the Paket .NET tool: dotnet tool install paket"
                        scriptDir foldersTried

            Console.ForegroundColor <- ConsoleColor.Green
            Console.Write ":paket>"
            Console.ResetColor()
            Console.WriteLine(sprintf " using %s" ((toolPath + " " + toolArguments).TrimEnd()))
        
            try File.Delete(loadScript) with _ -> ()
            File.WriteAllLines(workingDirSpecFile.FullName, packageManagerTextLines)
            let startInfo = 
                System.Diagnostics.ProcessStartInfo(
                    FileName = toolPath,
                    WorkingDirectory = workingDir, 
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Arguments = toolArguments + MakeDependencyManagerCommand fileType targetFramework rootDir,
                    CreateNoWindow = true,
                    UseShellExecute = false)
                
            use p = new System.Diagnostics.Process()
            let errors = ResizeArray<_>()
            let log = ResizeArray<_>()
            p.StartInfo <- startInfo
            p.ErrorDataReceived.Add(fun d -> if not (isNull d.Data) then errors.Add d.Data)
            p.OutputDataReceived.Add(fun d -> 
                if not (isNull d.Data) then
                    Console.ForegroundColor <- ConsoleColor.Green
                    Console.Write ":paket>"
                    Console.ResetColor()
                    Console.WriteLine (" " + d.Data)
                    log.Add d.Data
            )
            p.Start() |> ignore
            p.BeginErrorReadLine()
            p.BeginOutputReadLine()
            p.WaitForExit()
            
            if p.ExitCode <> 0 then
                let msg = String.Join(Environment.NewLine, errors)
                failwithf "Package resolution using '%s' failed, see directory '%s'.%s%s"
                    toolPath workingDir Environment.NewLine msg
            else
                (loadScript,additionalIncludeFolders())

    /// Resolve packages loaded into scripts using `paket:` in `#r` directives such as `#r @"paket: nuget AmazingNugetPackage"`. 
    /// <remarks>This function will throw if the resolution is not successful or the tool wasn't found</remarks>
    /// <param name="targetFramework">A string given to paket command to fix the framework.</param>
    /// <param name="scriptDir"The folder containing the script</param>
    /// <param name="scriptName">filename for the script (not necessarily existing if interactive evaluation)</param>
    /// <param name="packageManagerTextLinesFromScript">Package manager text lines from script, those are meant to be just the inner part, without `#r "paket:` prefix</param>
    let ResolveDependencies(targetFramework:string, scriptDir: string, scriptName: string,packageManagerTextLinesFromScript: string seq) =
        let extension = 
            if scriptName.ToLowerInvariant().EndsWith(".fsx") then "fsx"
            elif scriptName.ToLowerInvariant().EndsWith(".csx") then "csx"
            else
                // default to F# in case the calling process doesn't honor giving the script name to discriminate on 
                "fsx"
        
        ResolveDependenciesForLanguage(extension, targetFramework, Seq.empty, scriptDir, scriptName, packageManagerTextLinesFromScript)
