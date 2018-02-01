module Paket.RepoTools

open System
open System.IO
open Paket
open Paket.Domain

module RepoToolDiscovery =

    open Paket.Logging

    type RepoToolInNupkg =
        { FullPath: string
          Name: string
          WorkingDirectory: RepoToolInNupkgWorkingDirectoryPath
          Kind: RepoToolInNupkgKind }
    and [<RequireQualifiedAccess>] RepoToolInNupkgKind =
        | OldStyle
        | ByTFM of tfm:FrameworkIdentifier
    and [<RequireQualifiedAccess>] RepoToolInNupkgWorkingDirectoryPath =
        | ScriptDirectory
        | CurrentDirectory

    let getPreferenceFromEnv () =
        match System.Environment.GetEnvironmentVariable("PAKET_REPOTOOL_PREFERRED_RUNTIME") |> Option.ofObj |> Option.bind FrameworkDetection.Extract with
        | None -> FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4_5)
        | Some fw -> fw

    let applyPreferencesAboutRuntimeHost preferredFw (pkgs: RepoToolInNupkg list) =

        pkgs
        |> List.groupBy (fun p -> p.Name)
        |> List.collect (fun (name, tools) ->
            let byPreference tool =
                match tool.Kind, preferredFw with
                | RepoToolInNupkgKind.ByTFM(FrameworkIdentifier.DotNetCoreApp _), FrameworkIdentifier.DotNetCoreApp _ -> 1
                | RepoToolInNupkgKind.ByTFM(FrameworkIdentifier.DotNetFramework _), FrameworkIdentifier.DotNetFramework _ -> 1
                | RepoToolInNupkgKind.OldStyle, FrameworkIdentifier.DotNetFramework _ -> 2
                | _ -> 3
            match tools |> List.sortBy byPreference with
            | [] -> []
            | [x] -> [x]
            | x :: xs ->
                if verbose then
                    verbosefn "tool '%s' support multiple frameworks" name
                    verbosefn "- choosen %A based on preference %A" x preferredFw
                    for d in xs do
                        verbosefn "- avaiable but ignored: %A" d
                [x] )

    let avaiableTools (pkg: PackageResolver.PackageInfo) (pkgDir: DirectoryInfo) =
        let toolsDir = Path.Combine(pkgDir.FullName, "tools")

        let getNameOf (exeName: string) =
            let caseInsensitiveMap =
                pkg.Settings.RepotoolAliases
                |> Map.toList
                |> List.map (fun (s,v) -> s.ToUpper(),v)
                |> Map.ofList
            match caseInsensitiveMap |> Map.tryFind (exeName.ToUpper()) with
            | Some name-> name
            | None -> exeName

        let asTool kind path =
            { RepoToolInNupkg.FullPath = path
              Name = getNameOf (Path.GetFileNameWithoutExtension(path))
              WorkingDirectory =
                match pkg.Settings.RepotoolWorkingDirectory with
                | Requirements.RepotoolWorkingDirectoryPath.ScriptDir -> RepoToolInNupkgWorkingDirectoryPath.ScriptDirectory
                | Requirements.RepotoolWorkingDirectoryPath.CurrentDirectory -> RepoToolInNupkgWorkingDirectoryPath.CurrentDirectory
              Kind = kind }

        let toolsTFMDirs =
            Directory.EnumerateDirectories(toolsDir, "*", SearchOption.TopDirectoryOnly)
            |> Seq.choose (fun x ->
                match x |> Path.GetFileName |> FrameworkDetection.Extract with
                | Some tfm -> Some (x, tfm)
                | None -> None)
            |> Seq.toList

        [ 
            //old style (flatten): the .exe directly in tools dir are .net fw console app
            yield! Directory.EnumerateFiles(toolsDir, "*.exe")
                   |> Seq.map (asTool RepoToolInNupkgKind.OldStyle)

            //new style (dir by tfm): each dir with a tfm name may contains a console app
            for (toolsTFMDir, tfm) in toolsTFMDirs do
                yield!
                    match tfm with
                    | FrameworkIdentifier.DotNetFramework _ ->
                        // for .net fw, just check .exe extension
                        Directory.EnumerateFiles(toolsTFMDir , "*.exe")
                        |> Seq.map (asTool (RepoToolInNupkgKind.ByTFM tfm))
                        |> Seq.toList
                    | FrameworkIdentifier.DotNetCoreApp _ ->
                        // for .net core, a console app is .dll

                        // infer by same name of nupkg
                        let isSameNameOfPackage s = Path.GetFileNameWithoutExtension(s).Contains(pkg.Name.Name)
                        // infer by a .deps.json file
                        let hasDepsJson s = File.Exists(Path.ChangeExtension(s, ".deps.json"))

                        Directory.EnumerateFiles(toolsTFMDir , "*.dll")
                        |> Seq.filter (fun s -> (isSameNameOfPackage s) || (hasDepsJson s))
                        |> Seq.map (asTool (RepoToolInNupkgKind.ByTFM tfm))
                        |> Seq.toList
                    | tfm ->
                        if verbose then
                            verbosefn "found tool dir '%s' with known tfm %O but skipping because unsupported as repo tool" toolsTFMDir tfm
                        []
        ]

module WrapperToolGeneration =

    open System.Collections.Generic
    open Paket.Logging

    let private saveScript render (rootPath:DirectoryInfo) partialPath =
        if not rootPath.Exists then rootPath.Create()
        let scriptFile = FileInfo (Path.Combine(rootPath.FullName, partialPath))
        if verbose then
            verbosefn "generating wrapper script - %s" scriptFile.FullName
        if not scriptFile.Directory.Exists then scriptFile.Directory.Create()            
            
        let existingFileContents =
            if scriptFile.Exists then
                try
                    File.ReadAllText scriptFile.FullName
                with
                | exn -> failwithf "Could not read load script file %s. Message: %s" scriptFile.FullName exn.Message
            else
                ""

        let text = render rootPath
        try
            if existingFileContents <> text then
                File.WriteAllText (scriptFile.FullName, text)
        with
        | exn -> failwithf "Could not write load script file %s. Message: %s" scriptFile.FullName exn.Message

        scriptFile

    type [<RequireQualifiedAccess>]  ScriptContentRuntimeHost = DotNetFramework | DotNetCoreApp | Native

    type HelperScriptWindows = {
        PartialPath : string
        Direct: bool
    } with
        member self.Render (_directory:DirectoryInfo) =
            let cmdContent =
                [ "@ECHO OFF"
                  ""
                  """IF "%1" == "" ("""
                  sprintf "    ECHO Usage: %s ^<enable^|disable^>" Constants.PaketRepotoolsHelperName
                  "    EXIT /B 0"
                  ")"
                  ""
                  """IF "%1" == "enable" ("""
                  """    SET "PATH=%~dp0;%PATH%" """
                  "    EXIT /B 0"
                  """) ELSE IF "%1" == "disable" ("""
                  "    ECHO disable not yet implemented."
                  "    ECHO Please modify the PATH env var manually to remove the '%~dp0' dir"
                  "    EXIT /B 0"
                  ")"
                  ""
                  "EXIT /B 1"
                  "" ]
            
            cmdContent |> String.concat "\r\n"

        member self.RenderGlobal (_directory:DirectoryInfo) =
            let cmdContent =
                [ "@ECHO OFF"
                  ""
                  """IF "%1" == "" ("""
                  sprintf "    ECHO Usage: %s ^<enable^|disable^>" Constants.PaketRepotoolsHelperName
                  "    EXIT /B 0"
                  ")"
                  ""
                  "SET _PAKET_CMD=e:\github\paket\bin\paket.exe"
                  ""
                  """FOR /f %%i in ('"%_PAKET_CMD%" info --paket-repotools-dir -s 2^> NUL') DO ("""
                  """    IF NOT "%%i" == "" ("""
                  "        ECHO Found directory '%%i'"
                  ""
                  """        ECHO "%%i\repotools" enable"""
                  """        "%%i\repotools" enable"""
                  ""
                  "    ) ELSE ("
                  "        echo Paket repo tools directory not found in directory hierachy"
                  "    )"
                  ")"
                  ""
                  "EXIT /B 1"
                  "" ]
            
            cmdContent |> String.concat "\r\n"

        /// Save the script in '<directory>/paket-files/bin/<script>'
        member self.Save (rootPath:DirectoryInfo) =
            saveScript (if self.Direct then self.Render else self.RenderGlobal) rootPath self.PartialPath

    type HelperScriptPowershell = {
        PartialPath : string
    } with
        member self.Render (_directory:DirectoryInfo) =
            let cmdContent =
                [ ""
                  "$env:PATH = $PSScriptRoot + ';' + $env:PATH"
                  "" ]
            
            cmdContent |> String.concat "\r\n"

        /// Save the script in '<directory>/paket-files/bin/<script>'
        member self.Save (rootPath:DirectoryInfo) =
            saveScript self.Render rootPath self.PartialPath

    type ScriptContentWindows = {
        PartialPath : string
        RelativeToolPath : string
        Runtime: ScriptContentRuntimeHost
        WorkingDirectory: RepoToolDiscovery.RepoToolInNupkgWorkingDirectoryPath
    } with
        member self.Render (_directory:DirectoryInfo) =

            let paketToolRuntimeHostWin =
                match self.Runtime with
                | ScriptContentRuntimeHost.DotNetFramework -> ""
                | ScriptContentRuntimeHost.DotNetCoreApp -> "dotnet "
                | ScriptContentRuntimeHost.Native -> ""

            let cmdContent =
                [ yield "@ECHO OFF"
                  yield ""
                  yield "SETLOCAL"
                  yield ""
                  match self.WorkingDirectory with
                  | RepoToolDiscovery.RepoToolInNupkgWorkingDirectoryPath.CurrentDirectory -> ()
                  | RepoToolDiscovery.RepoToolInNupkgWorkingDirectoryPath.ScriptDirectory ->
                      yield "CD /D %~dp0"
                      yield ""
                  yield sprintf """%s"%%~dp0%s" %%*""" paketToolRuntimeHostWin self.RelativeToolPath
                  yield "" ]
            
            cmdContent |> String.concat "\r\n"
        
        /// Save the script in '<directory>/paket-files/bin/<script>'
        member self.Save (rootPath:DirectoryInfo) =
            saveScript self.Render rootPath self.PartialPath
    
    and [<RequireQualifiedAccess>] ScriptContentWindowsRuntime = DotNetFramework | DotNetCoreApp | Mono | Native

    let directChmod (workDir: DirectoryInfo) args =
        let configProcessStartInfoF (psi: Diagnostics.ProcessStartInfo) =
            psi.UseShellExecute <- true
            psi.FileName <- "chmod"
            psi.Arguments <- args
            psi.WorkingDirectory <- workDir.FullName
        let chmodTimeout = TimeSpan.FromSeconds 2.
        let exitCode = Paket.Git.CommandHelper.ExecProcessWithLambdas configProcessStartInfoF chmodTimeout false ignore ignore
        exitCode

    let chmod_plus_x (path: FileInfo) =
        if Utils.isWindows then
            verbosefn "chmod+x of '%s' skipped on windows, execute it manually if needed" path.FullName
        else
            try
                verbosefn "running chmod+x on '%s' ..." path.FullName
                let exitCode = directChmod path.Directory (sprintf """+x "%s" """ path.FullName)
                if exitCode = 0 then
                    verbosefn "chmod+x on '%s' was successful (exit code %i)." path.FullName exitCode
                else
                    verbosefn "chmod+x on '%s' failed with code %i. Execute it manually" path.FullName exitCode
            with e ->
                verbosefn "Running chmod+x on '%s' failed with an exception. Execute it manually" path.FullName
                printError e

    type HelperScriptShell = {
        PartialPath : string
    } with
        member self.Render (_directory:DirectoryInfo) =

            let cmdContent =
                [ "#!/bin/sh"
                  ""
                  """_script="$(readlink -f ${BASH_SOURCE[0]})" """
                  """_base="$(dirname $_script)" """
                  ""
                  """export PATH="$_base:$PATH" """
                  "" ]
            
            cmdContent |> String.concat "\n"
        
        /// Save the script in '<directory>/paket-files/bin/<script>'
        member self.Save (rootPath:DirectoryInfo) =
            saveScript self.Render rootPath self.PartialPath

    type ScriptContentShell = {
        PartialPath : string
        RelativeToolPath : string
        Runtime: ScriptContentRuntimeHost
        WorkingDirectory: RepoToolDiscovery.RepoToolInNupkgWorkingDirectoryPath
    } with
        member self.Render (_directory:DirectoryInfo) =
            let paketToolRuntimeHostLinux =
                match self.Runtime with
                | ScriptContentRuntimeHost.DotNetFramework -> "mono "
                | ScriptContentRuntimeHost.DotNetCoreApp -> "dotnet "
                | ScriptContentRuntimeHost.Native -> ""
            
            let runCmd = sprintf """%s"$(dirname "$0")/%s" "$@" """ paketToolRuntimeHostLinux (self.RelativeToolPath.Replace('\\','/'))

            let cmdContent =
                [ yield "#!/bin/sh"
                  yield ""
                  match self.WorkingDirectory with
                  | RepoToolDiscovery.RepoToolInNupkgWorkingDirectoryPath.CurrentDirectory ->
                      yield runCmd
                  | RepoToolDiscovery.RepoToolInNupkgWorkingDirectoryPath.ScriptDirectory ->
                      yield sprintf """(cd "$(dirname "$0")"; %s)""" runCmd
                  yield "" ]
            
            cmdContent |> String.concat "\n"
        
        /// Save the script in '<directory>/paket-files/bin/<script>'
        member self.Save (rootPath:DirectoryInfo) =
            saveScript self.Render rootPath self.PartialPath

    type [<RequireQualifiedAccess>] ToolWrapper =
        | Windows of ScriptContentWindows
        | Shell of ScriptContentShell
    
    type [<RequireQualifiedAccess>] HelperScript =
        | Windows of HelperScriptWindows
        | Shell of HelperScriptShell
        | Powershell of HelperScriptPowershell
     
    let constructWrapperScriptsFromData (depCache:DependencyCache) (groups: (LockFileGroup * Map<PackageName,PackageResolver.ResolvedPackage>) list) =
        let lockFile = depCache.LockFile
        
        if verbose then
            verbosefn "Generating wrapper scripts for the following groups: %A" (groups |> List.map (fun (g,_) -> g.Name.ToString()))
            verbosefn " - using Paket lock file: %s" lockFile.FileName
        
        //depCache.GetOrderedPackageReferences

        let allRepoToolPkgs =

            let resolved = lazy (lockFile.GetGroupedResolution())

            [ for (g, pkgs) in groups do
                
                for (pkg, resolvedPkg) in pkgs |> Map.toList do
                    let x = resolved.Force().[ g.Name, pkg ]
                    let y =
                        x.Folder lockFile.RootPath g.Name
                        |> DirectoryInfo
                    if y.Exists then
                        yield (g, x, y)  ]

        let toolWrapperInDir =
            allRepoToolPkgs
            |> List.collect (fun (g, x, y) ->
                RepoToolDiscovery.avaiableTools x y
                |> RepoToolDiscovery.applyPreferencesAboutRuntimeHost (RepoToolDiscovery.getPreferenceFromEnv ())
                |> List.map (fun tool -> g, tool) )
            |> List.map (fun (g, tool) ->
                    let scriptPath =
                        match g.Options.Settings.RepotoolsBinDirectory with
                        | Some path ->
                            Path.Combine(lockFile.RootPath, path) |> Path.GetFullPath
                        | None ->
                            if g.Name = Constants.MainDependencyGroup then
                                Path.Combine(Constants.PaketFilesFolderName,"bin")
                            else
                                Path.Combine(Constants.PaketFilesFolderName, g.Name.Name, "bin")
                    tool, scriptPath)

        let wrapperScripts =
            toolWrapperInDir
            |> List.collect (fun (tool, scriptPath) ->
                let relativePath = createRelativePath (Path.Combine(lockFile.RootPath, scriptPath, tool.Name)) (tool.FullPath)

                let runtimeOpt =
                    match tool.Kind with
                    | RepoToolDiscovery.RepoToolInNupkgKind.OldStyle ->
                        Some ScriptContentRuntimeHost.DotNetFramework
                    | RepoToolDiscovery.RepoToolInNupkgKind.ByTFM tfm ->
                        match tfm with
                        | FrameworkIdentifier.DotNetFramework _ ->
                            Some ScriptContentRuntimeHost.DotNetFramework
                        | FrameworkIdentifier.DotNetCoreApp _ ->
                            Some ScriptContentRuntimeHost.DotNetCoreApp
                        | _ ->
                            None

                match runtimeOpt with
                | None -> []
                | Some runtime ->
                  [ { ScriptContentWindows.PartialPath = Path.Combine(scriptPath, (sprintf "%s.cmd" tool.Name))
                      Runtime = runtime
                      WorkingDirectory = tool.WorkingDirectory
                      RelativeToolPath = relativePath } |> ToolWrapper.Windows
                
                    { ScriptContentShell.PartialPath = Path.Combine(scriptPath, tool.Name)
                      Runtime = runtime
                      WorkingDirectory = tool.WorkingDirectory
                      RelativeToolPath = relativePath } |> ToolWrapper.Shell ] )

        let isGlobalToolInstall =
            toolWrapperInDir
            |> List.map fst
            |> List.exists (fun tool -> tool.Name = Constants.PaketGlobalExeName)

        let repoHelperScripts =
            toolWrapperInDir
            |> List.map snd
            |> List.distinct
            |> List.collect (fun scriptPath ->
                [ { HelperScriptWindows.PartialPath = Path.Combine(scriptPath, sprintf "%s.cmd" Constants.PaketRepotoolsHelperName)
                    Direct = true }
                  |> HelperScript.Windows
                  
                  { HelperScriptPowershell.PartialPath = Path.Combine(scriptPath, sprintf "%s.ps1" Constants.PaketRepotoolsHelperName) }
                  |> HelperScript.Powershell

                  { HelperScriptShell.PartialPath = Path.Combine(scriptPath, sprintf "%s.sh" Constants.PaketRepotoolsHelperName) }
                  |> HelperScript.Shell ] )

        let globalHelperScripts =
            toolWrapperInDir
            |> List.map snd
            |> List.distinct
            |> List.collect (fun scriptPath ->
                [ { HelperScriptWindows.PartialPath = Path.Combine(scriptPath, sprintf "%s.cmd" Constants.PaketRepotoolsHelperName)
                    Direct = false }
                  |> HelperScript.Windows ] )
        
        wrapperScripts, (if isGlobalToolInstall then globalHelperScripts else repoHelperScripts)

    let saveTool dir tool =
        match tool with
        | ToolWrapper.Windows cmd -> cmd.Save dir
        | ToolWrapper.Shell sh ->
            let scriptPath = sh.Save dir
            chmod_plus_x scriptPath
            scriptPath

    let saveHelper dir helper =
        match helper with
        | HelperScript.Windows cmd -> cmd.Save dir
        | HelperScript.Powershell ps1 -> ps1.Save dir
        | HelperScript.Shell sh ->
            let scriptPath = sh.Save dir
            chmod_plus_x scriptPath
            scriptPath
