/// Contains methods for the install and update process.
module Paket.Process

open System.IO
open System

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
                                let! folder = Nuget.ExtractPackage(packageFile, package.Name, version.ToString(), force) 
                                return package,Nuget.GetLibraries folder}
                    | _ -> failwithf "Can't download from source type %s" package.SourceType)

let findLockfile packageFile =
    let fi = FileInfo(packageFile)
    FileInfo(Path.Combine(fi.Directory.FullName, fi.Name.Replace(fi.Extension, ".lock")))

let findPackagesForProject projectFile =
    let fi = FileInfo(projectFile)
    let packageFile = FileInfo(Path.Combine(fi.Directory.FullName, "packages"))
    if packageFile.Exists then File.ReadAllLines packageFile.FullName else [||]

let private findAllProjects(folder) = DirectoryInfo(folder).EnumerateFiles("*.*proj", SearchOption.AllDirectories)

/// Installs the given packageFile.
let Install(regenerate, force, packageFile) = 
    let lockfile = findLockfile packageFile
    if regenerate || (not lockfile.Exists) then LockFile.Update(packageFile, lockfile.FullName)
    let extracted = 
        ExtractPackages(force, File.ReadAllLines lockfile.FullName |> LockFile.Parse)
        |> Async.Parallel
        |> Async.RunSynchronously
    for proj in findAllProjects(".") do
        let usedPackages = findPackagesForProject proj.FullName
        let project = ProjectFile.Load proj.FullName
        for package, libraries in extracted do
            if Array.exists ((=) package.Name) usedPackages then 
                for lib in libraries do
                    let relativePath = Uri(proj.FullName).MakeRelativeUri(Uri(lib.FullName)).ToString().Replace("/", "\\")

                    project.UpdateReference ({ DLLName = lib.Name.Replace(lib.Extension, "")
                                               HintPath = Some relativePath
                                               Private = true
                                               Node = None })

        if project.Modified then
            project.Document.Save(proj.FullName)
        

/// Finds all outdated packages.
let FindOutdated(packageFile) = 
    let lockfile = findLockfile packageFile
    
    let newPackages = LockFile.Create(packageFile)
    let installed = if lockfile.Exists then LockFile.Parse(File.ReadAllLines lockfile.FullName) else []

    [for p in installed do
        match newPackages.[p.Name] with
        | Resolved newVersion -> 
            if p.VersionRange <> newVersion.Referenced.VersionRange then 
                match newVersion.Referenced.VersionRange with
                | Specific v2 -> 
                    match p.VersionRange with
                    | Specific v1 -> yield p.Name,v1,v2

        | Conflict(_) -> failwith "version conflict handling not implemented" ]

/// Prints all outdated packages.
let ListOutdated(packageFile) = 
    let allOutdated = FindOutdated packageFile
    if allOutdated = [] then
        tracefn "No outdated packages found."
    else
        tracefn "Outdated packages found:"
        for name,oldVersion,newVersion in allOutdated do
            tracefn "  * %s %s -> %s" name (oldVersion.ToString()) (newVersion.ToString())