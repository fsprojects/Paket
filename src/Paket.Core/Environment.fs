namespace Paket

open System.IO

open Paket.Rop

type Environment = {
    RootDirectory : DirectoryInfo
    DependenciesFile : DependenciesFile
    LockFile : LockFile
    Projects : list<ProjectFile * ReferencesFile>
}

type EnvironmentMessage = 
    | DirectoryDoesntExist of DirectoryInfo
    | DependenciesFileNotFound of DirectoryInfo

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Environment = 
    
    let create root dependenciesFile lockFile projects = 
        { RootDirectory = root
          DependenciesFile = dependenciesFile
          LockFile = lockFile
          Projects = projects }       

    let private locateInDir (directory : DirectoryInfo) = 
        
        let dependenciesFile = 
            Rop.succeed <| DependenciesFile.ReadFromFile(Path.Combine(directory.FullName,Constants.DependenciesFileName))

        let lockFile =
            Rop.succeed <| LockFile.LoadFrom(Path.Combine(directory.FullName,Constants.LockFileName))

        let projects = Rop.succeed []

        create
        <!> succeed directory
        <*> dependenciesFile
        <*> lockFile
        <*> projects

    let locateInThisOrParentDirs (directory : DirectoryInfo) = 
        if directory.Exists |> not then 
            failure (DependenciesFileNotFound directory)
        else
            directory
            |> Seq.unfold (function
                | null -> None
                | dir -> Some(Path.Combine(dir.FullName, Constants.DependenciesFileName), dir.Parent))
            |> Seq.tryFind File.Exists
            |> Option.map (fun f -> DirectoryInfo(Path.GetDirectoryName(f)))
            |> failIfNone (DependenciesFileNotFound directory)
            >>= locateInDir