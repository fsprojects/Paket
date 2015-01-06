namespace Paket

open System.IO

open Paket.Rop
open Paket.Domain

type PaketEnv = {
    RootDirectory : DirectoryInfo
    DependenciesFile : DependenciesFile
    LockFile : option<LockFile>
    Projects : list<ProjectFile * ReferencesFile>
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PaketEnv = 
    
    let create root dependenciesFile lockFile projects = 
        { RootDirectory = root
          DependenciesFile = dependenciesFile
          LockFile = lockFile
          Projects = projects }       

    let fromRootDirectory (directory : DirectoryInfo) = rop {
        if not directory.Exists then 
            return! fail (DirectoryDoesntExist directory)
        else
            let! dependenciesFile = 
                let fi = FileInfo(Path.Combine(directory.FullName, Constants.DependenciesFileName))
                if not fi.Exists then
                    fail (DependenciesFileNotFoundInDir directory)
                else
                    try
                        succeed (DependenciesFile.ReadFromFile(fi.FullName))
                    with _ ->
                        fail (DependenciesFileParseError fi)

            let! lockFile =
                let fi = FileInfo(Path.Combine(directory.FullName, Constants.LockFileName))
                if not fi.Exists then
                    None |> succeed
                else
                    try
                        LockFile.LoadFrom(fi.FullName) |> Some |> succeed
                    with _ ->
                        fail (LockFileParseError fi)

            let! projects = InstallProcess.findAllReferencesFiles(directory.FullName)

            return create directory dependenciesFile lockFile projects
    }

    let locatePaketRootDirectory (directory : DirectoryInfo) = 
        if not directory.Exists then 
            None
        else
            directory
            |> Seq.unfold (function
                | null -> None
                | dir -> Some(Path.Combine(dir.FullName, Constants.DependenciesFileName), dir.Parent))
            |> Seq.tryFind File.Exists
            |> Option.map (fun f -> DirectoryInfo(Path.GetDirectoryName(f)))


    let ensureNotExists (directory : DirectoryInfo) =
        match fromRootDirectory directory with
        | Success(_) -> fail (PaketEnvAlreadyExistsInDirectory directory)
        | Failure(msgs) -> 
            let filtered = 
                msgs
                |> List.filter (function
                    | DependenciesFileNotFoundInDir _ -> false
                    | _ -> true )
            if filtered |> List.isEmpty then succeed directory
            else Failure(filtered)

    let ensureNotInStrictMode environment =
        if not environment.DependenciesFile.Options.Strict then succeed environment
        else fail StrictModeDetected

    let ensureLockFileExists environment =
        environment.LockFile
        |> failIfNone (LockFileNotFound environment.RootDirectory)

    let init (directory : DirectoryInfo) =
        match locatePaketRootDirectory directory with
        | Some rootDirectory -> 
            fromRootDirectory rootDirectory
            |> successTee (fun (env,_) -> Logging.tracefn "Paket is already initialized in %s" env.RootDirectory.FullName)
        | None -> 
            create 
                directory 
                (DependenciesFile(Path.Combine(directory.FullName, Constants.DependenciesFileName), InstallOptions.Default, [], [], []))
                None
                []
            |> succeed

    let save env =
        let overwrite (currentEnv,_) = 
            if currentEnv.DependenciesFile.ToString() <> env.DependenciesFile.ToString() 
            then env.DependenciesFile.Save()
            
            match currentEnv.LockFile, env.LockFile with
            | Some currentLockFile, Some lockFile ->
                if currentLockFile.ToString() <> lockFile.ToString() 
                then lockFile.Save()
            | Some currentLockFile, None -> 
                File.Delete currentLockFile.FileName
            | None, Some lockFile ->
                lockFile.Save()
            | _ ->
                ()

            // TODO: save Projects and their References

        let justSave _ =
            env.DependenciesFile.Save()
            env.LockFile |> Option.iter (fun l -> l.Save())
            env.Projects |> List.iter (fun (project,references) -> project.Save(); references.Save())

        fromRootDirectory env.RootDirectory
        |> either overwrite justSave
