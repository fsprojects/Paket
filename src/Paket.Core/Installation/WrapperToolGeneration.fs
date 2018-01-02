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

    type [<RequireQualifiedAccess>]  ScriptContentRuntimeHost = DotNetFramework | DotNetCoreApp | Native

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
            saveScript self.Render rootPath self.PartialPath

    type [<RequireQualifiedAccess>] ScriptContent =
        | Windows of ScriptContentWindows
        | Shell of ScriptContentShell

    type RepoToolInNupkg =
        { FullPath: string
          Name: string
          Kind: RepoToolInNupkgKind }
    and [<RequireQualifiedAccess>] RepoToolInNupkgKind =
        | OldStyle
        | ByTFM of tfm:FrameworkIdentifier

    let avaiableTools (pkg: PackageResolver.PackageInfo) (pkgDir: DirectoryInfo) =
        let toolsDir = pkgDir.FullName </> "tools"

        let asTool kind path =
            { RepoToolInNupkg.FullPath = path
              Name = Path.GetFileNameWithoutExtension(path)
              Kind = kind }

        let toolsTFMDirs =
            Directory.EnumerateDirectories(toolsDir, "*", SearchOption.TopDirectoryOnly)
            |> Seq.choose (fun x ->
                match FrameworkDetection.Extract x with
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

        allRepoToolPkgs
        |> List.collect (fun (g, x, y) -> avaiableTools x y |> List.map (fun tool -> g, tool))
        |> List.collect (fun (g, tool) ->
            let dir =
                if g.Name = Constants.MainDependencyGroup then
                    "bin"
                else
                    g.Name.Name </> "bin"

            let scriptPath = Constants.PaketFilesFolderName </> dir
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

