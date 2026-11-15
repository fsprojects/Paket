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
                // a release comes before its prereleases
                |> Seq.sortByDescending (fun (_, rest) -> List.isEmpty rest, rest)
            number, struo)
        |> Seq.sortByDescending fst
        //|> Seq.concat
        //|> Seq.map fst

    /// What the content of a tool manifest says of paket: the version it declares, if any, and
    /// whether it is "isRoot", which dotnet doesn't look above
    let paketOfToolManifest (content: string) =
        let options = System.Text.RegularExpressions.RegexOptions.IgnoreCase
        let paketVersion =
            System.Text.RegularExpressions.Regex.Match(content, "\"paket\"\\s*:\\s*\\{[^}]*?\"version\"\\s*:\\s*\"([^\"]+)\"", options)
        let isRoot = System.Text.RegularExpressions.Regex.IsMatch(content, "\"isRoot\"\\s*:\\s*true", options)
        (if paketVersion.Success then Some paketVersion.Groups.[1].Value else None), isRoot

    /// Where to look for dotnet, in order: the host that runs the script (DOTNET_HOST_PATH, the
    /// running process when it is dotnet, the root of the running runtime), then DOTNET_ROOT, the
    /// PATH and the default install folders. A relative path, which would depend on the current
    /// folder, or on the current drive on Windows, is left out.
    let dotnetCandidates
        (isWindows: bool)
        (getEnvironmentVariable: string -> string)
        (processPath: string option)
        (runtimeDirectory: string option)
        (programFiles: string)
        (userProfile: string)
        =
        let dotnetExe = if isWindows then "dotnet.exe" else "dotnet"
        // file names are compared without case on Windows only
        let nameComparison = if isWindows then System.StringComparison.OrdinalIgnoreCase else System.StringComparison.Ordinal
        let inFolder (folders: string list) =
            if List.exists System.String.IsNullOrEmpty folders
            then None
            else
                // on .NET Framework, Path.Combine throws on invalid characters, such as quotes
                try
                    Some(System.IO.Path.Combine(Array.ofList (folders @ [ dotnetExe ])))
                with _ -> None
        let isFullyQualified (path: string) =
            if isWindows then
                // a drive or a UNC path, and not \folder or C:folder, which depend on the current drive or on
                // its current folder
                path.StartsWith(@"\\", System.StringComparison.Ordinal)
                || path.StartsWith("//", System.StringComparison.Ordinal)
                || (path.Length >= 3 && System.Char.IsLetter path.[0] && path.[1] = ':' && (path.[2] = '\\' || path.[2] = '/'))
            else
                System.IO.Path.IsPathRooted path
        [
            match getEnvironmentVariable "DOTNET_HOST_PATH" with
            | null | "" -> ()
            | path -> yield Some path
            // `dotnet fsi`
            match processPath with
            | Some exe when System.String.Equals(System.IO.Path.GetFileName exe, dotnetExe, nameComparison) -> yield Some exe
            | _ -> ()
            // the root of the runtime that runs the script, <root>/shared/Microsoft.NETCore.App/<version>/,
            // and not the folder of the .NET Framework or of a self-contained app
            match runtimeDirectory with
            | None -> ()
            | Some runtimeDirectory ->
                yield
                    try
                        let app = System.IO.DirectoryInfo(runtimeDirectory).Parent
                        let shared = if isNull app then null else app.Parent
                        if not (isNull shared)
                           && not (isNull shared.Parent)
                           && System.String.Equals(app.Name, "Microsoft.NETCore.App", nameComparison)
                           && System.String.Equals(shared.Name, "shared", nameComparison)
                        then inFolder [ shared.Parent.FullName ]
                        else None
                    with _ -> None
            yield inFolder [ getEnvironmentVariable "DOTNET_ROOT" ]
            match getEnvironmentVariable "PATH" with
            | null -> ()
            | path -> yield! path.Split(System.IO.Path.PathSeparator) |> Seq.map (fun folder -> inFolder [ folder.Trim '"' ])
            if isWindows then
                // the Program Files of a 32-bit process is Program Files (x86)
                yield inFolder [ getEnvironmentVariable "ProgramW6432"; "dotnet" ]
                yield inFolder [ programFiles; "dotnet" ]
                // the default of dotnet-install.ps1
                yield inFolder [ getEnvironmentVariable "LOCALAPPDATA"; "Microsoft"; "dotnet" ]
            else
                yield inFolder [ "/usr/local/share/dotnet" ]
                yield inFolder [ "/usr/share/dotnet" ]
                yield inFolder [ "/usr/lib/dotnet" ]
                yield inFolder [ "/usr/lib64/dotnet" ]
            // the default of dotnet-install.sh
            yield inFolder [ userProfile; ".dotnet" ]
        ]
        |> List.choose id
        |> List.filter isFullyQualified
        |> List.distinct

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

    /// Whether paket installed with --tool-path is the global tool of ~/.paket, which the walk up from
    /// a script in the home folder finds too
    let isGlobalToolPath (paketExe: string) =
        not (String.IsNullOrEmpty userProfile)
        && String.Equals(
            Path.GetDirectoryName paketExe,
            Path.GetFullPath(Path.Combine(userProfile, PM_DIR)),
            StringComparison.OrdinalIgnoreCase)

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

    /// The first tool manifest up from the directory that has paket, with the version of paket.
    /// As dotnet, it doesn't look above an "isRoot" manifest.
    let findLocalTool (baseDir: DirectoryInfo) =
        getDirectoryAndAllParentDirectories baseDir
        |> Seq.collect (fun dir ->
            [ Path.Combine(dir.FullName, ".config", "dotnet-tools.json")
              Path.Combine(dir.FullName, "dotnet-tools.json") ])
        |> Seq.filter File.Exists
        |> Seq.map (fun manifest -> manifest, Internals.Logic.paketOfToolManifest (File.ReadAllText manifest))
        |> Seq.tryFind (fun (_, (version, isRoot)) -> Option.isSome version || isRoot)
        |> Option.bind (fun (manifest, (version, _)) -> version |> Option.map (fun version -> manifest, version))

    /// The dotnet to run a framework-dependent paket.dll: the first of dotnetCandidates that exists
    let findDotnet () =
        let processPath =
            try
                use current = System.Diagnostics.Process.GetCurrentProcess()
                use mainModule = current.MainModule
                Option.ofObj mainModule.FileName
            with _ -> None
        let runtimeDirectory =
            try Some(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()) with _ -> None
        Internals.Logic.dotnetCandidates
            isWindows
            Environment.GetEnvironmentVariable
            processPath
            runtimeDirectory
            (Environment.GetFolderPath Environment.SpecialFolder.ProgramFiles)
            userProfile
        |> List.tryFind File.Exists

    /// The dotnet of findDotnet and the arguments that run a framework-dependent paket.dll; fails
    /// when there is no dotnet
    let dotnetCommand (paketDll: string) =
        match findDotnet () with
        | Some dotnet -> dotnet, $"\"%s{paketDll}\" "
        | None ->
            failwith $"No dotnet was found to run '%s{paketDll}'. Please set DOTNET_ROOT to the folder of dotnet in the environment of the IDE or app that runs F# Interactive, or add that folder to its PATH."

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
    /// <remarks>This function will throw if the resolution is not successful, or if the tool or a dotnet to run it wasn't found</remarks>
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
                let localTool = findLocalTool (DirectoryInfo scriptDir)
                // we try to resolve paket installed with --tool-path any place up in the folder structure from current script
                let toolPathExe =
                    match findPaketExe prioritizedSearchPaths (DirectoryInfo scriptDir), localTool with
                    // a global tool comes after the local tool, without a warning
                    | Some paketExe, Some _ when isGlobalToolPath paketExe -> None
                    // as in the targets, the local tool runs instead: it can be an older Paket, such as the paket.exe or bootstrapper of Paket 11
                    | Some paketExe, Some (manifest, _) ->
                        Console.ForegroundColor <- ConsoleColor.Yellow
                        Console.Write ":paket>"
                        Console.ResetColor()
                        Console.WriteLine $" Ignoring %s{paketExe}: '%s{manifest}' declares paket, which runs instead. Delete it if it is an older Paket, such as the paket.exe or bootstrapper of Paket 11."
                        None
                    | paketExe, _ -> paketExe

                match toolPathExe with
                | Some paketExe -> paketExe, ""
                | None ->

                match localTool with
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
    /// <remarks>This function will throw if the resolution is not successful, or if the tool or a dotnet to run it wasn't found</remarks>
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
