/// Contains methods for the install and update process.
module Paket.Process

open System
open System.IO
open System.Collections.Generic

/// Downloads and extracts all packages.
let ExtractPackages(force, packages) = 
    Seq.map (fun (package : ResolvedPackage) -> 
        async { 
            match package.Source with
            | Nuget source -> 
                let! packageFile = Nuget.DownloadPackage(source, package.Name, [ package.Source ], package.Version.ToString(), force)
                let! folder = Nuget.ExtractPackage(packageFile, package.Name, package.Version.ToString(), force)
                return Some(package, Nuget.GetLibraries folder)
            | LocalNuget path -> 
                let packageFile = Path.Combine(path, sprintf "%s.%s.nupkg" package.Name (package.Version.ToString()))
                let! folder = Nuget.ExtractPackage(packageFile, package.Name, package.Version.ToString(), force)
                return Some(package, Nuget.GetLibraries folder)
        }) packages

let DownloadSourceFiles(rootPath,sourceFiles) = 
    Seq.map (fun (source : SourceFile) -> 
        async { 
            let destination = Path.Combine(rootPath, source.FilePath)
            tracefn "Downloading %s..." (source.ToString())
            let! file = GitHub.downloadFile source
            Directory.CreateDirectory(destination |> Path.GetDirectoryName) |> ignore
            File.WriteAllText(destination, file)
            return None
        }) sourceFiles

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

let private findAllProjects(folder) = 
    ["*.csproj";"*.fsproj";"*.vbproj"]
    |> List.map (fun projectType -> findAllFiles(folder, projectType) |> Seq.toList)
    |> List.concat

let private findPackagesWithContent usedPackages = 
    usedPackages
    |> Seq.map (fun p -> DirectoryInfo(Path.Combine("packages", p)))
    |> Seq.choose (fun packageDir -> packageDir.GetDirectories("Content") |> Array.tryFind (fun _ -> true))
    |> Seq.toList

let private copyContentFilesToProject project packagesWithContent = 

    let rec copyDirContents (fromDir : DirectoryInfo, toDir : DirectoryInfo) =
        fromDir.GetDirectories() |> Array.toList
        |> List.collect (fun subDir -> copyDirContents(subDir, toDir.CreateSubdirectory(subDir.Name)))
        |> List.append
            (fromDir.GetFiles() 
                |> Array.toList
                |> List.map (fun file -> file.CopyTo(Path.Combine(toDir.FullName, file.Name), true)))

    packagesWithContent
    |> List.collect (fun packageDir -> copyDirContents (packageDir, (DirectoryInfo(Path.GetDirectoryName(project.FileName)))))

let private removeContentFiles (project: ProjectFile) =
    project.GetContentFiles() 
        |> List.sortBy (fun f -> f.FullName)
        |> List.rev
        |> List.iter(fun f -> 
                         File.Delete(f.FullName)
                         if f.Directory.GetFiles() |> Seq.isEmpty then Directory.Delete(f.Directory.FullName))

/// Installs the given packageFile.
let Install(regenerate, force, hard, dependenciesFilename) = 
    let lockFile =
        let lockFileName = findLockfile dependenciesFilename
        
        if regenerate || (not lockFileName.Exists) then 
            LockFile.Update(force, dependenciesFilename, lockFileName.FullName)
        
        File.ReadAllLines lockFileName.FullName 
        |> LockFile.LockFile.Parse


    let extractedPackages = 
        ExtractPackages(force, lockFile.ResolvedPackages)
        |> Seq.append (DownloadSourceFiles(Path.GetDirectoryName dependenciesFilename, lockFile.SourceFiles))
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.choose id

    for proj in findAllProjects(".") do
        let directPackages = extractReferencesFromListFile proj.FullName
        let project = ProjectFile.Load proj.FullName

        let usedPackages = new HashSet<_>()
        let usedSourceFiles = new HashSet<_>()

        let allPackages =
            extractedPackages
            |> Array.map (fun (p,_) -> p.Name.ToLower(),p)
            |> Map.ofArray

        let rec addPackage (name:string) =
            if name.ToLower().StartsWith "file:" then
                let sourceFile = name.Split(':').[1]
                usedSourceFiles.Add sourceFile |> ignore
            else
                let name = name.ToLower()
                match allPackages |> Map.tryFind name with
                | Some package ->
                    if usedPackages.Add name then
                        if not lockFile.Strict then
                            for d,_ in package.DirectDependencies do
                                addPackage d
                | None -> failwithf "Project %s references package %s, but it was not found in the paket.lock file." proj.FullName name

        directPackages
        |> Array.iter addPackage
        
        project.UpdateReferences(extractedPackages,usedPackages,hard)

        lockFile.SourceFiles 
        |> List.filter (fun file -> usedSourceFiles.Contains(file.Name))
        |> project.UpdateSourceFiles

        removeContentFiles project
        let packagesWithContent = findPackagesWithContent usedPackages
        let contentFiles = copyContentFilesToProject project packagesWithContent
        project.UpdateContentFiles(contentFiles)

        project.Save()

/// Finds all outdated packages.
let FindOutdated(dependenciesFile) = 
    let lockFile = findLockfile dependenciesFile

    //TODO: Anything we need to do for source files here?    
    let _,newPackages, _ = LockFile.Create(true, dependenciesFile)
    let lockFile  =
        if lockFile.Exists then LockFile.LockFile.Parse(File.ReadAllLines lockFile.FullName) else LockFile.LockFile(false,[],[])

    [for p in lockFile.ResolvedPackages do
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
        |> Seq.collect snd
        |> Seq.groupBy fst
        |> Seq.map (fun (name, packages) -> name, packages |> Seq.map snd |> Seq.distinct)
    
    for (name, versions) in allVersions do
        if Seq.length versions > 1 
        then traceWarnfn "Package %s is referenced multiple times in different versions: %A. Paket will choose the latest one." 
                            name    
                            (versions |> Seq.map string |> Seq.toList)
    
    let latestVersions = allVersions |> Seq.map (fun (name,versions) -> name, versions |> Seq.max |> string)

    let dependenciesFileContent = 
        "source http://nuget.org/api/v2" + Environment.NewLine + Environment.NewLine +
         (latestVersions |> Seq.map (fun (name,version) -> sprintf "nuget %s %s" name version)
         |> String.concat Environment.NewLine)
        
    File.WriteAllText("paket.dependencies", dependenciesFileContent)
    trace "Generated \"paket.dependencies\" file"
        
    for (packageFile, packages) in nugetPackages do
        if packageFile.Directory.Name <> ".nuget"
        then
            let referencesFileContent = packages |> List.map fst |> String.concat Environment.NewLine
            File.WriteAllText(packageFile.FullName, referencesFileContent)
            File.Move(packageFile.FullName, Path.Combine(packageFile.DirectoryName, "paket.references"))
            
            for file in findAllProjects(packageFile.DirectoryName) do
                let project = ProjectFile.Load(file.FullName)
                project.ConvertNugetToPaket()
                project.Save()
                           
            tracefn "Converted \"%s\" to \"paket.references\"" packageFile.FullName

        else
            File.Delete(packageFile.FullName)

            for slnFile in findAllFiles(packageFile.Directory.Parent.FullName, "*.sln") do
                SolutionFile.RemoveNugetPackagesFile(slnFile.FullName)
            
            tracefn "Deleted \"%s\"" packageFile.FullName

    if Directory.Exists(".\packages") then 
        CleanDir ".\packages"
        trace "Cleared .\packages directory"

    Install(false, false, true, "paket.dependencies")
