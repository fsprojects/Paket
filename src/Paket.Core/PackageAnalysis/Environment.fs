namespace Paket

open System.IO

open FsToolkit.ErrorHandling
open Paket.Domain
open InstallProcess

type PaketEnv = {
    RootDirectory : DirectoryInfo
    DependenciesFile : DependenciesFile
    LockFile : LockFile option
    Projects : (ProjectFile * ReferencesFile) list
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PaketEnv =
    let create root dependenciesFile lockFile projects =
        { RootDirectory = root
          DependenciesFile = dependenciesFile
          LockFile = lockFile
          Projects = projects }

    let fromRootDirectory (directory : DirectoryInfo) = validation {
        if not directory.Exists then
            return! Error (DirectoryDoesntExist directory)
        else
            let! dependenciesFile =
                let fi = FileInfo(Path.Combine(directory.FullName, Constants.DependenciesFileName))
                if not fi.Exists then
                    Error (DependenciesFileNotFoundInDir directory)
                else
                    try
                        Ok (DependenciesFile.ReadFromFile(fi.FullName))
                    with e ->
                        DependenciesFileParseError(fi,e) |> Error

            let! lockFile =
                let fi = FileInfo(Path.Combine(directory.FullName, Constants.LockFileName))
                if not fi.Exists then
                    None |> Ok
                else
                    try
                        LockFile.LoadFrom(fi.FullName) |> Some |> Ok
                    with _ ->
                        Error (LockFileParseError fi)

            let! projects = RestoreProcess.findAllReferencesFiles(directory.FullName)

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
            |> Option.map (Path.GetDirectoryName >> DirectoryInfo)

    let ensureNotExists (directory : DirectoryInfo) =
        match fromRootDirectory directory with
        | Ok _ -> Error [PaketEnvAlreadyExistsInDirectory directory]
        | Error errors ->
            let filtered =
                errors
                |> List.filter (function
                    | DependenciesFileNotFoundInDir _ -> false
                    | _ -> true )
            if filtered |> List.isEmpty then Ok directory
            else Error filtered

    let ensureNotInStrictMode environment =
        if not environment.DependenciesFile.Groups.[Constants.MainDependencyGroup].Options.Strict then Ok environment
        else Error StrictModeDetected

    let ensureLockFileExists environment =
        environment.LockFile
        |> Result.requireSome (LockFileNotFound environment.RootDirectory)

    let initWithContent sources additional (directory : DirectoryInfo) =
        match locatePaketRootDirectory directory with
        | Some rootDirectory when rootDirectory.FullName = directory.FullName ->
            Logging.tracefn "Paket is already initialized in %s" rootDirectory.FullName
            Ok ()
        | _ ->
            let sourcesSerialized =
                (sources
                |> List.map (string >> DependenciesFileSerializer.sourceString))
                @ [""]
                |> Array.ofList

            let serialized = Array.append sourcesSerialized (additional |> Array.ofList)

            let mainGroup =
                { Name = Constants.MainDependencyGroup
                  Options = InstallOptions.Default
                  Sources = sources
                  Caches = []
                  ExternalLocks = []
                  Packages = []
                  RemoteFiles = [] }
            let groups = [Constants.MainDependencyGroup, mainGroup] |> Map.ofSeq

            let dependenciesFile =
                DependenciesFile(
                    Path.Combine(directory.FullName, Constants.DependenciesFileName),
                    groups,
                    serialized)

            dependenciesFile.ToString()
            |> saveFile dependenciesFile.FileName

    let init (directory : DirectoryInfo) =
        let sources = [PackageSources.DefaultNuGetV3Source]
        let additionalLines = [
            "storage: none"
            ""
        ]
        initWithContent sources additionalLines directory
