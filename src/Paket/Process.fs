/// Contains methods for the install and update process.
module Paket.Process

open System.IO

/// Downloads and extracts all package.
let ExtractPackages(force, packages : Package seq) = 
    packages |> Seq.map (fun package -> 
                    let version = 
                        match package.VersionRange with
                        | Specific v -> v
                        | v -> failwithf "Version error in lockfile for %s %A" package.Name v
                    match package.SourceType with
                    | "nuget" -> 
                        async { let! packageFile = Nuget.DownloadPackage
                                                       (package.Source, package.Name, version.ToString(), force)
                                return! Nuget.ExtractPackage(packageFile, package.Name, version.ToString(), force) }
                    | _ -> failwithf "Can't download from source type %s" package.SourceType)

let findLockfile packageFile =
    let fi = FileInfo(packageFile)
    FileInfo(Path.Combine(fi.Directory.FullName, fi.Name.Replace(fi.Extension, ".lock")))

/// Installs the given packageFile.
let Install(regenerate, force, packageFile) = 
    let lockfile = findLockfile packageFile
    if regenerate || (not lockfile.Exists) then LockFile.Update(packageFile, lockfile.FullName)
    ExtractPackages(force, File.ReadAllLines lockfile.FullName |> LockFile.Parse)
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore

/// Finds all outdated packages.
let FindOutdated(packageFile) = 
    let lockfile = findLockfile packageFile
    
    let newPackages = LockFile.Create(packageFile)
    let installed = if lockfile.Exists then LockFile.Parse(File.ReadAllLines lockfile.FullName) else []

    [for p in installed do
        match newPackages.[p.Name] with
        | Resolved newVersion ->  if p.VersionRange <> newVersion.Referenced.VersionRange then yield newVersion.Referenced
        | Conflict(_) -> failwith "version conflict handling not implemented" ]

/// Prints all outdated packages.
let ListOutdated(packageFile) = 
    let allOutdated = FindOutdated packageFile
    if allOutdated = [] then
        tracefn "No outdated packages found."
    for outdated in allOutdated do
        match outdated.VersionRange with
        | Specific v ->  tracefn "%s %s" outdated.Name <| v.ToString()