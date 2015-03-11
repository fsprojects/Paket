module Paket.Domain

open System.IO
open Paket

/// Represents a NuGet package name
[<System.Diagnostics.DebuggerDisplay("{Item}")>]
type PackageName =
| PackageName of string

    member this.Id = 
        match this with
        | PackageName id -> id

    override this.ToString() = this.Id

/// Active recognizer to convert a NuGet package name into a string
let (|PackageName|) (PackageName.PackageName name) = name

/// Function to convert a string into a NuGet package name
let PackageName name = PackageName.PackageName name

/// Represents a normalized NuGet package name
[<System.Diagnostics.DebuggerDisplay("{Item}")>]
type NormalizedPackageName =
| NormalizedPackageName of string

    override this.ToString() = 
        match this with
        | NormalizedPackageName id -> id

/// Active recognizer to convert a NuGet package name into a normalized one
let (|NormalizedPackageName|) (PackageName name) =
    NormalizedPackageName.NormalizedPackageName(name.ToLowerInvariant())

/// Function to convert a NuGet package name into a normalized one
let NormalizedPackageName = (|NormalizedPackageName|)

type DomainMessage = 
    | DirectoryDoesntExist of DirectoryInfo
    | DependenciesFileNotFoundInDir of DirectoryInfo
    | DependenciesFileParseError of FileInfo
    | LockFileNotFound of DirectoryInfo
    | LockFileParseError of FileInfo
    | ReferencesFileParseError of FileInfo

    | PackageSourceParseError of string
    
    | InvalidCredentialsMigrationMode of string
    | PaketEnvAlreadyExistsInDirectory of DirectoryInfo
    | NugetConfigFileParseError of FileInfo
    | NugetPackagesConfigParseError of FileInfo

    | StrictModeDetected
    | DependencyNotFoundInLockFile of PackageName
    | ReferenceNotFoundInLockFile of string * PackageName

    | DownloadError of string
    | ReleasesJsonParseError
    
    | DirectoryCreateError of string 
    | FileDeleteError of string
    | FileSaveError of string

    | ConfigFileParseError
    
    | PackagingConfigParseError of string * string

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
        
        | PackageSourceParseError(source) -> 
            sprintf "Unable to parse package source: %s." source

        | InvalidCredentialsMigrationMode(mode) ->
            sprintf "Invalid credentials migration mode: %s." mode
        | PaketEnvAlreadyExistsInDirectory(di) ->
            sprintf "Paket is already present in %s. Run with --force to overwrite." di.FullName
        | NugetConfigFileParseError(fi) ->
            sprintf "Unable to parse %s" fi.FullName
        | NugetPackagesConfigParseError(fi) ->
            sprintf "Unable to parse %s" fi.FullName

        | StrictModeDetected -> 
            "Strict mode detected. Command not executed."
        | DependencyNotFoundInLockFile(PackageName name) -> 
            sprintf "Dependency %s from %s not found in lock file." name Constants.DependenciesFileName
        | ReferenceNotFoundInLockFile(path, PackageName name) -> 
            sprintf "Reference %s from %s not found in lock file." name path

        | DownloadError url ->
            sprintf "Error occured while downloading from %s." url
        | ReleasesJsonParseError ->
            "Unable to parse Json from GitHub releases API."
        
        | DirectoryCreateError path ->
            sprintf "Unable to create directory %s." path
        | FileDeleteError path ->
            sprintf "Unable to delete file %s." path
        | FileSaveError path ->
            sprintf "Unable to save file %s." path

        | ConfigFileParseError ->
            sprintf "Unable to parse Paket configuration file %s." Constants.PaketConfigFile

        | PackagingConfigParseError(file,error) ->
            sprintf "Unable to parse template file %s: %s." file error
