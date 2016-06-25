namespace MSBuild.Tasks

open System
open System.IO
open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open Paket
open Paket.Domain
open Paket.Requirements

type CopyRuntimeDependencies() =
    inherit Task()

    let mutable outputPath = ""
    let mutable targetFramework = ""
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

    member this.TargetFramework
        with get() = targetFramework
        and set(v) = targetFramework <- v

    override this.Execute() = 
        let resultCode =
            try
                let currentRuntimes =
                    if isWindows then
                       ["win"; "win7-x86"]
                    elif isMacOS then ["unix"; "osx"]
                    else ["linux"; "debian-x64"; "unix"]

                base.Log.LogMessage(MessageImportance.Normal, "Detected runtimes: {0}", sprintf "%A" currentRuntimes)
                base.Log.LogMessage(MessageImportance.Normal, "Target framework: {0}", targetFramework)
                
                let currentRuntimes = currentRuntimes |> Set.ofList
                let projectFile = FileInfo(if String.IsNullOrWhiteSpace this.ProjectFile then this.BuildEngine.ProjectFileOfTaskNode else this.ProjectFile)
                               
                let packagesToInstall = 
                    projectsWithRuntimeLibs.Split([|';'|],StringSplitOptions.RemoveEmptyEntries) 
                    |> Array.map (fun x -> 
                        match x.Split('#') |> Array.toList with
                        | [name] -> Constants.MainDependencyGroup,PackageName (name.Trim())
                        | [group; name] -> GroupName (group.Trim()),PackageName (name.Trim())
                        | _ -> failwithf "Unknown package %s" x)

                if Array.isEmpty packagesToInstall then true else

                
                let referencesFile = ProjectFile.FindReferencesFile projectFile

                let dependencies = Dependencies.Locate projectFile.FullName
                let lockFile = dependencies.GetLockFile()
                let dependenciesFile = dependencies.GetDependenciesFile()

                let root = Path.GetDirectoryName lockFile.FileName

                let packagesToInstall = 
                    if not <| String.IsNullOrEmpty targetFramework then
                        try
                            let s = targetFramework.Split([|" - "|],StringSplitOptions.None)
                            let restriction =
                                match FrameworkDetection.Extract(s.[0] + s.[1].Replace("v","")) with
                                | None -> SinglePlatform(DotNetFramework FrameworkVersion.V4)
                                | Some x -> SinglePlatform(x)

                            packagesToInstall
                            |> Array.filter (fun (groupName,packageName) ->
                                try
                                    let g = lockFile.Groups.[groupName]
                                    let p = g.Resolution.[packageName]
                                    let restrictions =
                                        filterRestrictions g.Options.Settings.FrameworkRestrictions p.Settings.FrameworkRestrictions 
                                        |> getRestrictionList
                                
                                    isTargetMatchingRestrictions(restrictions,restriction)
                                with
                                | _ -> true)
                        with
                        | _ -> packagesToInstall
                    else 
                        packagesToInstall

                let model = InstallProcess.CreateModel(root, false, dependenciesFile, lockFile, Set.ofSeq packagesToInstall, Map.empty) |> Map.ofArray

                let projectDir = FileInfo(this.BuildEngine.ProjectFileOfTaskNode).Directory

                for group,packageName in packagesToInstall do
                    match model |> Map.tryFind (group,packageName) with
                    | None -> failwithf "Package %O %O was not found in the install model" group packageName
                    | Some (package,projectModel) ->

                        let files =
                            projectModel.ReferenceFileFolders
                            |> List.choose (fun lib -> 
                                match lib with
                                | x when (match x.Targets with | [SinglePlatform(Runtimes(x))] when currentRuntimes |> Set.contains x -> true | _ -> false) -> Some lib.Files
                                | _ -> None)

                        match files with
                        | [] -> base.Log.LogMessage(MessageImportance.Normal, "No runtime dependencies found for {0} {1}.", group, packageName)
                        | files ->
                            base.Log.LogMessage(MessageImportance.Normal, "Installing runtime dependencies for {0} {1}:", group, packageName)
                            for file in files do
                                for reference in file.References do
                                    let sourceFile = FileInfo(reference.Path)
                                    base.Log.LogMessage(MessageImportance.Normal, "    Copying {0} to {1}", sourceFile.Name, this.OutputPath)
                                    let destFile = Path.Combine(this.OutputPath,sourceFile.Name)

                                    File.Copy(sourceFile.FullName,destFile,true)
                true
            with
            | _ as ex -> base.Log.LogErrorFromException(ex, false); false
        resultCode