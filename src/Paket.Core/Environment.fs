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

    let fromRootDirectory (directory : DirectoryInfo) = 
        if not directory.Exists then 
            fail (DirectoryDoesntExist directory)
        else
            let dependenciesFile = 
                let fi = FileInfo(Path.Combine(directory.FullName, Constants.DependenciesFileName))
                if not fi.Exists then
                    fail (DependenciesFileNotFoundInDir directory)
                else
                    try
                        succeed (DependenciesFile.ReadFromFile(fi.FullName))
                    with _ ->
                        fail (DependenciesFileParseError fi)

            let lockFile =
                let fi = FileInfo(Path.Combine(directory.FullName, Constants.LockFileName))
                if not fi.Exists then
                    None |> succeed
                else
                    try
                        LockFile.LoadFrom(fi.FullName) |> Some |> succeed
                    with _ ->
                        fail (LockFileParseError fi)

            let projects = InstallProcess.findAllReferencesFiles(directory.FullName)

            create directory
            <!> dependenciesFile
            <*> lockFile
            <*> projects

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
