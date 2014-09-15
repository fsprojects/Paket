/// Contains methods for the install and update process.
module Paket.Process

open System
open System.IO
open System.Collections.Generic

/// Downloads and extracts all package.
let ExtractPackages(force, packages : ResolvedPackage seq) = 
    packages |> Seq.map (fun package -> 
                            async {
                                match package.Source with
                                | Nuget source -> 
                                    let! packageFile = 
                                        Nuget.DownloadPackage(source, package.Name, [package.Source], package.Version.ToString(), force)
                                    let! folder = Nuget.ExtractPackage(packageFile, package.Name, package.Version.ToString(), force)
                                    return package, Nuget.GetLibraries folder
                                | LocalNuget path -> 
                                    let packageFile = Path.Combine(path, sprintf "%s.%s.nupkg" package.Name (package.Version.ToString()))
                                    let! folder = Nuget.ExtractPackage(packageFile, package.Name, package.Version.ToString(), force)
                                    return package, Nuget.GetLibraries folder })

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

let private findAllFiles(folder, pattern) = DirectoryInfo(folder).EnumerateFiles(pattern, SearchOption.AllDirectories)

/// Installs the given packageFile.
let Install(regenerate, force, hard, dependenciesFile) = 
    let lockfile = findLockfile dependenciesFile
     
    if regenerate || (not lockfile.Exists) then 
        LockFile.Update(force, dependenciesFile, lockfile.FullName)

    let extracted = 
        ExtractPackages(force, File.ReadAllLines lockfile.FullName |> LockFile.Parse)
        |> Async.Parallel
        |> Async.RunSynchronously
    for proj in findAllFiles(".", "*.*proj") do
        let directPackages = extractReferencesFromListFile proj.FullName
        let project = ProjectFile.Load proj.FullName

        let usedPackages = new HashSet<_>()

        let allPackages =
            extracted
            |> Array.map (fun (p,_) -> p.Name,p)
            |> Map.ofArray

        let rec addPackage name =
            match allPackages |> Map.tryFind name with
            | Some package ->
                if usedPackages.Add name then
                    for d,_ in package.DirectDependencies do
                        addPackage d
            | None -> failwithf "Project %s references package %s, but it was not found in the Lock file." proj.FullName name

        directPackages
        |> Array.iter addPackage
        
        project.UpdateReferences(extracted,usedPackages,hard)


/// Finds all outdated packages.
let FindOutdated(packageFile) = 
    let lockFile = findLockfile packageFile
    
    let newPackages = LockFile.Create(true,packageFile)
    let installed = if lockFile.Exists then LockFile.Parse(File.ReadAllLines lockFile.FullName) else []

    [for p in installed do
        match newPackages.[p.Name] with
        | Resolved newVersion -> 
            if p.Version <> newVersion.Version then 
                yield p.Name,p.Version,newVersion.Version

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

/// Converts all projects from NuGet to Paket
let ConvertFromNuget() =
    let nugetPackages = 
        findAllFiles(".", "packages.config") |> Seq.map (fun f -> f, Nuget.ReadPackagesFromFile f)
    
    let allVersions =
        nugetPackages
        |> Seq.map snd
        |> Seq.concat
        |> Seq.groupBy fst
        |> Seq.map (fun (k, packages) -> k, packages |> Seq.map snd |> Seq.distinct)
    
    for (name, versions) in allVersions do
        if versions |> Seq.length > 1 
        then traceWarnfn "Package %s is referenced multiple times in different versions: %A. Taking the latest one." 
                            name    
                            (versions |> Seq.map string |> Seq.toList)

    let dependenciesFileContent = 
        "source \"http://nuget.org/api/v2\"" + Environment.NewLine + Environment.NewLine +
         (allVersions |> Seq.map (fun (k,packages) -> sprintf "nuget \"%s\" \"%s\"" k (Seq.max packages |> string))
         |> String.concat Environment.NewLine)
        
    File.WriteAllText("paket.dependencies", dependenciesFileContent)
    trace "Generated paket.dependencies file"
        
    for (packageFile, packages) in nugetPackages do
        if packageFile.Directory.Name <> ".nuget"
        then
            match findAllFiles(packageFile.DirectoryName, "*.*proj") |> Seq.toList with
            | [projFile] -> 
                let referencesFileContent = packages |> List.map fst |> String.concat Environment.NewLine
                File.WriteAllText(packageFile.FullName, referencesFileContent)
                File.Move(packageFile.FullName, Path.Combine(packageFile.DirectoryName, "paket.references"))
                let updated = File.ReadAllText(projFile.FullName).Replace(packageFile.Name, "paket.references")
                File.WriteAllText(projFile.FullName, updated)
                tracefn "Converted \"%s\" to \"paket.references\"" packageFile.FullName

            | [] -> traceErrorfn "Unable to convert \"%s\" because corresponding project file was not found." packageFile.FullName
            | _::_ as files -> 
                traceErrorfn "Unable to convert \"%s\" because more than one file matches project file: %A" packageFile.FullName files
        else
            match findAllFiles(packageFile.Directory.Parent.FullName, "*.*sln") |> Seq.toList with
            | [slnFile] -> 
                let solution = SolutionFile(slnFile.FullName)
                solution.RemoveNugetPackagesFile()
                solution.Save()
                File.Delete(packageFile.FullName)
                tracefn "Deleted \"%s\"" packageFile.FullName

            | [] -> traceErrorfn "Unable to delete \"%s\" because corresponding solution file was not found." packageFile.FullName
            | _::_ as files -> 
                traceErrorfn "Unable to delete \"%s\" because more than one file matches solution file: %A" packageFile.FullName files

    if Directory.Exists(".\packages") then 
        CleanDir ".\packages"
        trace "Cleared .\packages directory"