namespace Paket

open System.IO

open Paket.Rop
open Paket.Domain

type DomainMessage = 
    | DirectoryDoesntExist of DirectoryInfo
    | DependenciesFileNotFoundInDir of DirectoryInfo
    | DependenciesFileParseError of FileInfo
    | LockFileNotFound of DirectoryInfo
    | LockFileParseError of FileInfo
    | ReferencesFileParseError of FileInfo

    | StrictModeDetected
    | DependencyNotFoundInLockFile of PackageName
    | ReferenceNotFoundInLockFile of ReferencesFile * PackageName

    override this.ToString() = 
        match this with
        | DirectoryDoesntExist(di) -> 
            sprintf "Directory %s does not exist." di.FullName
        | DependenciesFileNotFoundInDir(di) -> 
            sprintf "Dependencies file not found in %s." di.FullName
        | DependenciesFileParseError(fi) -> 
            sprintf "Unable to parse %s." fi.FullName
        | LockFileNotFound(di) -> 
            sprintf "Lock file not found in %s. Create lock file by running paket install." di.FullName
        | LockFileParseError(fi) -> 
            sprintf "Unable to parse lock %s." fi.FullName
        | ReferencesFileParseError(fi) -> 
            sprintf "Unable to parse %s" fi.FullName
        
        | StrictModeDetected -> 
            "Strict mode detected. Command not executed."
        | DependencyNotFoundInLockFile(PackageName name) -> 
            sprintf "Dependency %s from %s not found in lock file." name Constants.DependenciesFileName
        | ReferenceNotFoundInLockFile(referencesFile, PackageName name) -> 
            sprintf "Reference %s from %s not found in lock file." name referencesFile.FileName

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

            let projects = 
                ProjectFile.FindAllProjects(directory.FullName) 
                |> Array.choose (fun project -> ProjectFile.FindReferencesFile(FileInfo(project.FileName))
                                                |> Option.map (fun refFile -> project,refFile))
                |> Array.map (fun (project,file) -> 
                    try 
                        succeed <| (project, ReferencesFile.FromFile(file))
                    with _ -> 
                        fail <| ReferencesFileParseError (FileInfo(file)))
                |> collect

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