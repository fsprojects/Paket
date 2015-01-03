namespace Paket

open System.IO

open Paket.Rop
open Paket.Domain

type Environment = {
    RootDirectory : DirectoryInfo
    DependenciesFile : DependenciesFile
    LockFile : LockFile
    Projects : list<ProjectFile * ReferencesFile>
}

type DomainMessage = 
    | DirectoryDoesntExist of DirectoryInfo
    | DependenciesFileNotFoundInDir of DirectoryInfo
    | DependenciesFileParseError of FileInfo
    | LockFileNotFound of DirectoryInfo
    | LockFileParseError of FileInfo
    | ReferencesFileParseError of FileInfo

    | StrictModeDetected2
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
        
        | StrictModeDetected2 -> 
            "Strict mode detected. Command will not be executed."
        | DependencyNotFoundInLockFile(PackageName name) -> 
            sprintf "Dependency %s from %s not found in lock file." name Constants.DependenciesFileName
        | ReferenceNotFoundInLockFile(referencesFile, PackageName name) -> 
            sprintf "Reference %s from %s not found in lock file." name referencesFile.FileName
            

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Environment = 
    
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
                    fail (LockFileNotFound directory)
                else
                    try
                        succeed <| LockFile.LoadFrom(fi.FullName)
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