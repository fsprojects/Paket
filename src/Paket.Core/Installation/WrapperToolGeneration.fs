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

                        // infer by a .deps.json file
                        let hasDepsJson s = File.Exists(Path.ChangeExtension(s, ".deps.json"))

                        Directory.EnumerateFiles(toolsTFMDir , "*.dll")
                        |> Seq.filter hasDepsJson
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

    type [<RequireQualifiedAccess>]  ScriptContentRuntimeHost = DotNetFramework | DotNetCoreApp | Native

    type HelperScriptWindows = {
        PartialPath : string
        Direct: bool
    } with
        member __.Render () =
            let cmdContent =
                [ """@ECHO OFF                                         """
                  """                                                  """
                  """IF "%1" == "" (                                   """
                  """    CALL :SHOW_HELP                               """
                  """    EXIT /B 1                                     """
                  """)                                                 """
                  """                                                  """
                  """IF "%1" == "--help" (                             """
                  """    CALL :SHOW_HELP                               """
                  """    EXIT /B 0                                     """
                  """)                                                 """
                  """                                                  """
                  """IF "%1" == "enable" (                             """
                  """    GOTO :ENABLE_REPOTOOLS                        """
                  """)                                                 """
                  """                                                  """
                  """IF "%1" == "e" (                                  """
                  """    GOTO :ENABLE_REPOTOOLS                        """
                  """)                                                 """
                  """                                                  """
                  """IF "%1" == "disable" (                            """
                  """    GOTO :DISABLE_REPOTOOLS                       """
                  """)                                                 """
                  """                                                  """
                  """IF "%1" == "d" (                                  """
                  """    GOTO :DISABLE_REPOTOOLS                       """
                  """)                                                 """
                  """                                                  """
                  """CALL :SHOW_HELP                                   """
                  """EXIT /B 1                                         """
                  """                                                  """
                  """:SHOW_HELP                                        """
                  """ECHO Paket repo tools helper                      """
                  sprintf """ECHO Usage: %s [command] ^<options^>      """ Constants.PaketRepotoolsHelperName
                  """ECHO.                                             """
                  """ECHO COMMANDS:                                    """
                  """ECHO.                                             """
                  """ECHO. enable  (alias e)        enable repo tools  """
                  """ECHO. disable (alias d)        disable repo tools """
                  """ECHO.                                             """
                  """ECHO OPTIONS:                                     """
                  """ECHO.                                             """
                  """ECHO.   --help                 display this help. """
                  """ECHO.                                             """
                  """                                                  """
                  """GOTO :EOF                                         """
                  """                                                  """
                  """:ENABLE_REPOTOOLS                                 """
                  """ECHO Adding '%~dp0' dir to PATH env var...        """
                  """SET "PATH=%~dp0;%PATH%"                           """
                  """ECHO Done.                                        """
                  """EXIT /B 0                                         """
                  """                                                  """
                  """:DISABLE_REPOTOOLS                                """
                  """ECHO Removing '%~dp0' dir from PATH env var...    """
                  """CALL SET PATH=%%PATH:%~dp0;=%%                    """
                  """ECHO Done.                                        """
                  """EXIT /B 0                                         """
                  "" ]
            
            cmdContent |> List.map (fun s -> s.TrimEnd()) |> String.concat "\r\n"

        member __.RenderGlobal () =
            let cmdContent =
                [ """@ECHO OFF                                                                    """
                  """                                                                             """
                  """IF "%1" == "" (                                                              """
                  sprintf """    CALL %s --help                                                   """ Constants.PaketRepotoolsHelperName
                  """    EXIT /B 1                                                                """
                  """)                                                                            """
                  """                                                                             """
                  """FOR /f %%i in ('"%~dp0\paketg" info --paket-repotools-dir -s 2^> NUL') DO (  """
                  """    IF NOT "%%i" == "" (                                                     """
                  """        ECHO Found directory '%%i'                                           """
                  """                                                                             """
                  sprintf """        ECHO "%%%%i\%s" %%*                                          """ Constants.PaketRepotoolsHelperName
                  sprintf """        "%%%%i\%s" %%*                                               """ Constants.PaketRepotoolsHelperName
                  """                                                                             """
                  """    ) ELSE (                                                                 """
                  """        GOTO REPOTOOLS_DIR_NOT_FOUND                                         """
                  """    )                                                                        """
                  """)                                                                            """
                  """                                                                             """
                  """:REPOTOOLS_DIR_NOT_FOUND                                                     """
                  """echo Paket repo tools directory not found in directory hierachy              """
                  """EXIT /B 1                                                                    """
                  """                                                                             """ 
                  "" ]
            
            cmdContent |> List.map (fun s -> s.TrimEnd()) |> String.concat "\r\n"

        member self.Save (rootPath:DirectoryInfo) =
            let scriptFile = Path.Combine(rootPath.FullName, self.PartialPath) |> FileInfo
            scriptFile, (if self.Direct then self.Render () else self.RenderGlobal ())

    type ScriptContentWindows = {
        PartialPath : string
        ToolPath : string
        Runtime: ScriptContentRuntimeHost
        WorkingDirectory: RepoToolDiscovery.RepoToolInNupkgWorkingDirectoryPath
    } with
        member self.Render () =

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
                  yield sprintf """%s"%s" %%*""" paketToolRuntimeHostWin self.ToolPath
                  yield "" ]
            
            cmdContent |> String.concat "\r\n"
        
        member self.Save (rootPath:DirectoryInfo) =
            let scriptFile = Path.Combine(rootPath.FullName, self.PartialPath) |> FileInfo
            scriptFile, self.Render ()
    
    and [<RequireQualifiedAccess>] ScriptContentWindowsRuntime = DotNetFramework | DotNetCoreApp | Mono | Native

    type HelperScriptShell = {
        PartialPath : string
        Direct: bool
    } with
        member __.Render () =

            let cmdContent =
                [ """#!/bin/bash                                                           """
                  """#                                                                     """
                  """# Output the command to update PATH to enable/disable repo tools      """
                  """#                                                                     """
                  """                                                                      """
                  """write_out () {                                                        """
                  """  if [ -t 1 ] ; then                                                  """
                  """    # stdout is a terminal                                            """
                  """    echo "$1"                                                         """
                  """  else                                                                """
                  """    # stdout isn't a terminal                                         """
                  """    echo echo \'"$1"\'                                                """
                  """  fi                                                                  """
                  """}                                                                     """
                  """                                                                      """
                  """show_help () {                                                        """
                  """  write_out 'Paket repo tools helper'                                 """
                  sprintf """  write_out 'Usage: %s [command] <options>'                   """ Constants.PaketRepotoolsShellHelperName
                  """  write_out ''                                                        """
                  """  write_out 'COMMANDS:'                                               """
                  """  write_out ''                                                        """
                  """  write_out 'enable  (alias e)        enable repo tools'              """
                  """  write_out 'disable (alias d)        disable repo tools'             """
                  """  write_out ''                                                        """
                  """  write_out 'OPTIONS:'                                                """
                  """  write_out ''                                                        """
                  """  write_out ' --help                 display this help.'              """
                  """  write_out ''                                                        """
                  """}                                                                     """
                  """                                                                      """
                  """enable_repotools () {                                                 """
                  """    echo echo \'"Adding $1 dir to PATH env var..."\'                  """
                  """    echo export PATH=\""$1:\$PATH"\"                                  """
                  """    echo echo \'"Done."\'                                             """
                  """}                                                                     """
                  """                                                                      """
                  """disable_repotools () {                                                """
                  """    echo echo \'"Removing $1 dir from PATH env var..."\'              """
                  """    echo export PATH=\'"${PATH//"$1:"/}"\'                            """
                  """    echo echo \'"Done."\'                                             """
                  """}                                                                     """
                  """                                                                      """
                  """main () {                                                             """
                  """                                                                      """
                  """  local _script                                                       """
                  """  _script="$(readlink -f "$0")"                                       """
                  """  local _base                                                         """
                  """  _base="$(dirname "$_script")"                                       """
                  """                                                                      """
                  """  if [[ "$1" = '' ]]; then                                            """
                  """    show_help                                                         """
                  """  elif [[ "$1" = '--help' ]]; then                                    """
                  """    show_help                                                         """
                  """  elif [[ "$1" = 'enable' ]]; then                                    """
                  """    enable_repotools "$_base"                                         """
                  """  elif [[ "$1" = 'e' ]]; then                                         """
                  """    enable_repotools "$_base"                                         """
                  """  elif [[ "$1" = 'disable' ]]; then                                   """
                  """    disable_repotools "$_base"                                        """
                  """  elif [[ "$1" = 'd' ]]; then                                         """
                  """    disable_repotools "$_base"                                        """
                  """  fi                                                                  """
                  """}                                                                     """
                  """                                                                      """
                  """main "$@"                                                             """
                  "" ]
            
            cmdContent |> List.map (fun s -> s.TrimEnd()) |> String.concat "\n"
        
        member __.RenderGlobal () =
            let cmdContent =
                [ """#!/bin/bash                                                                               """
                  """#                                                                                         """
                  """# Helper to find paket repo tools in directory hierarchy                                  """
                  """#                                                                                         """
                  """                                                                                          """
                  """write_out () {                                                                            """
                  """  if [ -t 1 ] ; then                                                                      """
                  """    # stdout is a terminal                                                                """
                  """    echo "$1"                                                                             """
                  """  else                                                                                    """
                  """    # stdout isn't a terminal                                                             """
                  """    echo echo \'"$1"\'                                                                    """
                  """  fi                                                                                      """
                  """}                                                                                         """
                  """                                                                                          """
                  """find_repotools_dir () {                                                                   """
                  """                                                                                          """
                  """  local repotools_dir                                                                     """
                  """  repotools_dir="$(mono /mnt/e/github/Paket/bin/paket.exe info --paket-repotools-dir -s)" """
                  """                                                                                          """
                  """  if [[ $? -eq 0 ]] && [[ -d "$repotools_dir" ]]; then                                    """
                  """    echo echo \'"Found directory $repotools_dir "\'                                       """
                  sprintf """    echo echo \'""$repotools_dir/%s" $@"\'                                        """ Constants.PaketRepotoolsShellHelperName
                  sprintf """    "$repotools_dir/%s" $@                                                        """ Constants.PaketRepotoolsShellHelperName
                  """  else                                                                                    """
                  """    echo echo \'"Paket repo tools directory not found in directory hierachy"\'            """
                  """  fi                                                                                      """
                  """}                                                                                         """
                  """                                                                                          """
                  """main () {                                                                                 """
                  """                                                                                          """
                  """  local _script                                                                           """
                  """  _script="$(readlink -f "$0")"                                                           """
                  """  local _base                                                                             """
                  """  _base="$(dirname "$_script")"                                                           """
                  """                                                                                          """
                  """  if [[ "$1" = '' ]]; then                                                                """
                  """    echo echo not implemented yet                                                         """
                  """  else                                                                                    """
                  """    find_repotools_dir "$@"                                                               """
                  """  fi                                                                                      """
                  """}                                                                                         """
                  """                                                                                          """
                  """main "$@"                                                                                 """
                  "" ]
            
            cmdContent |> List.map (fun s -> s.TrimEnd()) |> String.concat "\n"


        /// Save the script in '<directory>/paket-files/bin/<script>'
        member self.Save (rootPath:DirectoryInfo) =
            let scriptFile = Path.Combine(rootPath.FullName, self.PartialPath) |> FileInfo
            scriptFile, (if self.Direct then self.Render () else self.RenderGlobal ())


    type HelperFunctionScriptShell = {
        PartialPath : string
    } with
        member __.Render () =

            let cmdContent =
                [ """#                                                    """
                  """# Paket Helper functions                             """
                  """#                                                    """
                  """                                                     """
                  """# source repotools in current shell                  """
                  sprintf """%s () {                                      """ Constants.PaketRepotoolsHelperName
                  sprintf """  . <(command %s "$@")                       """ Constants.PaketRepotoolsShellHelperName
                  """}                                                    """
                  "" ]
            
            cmdContent |> List.map (fun s -> s.TrimEnd()) |> String.concat "\n"

        /// Save the script in '<directory>/paket-files/bin/<script>'
        member self.Save (rootPath:DirectoryInfo) =
            let scriptFile = Path.Combine(rootPath.FullName, self.PartialPath) |> FileInfo
            scriptFile, self.Render ()

    type RestoredToolsInfo = {
        PartialPath : string
        InstalledToolsDirectories: (GroupName * DirectoryInfo) list
    } with
        member self.Render () =
            //TODO use CSV lib

            // save as CSV
            self.InstalledToolsDirectories
            |> List.map (fun (g, path) -> sprintf "%s,%s" (g.Name) (path.FullName))
            |> String.concat Environment.NewLine

        /// Save the script in '<directory>/paket-files/bin/<script>'
        member self.Save (rootPath:DirectoryInfo) =
            let scriptFile = Path.Combine(rootPath.FullName, self.PartialPath) |> FileInfo
            scriptFile, self.Render ()

    type ScriptContentShell = {
        PartialPath : string
        ToolPath : string
        Runtime: ScriptContentRuntimeHost
        WorkingDirectory: RepoToolDiscovery.RepoToolInNupkgWorkingDirectoryPath
    } with
        member self.Render () =
            let paketToolRuntimeHostLinux =
                match self.Runtime with
                | ScriptContentRuntimeHost.DotNetFramework -> "mono "
                | ScriptContentRuntimeHost.DotNetCoreApp -> "dotnet "
                | ScriptContentRuntimeHost.Native -> ""
            
            let runCmd = sprintf """%s"%s" "$@" """ paketToolRuntimeHostLinux (self.ToolPath.Replace('\\','/'))

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
            let scriptFile = Path.Combine(rootPath.FullName, self.PartialPath) |> FileInfo
            scriptFile, self.Render ()

    type [<RequireQualifiedAccess>] ToolWrapper =
        | Windows of ScriptContentWindows
        | Shell of ScriptContentShell
    
    type [<RequireQualifiedAccess>] HelperScript =
        | Windows of HelperScriptWindows
        | Shell of HelperScriptShell
        | ShellFunctions of HelperFunctionScriptShell
        | RestoredTools of RestoredToolsInfo

    let constructWrapperScriptsFromData (depCache:DependencyCache) (groups: (LockFileGroup * Map<PackageName,PackageResolver.ResolvedPackage>) list) =
        let lockFile = depCache.LockFile
        
        if verbose then
            verbosefn "Generating wrapper scripts for the following groups: %A" (groups |> List.map (fun (g,_) -> g.Name.ToString()))
            verbosefn " - using Paket lock file: %s" lockFile.FileName
        
        //depCache.GetOrderedPackageReferences

        let binDirectoryForGroup (g: LockFileGroup) =
            match g.Options.Settings.RepotoolsBinDirectory with
            | Some path ->
                Path.Combine(lockFile.RootPath, path) |> Path.GetFullPath
            | None ->
                if g.Name = Constants.MainDependencyGroup then
                    Path.Combine(Constants.PaketFilesFolderName, Constants.PaketRepotoolsDirectoryName)
                else
                    Path.Combine(Constants.PaketFilesFolderName, g.Name.Name, Constants.PaketRepotoolsDirectoryName)

        let toolWrapperInDir =

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

            allRepoToolPkgs
            |> List.collect (fun (g, x, y) ->
                RepoToolDiscovery.avaiableTools x y
                |> RepoToolDiscovery.applyPreferencesAboutRuntimeHost (RepoToolDiscovery.getPreferenceFromEnv ())
                |> List.map (fun tool -> g, tool) )
            |> List.map (fun (g, tool) ->
                let scriptPath = binDirectoryForGroup g
                g, tool, scriptPath)

        let wrapperScripts =
            toolWrapperInDir
            |> List.collect (fun (_g, tool, scriptPath) ->
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
                      ToolPath = tool.FullPath } |> ToolWrapper.Windows
                
                    { ScriptContentShell.PartialPath = Path.Combine(scriptPath, tool.Name)
                      Runtime = runtime
                      WorkingDirectory = tool.WorkingDirectory
                      ToolPath = tool.FullPath } |> ToolWrapper.Shell ] )

        let isGlobalToolInstall =
            toolWrapperInDir
            |> List.exists (fun (_, tool, _) -> tool.Name = Constants.PaketGlobalExeName)

        let repoHelperScripts =
            toolWrapperInDir
            |> List.map (fun (_, _, path) -> path)
            |> List.distinct
            |> List.collect (fun scriptPath ->
                [ { HelperScriptWindows.PartialPath = Path.Combine(scriptPath, sprintf "%s.cmd" Constants.PaketRepotoolsHelperName)
                    Direct = true }
                  |> HelperScript.Windows

                  { HelperScriptShell.PartialPath = Path.Combine(scriptPath, Constants.PaketRepotoolsShellHelperName)
                    Direct = true }
                  |> HelperScript.Shell ] )

        let globalHelperScripts =
            toolWrapperInDir
            |> List.map (fun (_, _, path) -> path)
            |> List.distinct
            |> List.collect (fun scriptPath ->
                [ { HelperScriptWindows.PartialPath = Path.Combine(scriptPath, sprintf "%s.cmd" Constants.PaketRepotoolsHelperName)
                    Direct = false }
                  |> HelperScript.Windows

                  { HelperScriptShell.PartialPath = Path.Combine(scriptPath, Constants.PaketRepotoolsShellHelperName)
                    Direct = false }
                  |> HelperScript.Shell

                  { HelperFunctionScriptShell.PartialPath = Path.Combine(scriptPath, Constants.PaketRepotoolsShellFunctionsHelperName) }
                  |> HelperScript.ShellFunctions ] )

        let paketWrapperScript =
            let mainBinDirOpt =
                toolWrapperInDir
                |> List.tryPick (fun (g,_,_) -> if g.Name = Constants.MainDependencyGroup then Some g else None)
                |> Option.map binDirectoryForGroup

            match mainBinDirOpt with
            | None -> []
            | Some scriptPath ->
                let toolFullPath = Path.Combine(lockFile.RootPath, Constants.PaketFolderName, Constants.PaketFileName)

                if File.Exists toolFullPath then
                    let toolName = Path.GetFileNameWithoutExtension(Constants.PaketFileName)

                    [ { ScriptContentWindows.PartialPath = Path.Combine(scriptPath, (sprintf "%s.cmd" toolName))
                        Runtime = ScriptContentRuntimeHost.DotNetFramework
                        WorkingDirectory = RepoToolDiscovery.RepoToolInNupkgWorkingDirectoryPath.ScriptDirectory
                        ToolPath = toolFullPath } |> ToolWrapper.Windows
                
                      { ScriptContentShell.PartialPath = Path.Combine(scriptPath, toolName)
                        Runtime = ScriptContentRuntimeHost.DotNetFramework
                        WorkingDirectory = RepoToolDiscovery.RepoToolInNupkgWorkingDirectoryPath.ScriptDirectory
                        ToolPath = toolFullPath } |> ToolWrapper.Shell ]
                else
                    []

        let restoredToolsList =
            let path = Path.Combine(lockFile.RootPath, Constants.PaketFilesFolderName, Constants.PaketRepotoolsCsvName)
            let dirs =
                toolWrapperInDir
                |> List.map (fun (g, _, scriptPath) -> (g.Name), scriptPath)
                |> List.distinct
                |> List.map (fun (g, scriptPath) -> g, DirectoryInfo(scriptPath))
                    
            { RestoredToolsInfo.PartialPath = path
              InstalledToolsDirectories = dirs }
            |> HelperScript.RestoredTools
        
        if isGlobalToolInstall then
             wrapperScripts, (restoredToolsList :: globalHelperScripts)
        else
            (wrapperScripts @ paketWrapperScript), (restoredToolsList :: repoHelperScripts)

module WrapperToolInstall =

    open Paket.Logging
    open WrapperToolGeneration

    let private saveScript ((scriptFile: FileInfo), text) =
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

        try
            if existingFileContents <> text then
                File.WriteAllText (scriptFile.FullName, text)
        with
        | exn -> failwithf "Could not write load script file %s. Message: %s" scriptFile.FullName exn.Message

        scriptFile

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

    let saveTool dir tool =
        match tool with
        | ToolWrapper.Windows cmd ->
            cmd.Save dir |> saveScript
        | ToolWrapper.Shell sh ->
            let scriptPath =
                sh.Save dir
                |> saveScript
            chmod_plus_x scriptPath
            scriptPath

    let saveHelper dir helper =
        match helper with
        | HelperScript.RestoredTools restoredTools ->
            restoredTools.Save dir |> saveScript
        | HelperScript.Windows cmd ->
            cmd.Save dir |> saveScript
        | HelperScript.ShellFunctions sh ->
            sh.Save dir |> saveScript
        | HelperScript.Shell sh ->
            let scriptPath =
                sh.Save dir
                |> saveScript
            chmod_plus_x scriptPath
            scriptPath
