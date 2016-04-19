namespace MSBuild.Tasks

open System
open System.IO
open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open Paket
open Paket.Domain

[<AutoOpen>]
module PlatformDetection =
    /// Determines if the current system is an Unix system
    let isUnix = Environment.OSVersion.Platform = PlatformID.Unix

    /// Determines if the current system is a MacOs system
    let isMacOS =
        (Environment.OSVersion.Platform = PlatformID.MacOSX) ||
          // Running on OSX with mono, Environment.OSVersion.Platform returns Unix
          // rather than MacOSX, so check for osascript (the AppleScript
          // interpreter). Checking for osascript for other platforms can cause a
          // problem on Windows if the current-directory is on a mapped-drive
          // pointed to a Mac's root partition; e.g., Parallels does this to give
          // Windows virtual machines access to files on the host.
          (Environment.OSVersion.Platform = PlatformID.Unix && (File.Exists "/usr/bin/osascript"))

    /// Determines if the current system is a Linux system
    let isLinux = int System.Environment.OSVersion.Platform |> fun p -> (p = 4) || (p = 6) || (p = 128)

    /// Determines if the current system is a mono system
    /// Todo: Detect mono on windows
    let isMono = isLinux || isUnix || isMacOS

    let isWindows =
        if isMono then false else
          Environment.OSVersion.Platform = PlatformID.Win32NT

    let isWin8 = isWindows && Environment.OSVersion.Version >= new Version(6, 2, 9200, 0)


type CopyRuntimeDependencies() =
    inherit Task()

    let mutable outputPath = ""
    let mutable projectFile = ""
    let mutable projectsWithRuntimeLibs = ""

    [<Required>]
    member this.OutputPath
        with get() = outputPath
        and set(v) = outputPath <- v

    [<Required>]
    member this.ProjectsWithRuntimeLibs
        with get() = projectsWithRuntimeLibs
        and set(v) = projectsWithRuntimeLibs <- v

    member this.ProjectFile
        with get() = outputPath
        and set(v) = outputPath <- v

    override this.Execute() = 
        let resultCode =
            try
                let currentRuntime =
                    if isWindows then
                       "win7-x64"
                    elif isMacOS then "osx"
                    else "debian-x64"

                let currentRuntime = "win7-x64"
                base.Log.LogMessage(MessageImportance.Normal, "Current runtime is {0}", currentRuntime)
                let projectFile = FileInfo(if String.IsNullOrWhiteSpace this.ProjectFile then this.BuildEngine.ProjectFileOfTaskNode else this.ProjectFile)
                
               
                let packagesToInstall = 
                    projectsWithRuntimeLibs.Split([|';'|],StringSplitOptions.RemoveEmptyEntries) 
                    |> Array.map (fun x -> 
                        match x.Split('#') |> Array.toList with
                        | [name] -> Constants.MainDependencyGroup,PackageName (name.Trim())
                        | [group; name] -> GroupName (group.Trim()),PackageName (name.Trim())
                        | _ -> failwithf "Unknown package %s" x)

                if Array.isEmpty packagesToInstall then true else

                let referencesFile = ProjectFile.findReferencesFile projectFile

                let dependencies = Dependencies.Locate projectFile.FullName
                let lockFile = dependencies.GetLockFile()
                let dependenciesFile = dependencies.GetDependenciesFile()

                let root = Path.GetDirectoryName lockFile.FileName
                let model = InstallProcess.CreateModel(root, false, dependenciesFile, lockFile, Set.ofSeq packagesToInstall) |> Map.ofArray
                let projectDir = FileInfo(this.BuildEngine.ProjectFileOfTaskNode).Directory

                for group,packageName in packagesToInstall do
                    match model |> Map.tryFind (group,packageName) with
                    | None -> failwithf "Package %O %O was not found in the install model" group packageName
                    | Some (package,model) ->
                        let files =
                            model.ReferenceFileFolders
                            |> List.choose (fun lib -> 
                                match lib with
                                | x when (match x.Targets with | [SinglePlatform(Runtimes(x))] when x = currentRuntime -> true | _ -> false) -> Some lib.Files
                                | _ -> None)

                        for file in files do
                            for reference in file.References do
                                let sourceFile = FileInfo(reference.Path)
                                base.Log.LogMessage(MessageImportance.Normal, "Copying {0} to {1}", sourceFile, this.OutputPath)
                                let destFile = Path.Combine(this.OutputPath,sourceFile.Name)

                                File.Copy(sourceFile.FullName,destFile)
                true
            with
            | _ as ex -> base.Log.LogErrorFromException(ex, false); false
        resultCode