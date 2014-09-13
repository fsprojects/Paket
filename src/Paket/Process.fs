/// Contains methods for the install and update process.
module Paket.Process

open System.IO
open System.Collections.Generic

/// Downloads and extracts all package.
let ExtractPackages(force, packages : Package seq) = 
    packages |> Seq.map (fun package -> 
                    let version = 
                        match package.VersionRange with
                        | Specific v -> v
                        | v -> failwithf "Version error in Lock file for %s %A" package.Name v


                    let rec trySource sources = 
                        async { 
                            match sources with
                            | [] -> 
                                failwithf "could not find package %s in %A" package.Name package.Sources
                                return package, [||]
                            | source :: rest -> 
                                try
                                    match source with
                                    | Nuget source -> let! packageFile = Nuget.DownloadPackage
                                                                             (source, package.Name, package.Sources, package.ResolverStrategy, 
                                                                              version.ToString(), force)
                                                      let! folder = Nuget.ExtractPackage(packageFile, package.Name, version.ToString(), force)
                                                      return package, Nuget.GetLibraries folder
                                    | LocalNuget path -> 
                                        let packageFile = Path.Combine(path, sprintf "%s.%s.nupkg" package.Name (version.ToString()))
                                        let! folder = Nuget.ExtractPackage(packageFile, package.Name, version.ToString(), force)
                                        return package, Nuget.GetLibraries folder
                                with
                                | _ -> return! trySource rest
                        }
                    
                    trySource package.Sources)

let findLockfile dependenciesFile =
    let fi = FileInfo(dependenciesFile)
    FileInfo(Path.Combine(fi.Directory.FullName, fi.Name.Replace(fi.Extension,"") + ".lock"))


let extractReferencesFromListFile projectFile = 
    let fi = FileInfo(projectFile)
    
    let references = 
        let specificReferencesFile = FileInfo(Path.Combine(fi.Directory.FullName, fi.Name + ".paket.references"))
        if specificReferencesFile.Exists then File.ReadAllLines specificReferencesFile.FullName
        else 
            let generalReferencesFile = FileInfo(Path.Combine(fi.Directory.FullName, "paket.references"))
            if generalReferencesFile.Exists then File.ReadAllLines generalReferencesFile.FullName
            else [||]
    references
    |> Array.map (fun s -> s.Trim())
    |> Array.filter (fun s -> System.String.IsNullOrWhiteSpace s |> not)

let private findAllProjects(folder) = DirectoryInfo(folder).EnumerateFiles("*.*proj", SearchOption.AllDirectories)

/// Installs the given packageFile.
let Install(regenerate, force, dependenciesFilename) = 
    let packages, sourceFiles =
        let lockfile = findLockfile dependenciesFilename   
        
        if regenerate || (not lockfile.Exists) then 
            LockFile.Update(force, dependenciesFilename, lockfile.FullName)
        
        File.ReadAllLines lockfile.FullName |> LockFile.Parse

    let extractedPackages = 
        ExtractPackages(force, packages)
        |> Async.Parallel
        |> Async.RunSynchronously    

    let extractedSourceFiles =
        let rootPath = dependenciesFilename |> Path.GetDirectoryName
        sourceFiles
        |> List.map(fun source ->
                async {
                    let destination = Path.Combine(rootPath, "paket-files", source.Owner, source.Project, source.CommitWithDefault, source.FilePath)

                    if File.Exists destination then tracefn "%s already exists locally" (source.ToString())
                    else
                        tracefn "Downloading %s..." (source.ToString())
                        let! file = GitHub.downloadFile source
                        Directory.CreateDirectory(destination |> Path.GetDirectoryName) |> ignore
                        File.WriteAllText(destination, file)
                    return destination })
        |> Async.Parallel
        |> Async.RunSynchronously

    for proj in findAllProjects(".") do
        let directPackages = extractReferencesFromListFile proj.FullName
        let project = ProjectFile.Load proj.FullName

        let usedPackages = new HashSet<_>()

        let allPackages =
            extractedPackages
            |> Array.map (fun (p,_) -> p.Name,p)
            |> Map.ofArray

        let rec addPackage name =
            match allPackages |> Map.tryFind name with
            | Some package ->
                if usedPackages.Add name then
                    for d in package.DirectDependencies do
                        addPackage d
            | None -> failwithf "Project %s references package %s, but it was not found in the Lock file." proj.FullName name

        directPackages
        |> Array.iter addPackage
        
        project.UpdateReferences(extractedPackages,usedPackages)


/// Finds all outdated packages.
let FindOutdated(dependenciesFile) = 
    let lockFile = findLockfile dependenciesFile

    //TODO: Anything we need to do for source files here?    
    let newPackages, _ = LockFile.Create(true, dependenciesFile)
    let installedPackages, _ =
        if lockFile.Exists then LockFile.Parse(File.ReadAllLines lockFile.FullName) else [], []

    [for p in installedPackages do
        match newPackages.ResolvedVersionMap.[p.Name] with
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