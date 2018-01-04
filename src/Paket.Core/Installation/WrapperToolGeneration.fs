module Paket.RepoTools

open System
open System.IO
open Paket
open Paket.Domain

module WrapperToolGeneration =

    type PathCombine = PathCombine with
        static member (?<-) (_,dir:DirectoryInfo,framework:FrameworkIdentifier) =  Path.Combine (dir.Name, string framework)
        static member (?<-) (_,dir:DirectoryInfo,group:GroupName) =  Path.Combine (dir.Name, string group)
        static member (?<-) (_,dir:DirectoryInfo,path:string) =  Path.Combine(dir.Name,path)
        static member (?<-) (_,dir:DirectoryInfo,file:FileInfo) =  Path.Combine(dir.Name,file.Name)
        static member (?<-) (_,path:string,framework:FrameworkIdentifier) = Path.Combine(path, string framework)
        static member (?<-) (_,path:string,group:GroupName) =  Path.Combine (path, string group)
        static member (?<-) (_,path1:string,path2:string) =  Path.Combine (path1, path2)
        static member (?<-) (_,path:string,file:FileInfo) =  Path.Combine (path, file.Name)
        static member (?<-) (_,path:string,package:PackageName) =  Path.Combine (path, string package)
        static member (?<-) (_,path:string,dir:DirectoryInfo) =  Path.Combine (path, dir.Name)

    let inline (</>) p1 p2 = (?<-) PathCombine p1 p2

    open System.Collections.Generic
    open Paket.Logging

    let private saveScript render (rootPath:DirectoryInfo) partialPath =
        if not rootPath.Exists then rootPath.Create()
        let scriptFile = FileInfo (rootPath.FullName </> partialPath)
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

    type ScriptAddToPATHWindows = {
        PartialPath : string
    } with
        member self.Render (_directory:DirectoryInfo) =
            let cmdContent =
                [ "@ECHO OFF"
                  ""
                  """set PATH=%~dp0;%PATH%"""
                  "" ]
            
            cmdContent |> String.concat "\r\n"

        /// Save the script in '<directory>/paket-files/bin/<script>'
        member self.Save (rootPath:DirectoryInfo) =
            saveScript self.Render rootPath self.PartialPath

    type ScriptContentWindows = {
        PartialPath : string
        RelativeToolPath : string
        Runtime: ScriptContentRuntimeHost
    } with
        member self.Render (_directory:DirectoryInfo) =

            let paketToolRuntimeHostWin =
                match self.Runtime with
                | ScriptContentRuntimeHost.DotNetFramework -> ""
                | ScriptContentRuntimeHost.DotNetCoreApp -> "dotnet "
                | ScriptContentRuntimeHost.Native -> ""

            let cmdContent =
                [ "@ECHO OFF"
                  ""
                  sprintf """%s"%%~dp0%s" %%*""" paketToolRuntimeHostWin self.RelativeToolPath
                  "" ]
            
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

    type ScriptAddToPATHShell = {
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
            let scriptPath = saveScript self.Render rootPath self.PartialPath
            chmod_plus_x scriptPath
            scriptPath

    type ScriptContentShell = {
        PartialPath : string
        RelativeToolPath : string
        Runtime: ScriptContentRuntimeHost
    } with
        member self.Render (_directory:DirectoryInfo) =
            let paketToolRuntimeHostLinux =
                match self.Runtime with
                | ScriptContentRuntimeHost.DotNetFramework -> "mono "
                | ScriptContentRuntimeHost.DotNetCoreApp -> "dotnet "
                | ScriptContentRuntimeHost.Native -> ""

            let cmdContent =
                [ "#!/bin/sh"
                  ""
                  sprintf """%s"$(dirname "$0")/%s" "$@" """ paketToolRuntimeHostLinux (self.RelativeToolPath.Replace('\\','/'))
                  "" ]
            
            cmdContent |> String.concat "\n"
        
        /// Save the script in '<directory>/paket-files/bin/<script>'
        member self.Save (rootPath:DirectoryInfo) =
            let scriptPath = saveScript self.Render rootPath self.PartialPath
            chmod_plus_x scriptPath
            scriptPath

    type [<RequireQualifiedAccess>] ScriptContent =
        | Windows of ScriptContentWindows
        | Shell of ScriptContentShell
        | WindowsAddToPATH of ScriptAddToPATHWindows
        | ShellAddToPATH of ScriptAddToPATHShell

    type RepoToolInNupkg =
        { FullPath: string
          Name: string
          Kind: RepoToolInNupkgKind }
    and [<RequireQualifiedAccess>] RepoToolInNupkgKind =
        | OldStyle
        | ByTFM of tfm:FrameworkIdentifier

    let applyPreferencesAboutRuntimeHost (pkgs: RepoToolInNupkg list) =

        let preference =
            match System.Environment.GetEnvironmentVariable("PAKET_REPOTOOL_PREFERRED_RUNTIME") |> Option.ofObj |> Option.bind FrameworkDetection.Extract with
            | None -> FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4_5)
            | Some fw -> fw

        pkgs
        |> List.groupBy (fun p -> p.Name)
        |> List.collect (fun (name, tools) ->
            let byPreference tool =
                match tool.Kind, preference with
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
                    verbosefn "- choosen %A based on preference %A" x preference
                    for d in xs do
                        verbosefn "- avaiable but ignored: %A" d
                [x] )

    let avaiableTools (pkg: PackageResolver.PackageInfo) (pkgDir: DirectoryInfo) =
        let toolsDir = pkgDir.FullName </> "tools"

        let asTool kind path =
            { RepoToolInNupkg.FullPath = path
              Name = Path.GetFileNameWithoutExtension(path)
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
                        // TODO configuration to specify the alias in the nupkg
                        // infer by same name of nupkg
                        Directory.EnumerateFiles(toolsTFMDir , "*.dll")
                        |> Seq.filter (fun s -> Path.GetFileNameWithoutExtension(s).Contains(pkg.Name.Name))
                        |> Seq.map (asTool (RepoToolInNupkgKind.ByTFM tfm))
                        |> Seq.toList
                    | tfm ->
                        if verbose then
                            verbosefn "found tool dir '%s' with known tfm %O but skipping because unsupported as repo tool" toolsTFMDir tfm
                        []
        ]
     
    let constructWrapperScriptsFromData (depCache:DependencyCache) (groups: (LockFileGroup * Map<PackageName,PackageResolver.ResolvedPackage>) list) =
        let lockFile = depCache.LockFile
        let frameworksForDependencyGroups = lockFile.ResolveFrameworksForScriptGeneration()
        let environmentFramework = FrameworkDetection.resolveEnvironmentFramework
        
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
                avaiableTools x y
                |> applyPreferencesAboutRuntimeHost
                |> List.map (fun tool -> g, tool) )
            |> List.map (fun (g, tool) ->
                    let dir =
                        if g.Name = Constants.MainDependencyGroup then
                            "bin"
                        else
                            g.Name.Name </> "bin"

                    let scriptPath = Constants.PaketFilesFolderName </> dir
                    tool, scriptPath)

        let wrapperScripts =
            toolWrapperInDir
            |> List.collect (fun (tool, scriptPath) ->
                let relativePath = createRelativePath (lockFile.RootPath </> scriptPath </> tool.Name) (tool.FullPath)

                let runtimeOpt =
                    match tool.Kind with
                    | RepoToolInNupkgKind.OldStyle ->
                        Some ScriptContentRuntimeHost.DotNetFramework
                    | RepoToolInNupkgKind.ByTFM tfm ->
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
                  [ { ScriptContentWindows.PartialPath = scriptPath </> (sprintf "%s.cmd" tool.Name)
                      Runtime = runtime
                      RelativeToolPath = relativePath } |> ScriptContent.Windows
                
                    { ScriptContentShell.PartialPath = scriptPath</> (tool.Name)
                      Runtime = runtime
                      RelativeToolPath = relativePath } |> ScriptContent.Shell ] )

        let installToPathScripts =
            toolWrapperInDir
            |> List.map snd
            |> List.distinct
            |> List.collect (fun scriptPath ->
                [ { ScriptAddToPATHWindows.PartialPath = scriptPath </> "add_to_PATH.cmd" }
                  |> ScriptContent.WindowsAddToPATH

                  { ScriptAddToPATHShell.PartialPath = scriptPath </> "add_to_PATH.sh" }
                  |> ScriptContent.ShellAddToPATH ] )
        
        [ yield! wrapperScripts
          yield! installToPathScripts ]
