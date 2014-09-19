/// Contains methods for the install and update process.
module Paket.Process

open System
open System.IO
open System.Collections.Generic
open System.Xml

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

let private depFileName = "paket.dependencies"

let private readPackageSources(configFile : FileInfo) =
    let doc = XmlDocument()
    doc.Load configFile.FullName
    [for node in doc.SelectNodes("//packageSources/add[@value]") -> node.Attributes.["value"].Value]

let private convertNugetsToDepFile(nugetPackagesConfigs) =
    let allVersions =
        nugetPackagesConfigs
        |> Seq.collect (fun c -> c.Packages)
        |> Seq.groupBy fst
        |> Seq.map (fun (name, packages) -> name, packages |> Seq.map snd |> Seq.distinct)
    
    for (name, versions) in allVersions do
        if Seq.length versions > 1 
        then traceWarnfn "Package %s is referenced multiple times in different versions: %A. Paket will choose the latest one." 
                            name    
                            (versions |> Seq.map string |> Seq.toList)
    
    let latestVersions = allVersions |> Seq.map (fun (name,versions) -> name, versions |> Seq.max |> string)
    
    let depFileExists = File.Exists depFileName 
    let existingPackages = 
        if depFileExists 
        then (DependenciesFile.ReadFromFile depFileName).Packages |> List.map (fun p -> p.Name.ToLower()) |> Set.ofList
        else Set.empty
    let confictingPackages = Set.intersect existingPackages (latestVersions |> Seq.map fst |> Seq.map (fun n -> n.ToLower()) |> Set.ofSeq)
    confictingPackages |> Set.iter (fun name -> traceWarnfn "Package %s is already defined in %s" name depFileName)

    let dependencyLines = 
        latestVersions 
        |> Seq.filter (fun (name,_) -> not (confictingPackages |> Set.contains (name.ToLower())))
        |> Seq.map (fun (name,version) -> sprintf "nuget %s %s" name version)
        |> Seq.toList
    
    if not depFileExists 
        then
            let packageSources =
                match findAllFiles(".", "nuget.config") |> Seq.tryFind (fun _ -> true) with
                | Some configFile -> 
                    let sources = readPackageSources(configFile) 
                    File.Delete(configFile.FullName)
                    sources
                | None -> ["http://nuget.org/api/v2"]
                |> List.map (sprintf "source %s")
            File.WriteAllLines(depFileName, packageSources @ [String.Empty] @ dependencyLines)
            tracefn "Generated \"%s\" file" depFileName 
    elif not (dependencyLines |> Seq.isEmpty)
        then
            File.AppendAllLines(depFileName, dependencyLines)
            traceWarnfn "Overwritten \"%s\" file" depFileName 
    else tracefn "%s is up to date" depFileName
        

let private convertNugetToRefFile(nugetPackagesConfig) =
    let refFile = Path.Combine(nugetPackagesConfig.File.DirectoryName, "paket.references")
    let refFileExists = File.Exists refFile
    let existingReferences = 
        if refFileExists
        then (File.ReadAllLines refFile) |> Array.map (fun p -> p.ToLower()) |> Set.ofArray
        else Set.empty
    let confictingReferences = Set.intersect existingReferences (nugetPackagesConfig.Packages |> List.map fst |> Seq.map (fun n -> n.ToLower()) |> Set.ofSeq)
    confictingReferences |> Set.iter (fun name -> traceWarnfn "Reference %s is already defined in %s" name refFile)

    let referencesLines = 
        nugetPackagesConfig.Packages 
        |> List.map fst 
        |> List.filter (fun name -> not (confictingReferences |> Set.contains (name.ToLower())))
                            
    if not refFileExists 
        then
            File.WriteAllLines(refFile, referencesLines)
            tracefn "Converted \"%s\" to \"paket.references\"" nugetPackagesConfig.File.FullName 
    elif not (referencesLines |> List.isEmpty)
        then
            File.AppendAllLines(refFile, referencesLines)
            traceWarnfn "Overwritten \"%s\" file" refFile
    else tracefn "%s is up to date" refFile

/// Converts all projects from NuGet to Paket
let ConvertFromNuget(force, installAfter) =
    if File.Exists depFileName && not force then failwithf "%s already exists, use --force to overwrite" depFileName
    
    let nugetPackagesConfigs = findAllFiles(".", "packages.config") |> Seq.map Nuget.ReadPackagesConfig
    convertNugetsToDepFile(nugetPackagesConfigs)
        
    for nugetPackagesConfig  in nugetPackagesConfigs do
        let packageFile = nugetPackagesConfig.File
        match nugetPackagesConfig.Type with
        | ProjectLevel ->
            let refFile = Path.Combine(packageFile.DirectoryName, "paket.references")
            if File.Exists refFile && not force then failwithf "%s already exists, use --force to overwrite" refFile

            convertNugetToRefFile(nugetPackagesConfig)

            for file in findAllProjects(packageFile.DirectoryName) do
                let project = ProjectFile.Load(file.FullName)
                project.ReplaceNugetPackagesFile()
                project.Save()

            File.Delete(packageFile.FullName)

        | SolutionLevel ->
            for slnFile in findAllFiles(packageFile.Directory.Parent.FullName, "*.sln") do
                SolutionFile.RemoveNugetPackagesFile(slnFile.FullName)
            
            File.Delete(packageFile.FullName)

            if Directory.EnumerateFileSystemEntries(packageFile.DirectoryName) |> Seq.isEmpty 
                then Directory.Delete packageFile.DirectoryName
            tracefn "Deleted solution-level \"%s\"" packageFile.FullName

    if installAfter then Install(false, false, true, depFileName)
