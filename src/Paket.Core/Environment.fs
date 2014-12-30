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
    | DependenciesFileParseError of FileInfo
    | LockFileNotFound of DirectoryInfo
    | LockFileParseError of FileInfo
    | ReferencesFileParseError of FileInfo

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Environment = 
    
    let create root dependenciesFile lockFile projects = 
        { RootDirectory = root
          DependenciesFile = dependenciesFile
          LockFile = lockFile
          Projects = projects }       

    let private locateInDir (directory : DirectoryInfo) = 
        
        let dependenciesFile = 
            let fi = FileInfo(Path.Combine(directory.FullName, Constants.DependenciesFileName))
            try 
                succeed (DependenciesFile.ReadFromFile(fi.FullName))
            with _ ->
                failure (DependenciesFileParseError fi)

        let lockFile =
            let fi = FileInfo(Path.Combine(directory.FullName, Constants.LockFileName))
            if not fi.Exists then
                failure (LockFileNotFound directory)
            else
                try
                    succeed <| LockFile.LoadFrom(fi.FullName)
                with _ ->
                    failure (LockFileParseError fi)

        let projects = 
            ProjectFile.FindAllProjects(directory.FullName) 
            |> Array.choose (fun project -> ProjectFile.FindReferencesFile(FileInfo(project.FileName))
                                            |> Option.map (fun refFile -> project,refFile))
            |> Array.map (fun (project,file) -> 
                try 
                    succeed <| (project, ReferencesFile.FromFile(file))
                with _ -> 
                    failure <| ReferencesFileParseError (FileInfo(file)))
            |> collect

        create
        <!> succeed directory
        <*> dependenciesFile
        <*> lockFile
        <*> projects

    let locateInThisOrParentDirs (directory : DirectoryInfo) = 
        if not directory.Exists then 
            failure (DirectoryDoesntExist directory)
        else
            directory
            |> Seq.unfold (function
                | null -> None
                | dir -> Some(Path.Combine(dir.FullName, Constants.DependenciesFileName), dir.Parent))
            |> Seq.tryFind File.Exists
            |> Option.map (fun f -> DirectoryInfo(Path.GetDirectoryName(f)))
            |> failIfNone (DependenciesFileNotFound directory)
            >>= locateInDir