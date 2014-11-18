/// Contains methods for NuGet conversion
module Paket.NuGetConvert

open Paket
open System
open System.IO
open System.Xml
open Paket.Domain
open Paket.Logging
open Paket.Xml
open Paket.NuGetV2
open Paket.PackageSources

type CredsMigrationMode =
    | Encrypt
    | Plaintext
    | Selective

    static member Parse(s : string) = 
        match s with 
        | "encrypt" -> Encrypt
        | "plaintext" -> Plaintext
        | "selective" -> Selective
        | _ -> failwithf "unknown credentials migration mode: %s" s

let private tryGetValue key (node : XmlNode) =
    node 
    |> getNodes "add"
    |> List.tryFind (getAttribute "key" >> (=) (Some key))
    |> Option.bind (getAttribute "value")

let private getKeyValueList (node : XmlNode) =
    node 
    |> getNodes "add"
    |> List.choose (fun node -> 
        match node |> getAttribute "key", node |> getAttribute "value" with
        | Some key, Some value -> Some(key, value)
        | _ -> None)

type NugetConfig = 
    { PackageSources : list<string * Auth option>
      PackageRestoreEnabled : bool
      PackageRestoreAutomatic : bool }

    static member empty =
        { PackageSources = [] 
          PackageRestoreEnabled = false 
          PackageRestoreAutomatic = false }

    member this.ApplyConfig (filename : string) =
        let doc = XmlDocument()
        doc.Load(filename)
        let config = 
            match doc |> getNode "configuration" with
            | Some node -> node
            | None -> failwithf "unable to parse %s" filename

        let clearSources = doc.SelectSingleNode("//packageSources/clear") <> null

        let getAuth key = 
            let getAuth' authNode =
                let userName = authNode |> tryGetValue "Username"
                let clearTextPass = authNode |> tryGetValue "ClearTextPassword"
                let encryptedPass = authNode |> tryGetValue "Password"

                match userName, encryptedPass, clearTextPass with 
                | Some userName, Some encryptedPass, _ -> 
                    Some { Username = userName; Password = ConfigFile.DecryptNuget encryptedPass }
                | Some userName, _, Some clearTextPass ->
                    Some  { Username = userName; Password = clearTextPass }
                | _ -> None

            config 
            |> getNode "packageSourceCredentials" 
            |> optGetNode (XmlConvert.EncodeLocalName key) 
            |> Option.bind getAuth'

        let sources = 
            config |> getNode "packageSources"
            |> Option.toList
            |> List.collect getKeyValueList
            |> List.map (fun (key,value) -> value, getAuth key)

        { PackageSources = if clearSources then sources else this.PackageSources @ sources
          PackageRestoreEnabled = 
            match config |> getNode "packageRestore" |> Option.bind (tryGetValue "enabled") with
            | Some value -> bool.Parse(value)
            | None -> this.PackageRestoreEnabled
          PackageRestoreAutomatic = 
            match config |> getNode "packageRestore" |> Option.bind (tryGetValue "automatic") with
            | Some value -> bool.Parse(value)
            | None -> this.PackageRestoreAutomatic }

let private readNugetConfig() =
    
    let config = 
        DirectoryInfo(".nuget")
        |> Seq.unfold (fun di -> if di = null 
                                 then None 
                                 else Some(FileInfo(Path.Combine(di.FullName, "nuget.config")), di.Parent)) 
        |> Seq.toList
        |> List.rev
        |> List.append [FileInfo(Path.Combine(Constants.AppDataFolder, "nuget", "nuget.config"))]
        |> List.filter (fun fi -> fi.Exists)
        |> List.fold (fun (config:NugetConfig) fi -> config.ApplyConfig fi.FullName) NugetConfig.empty
                     
    {config with PackageSources = if config.PackageSources = [] then [Constants.DefaultNugetStream, None] else config.PackageSources }

let removeFile file = 
    File.Delete file
    tracefn "Deleted %s" file

let private convertNugetsToDepFile(dependenciesFilename,nugetPackagesConfigs, sources) =
    let allVersions =
        nugetPackagesConfigs
        |> Seq.collect (fun c -> c.Packages)
        |> Seq.groupBy fst
        |> Seq.map (fun (name, packages) -> name, packages |> Seq.map snd |> Seq.distinct)
        |> Seq.sortBy (fun (name,_) -> name.ToLower())
    
    for (name, versions) in allVersions do
        if Seq.length versions > 1 
        then traceWarnfn "Package %s is referenced multiple times in different versions: %A. Paket will choose the latest one." 
                            name    
                            (versions |> Seq.map string |> Seq.toList)
    
    let latestVersions = allVersions |> Seq.map (fun (name,versions) -> name, versions |> Seq.max |> string) |> Seq.toList

    let existingDepFile = 
        if File.Exists dependenciesFilename
        then Some(DependenciesFile.ReadFromFile dependenciesFilename) 
        else None

    let confictingPackages, packagesToAdd = 
        match existingDepFile with
        | Some depFile -> latestVersions |> List.partition (fun (name,_) -> depFile.HasPackage (PackageName name))
        | None -> [], latestVersions
    
    for (name, _) in confictingPackages do traceWarnfn "Package %s is already defined in %s" name dependenciesFilename

    let nugetPackageRequirement (name: string, v: string) =
        {Requirements.PackageRequirement.Name = PackageName name
         Requirements.PackageRequirement.VersionRequirement = VersionRequirement(VersionRange.Specific(SemVer.Parse v), PreReleaseStatus.No)
         Requirements.PackageRequirement.ResolverStrategy = Max
         Requirements.PackageRequirement.Sources = sources
         Requirements.PackageRequirement.Parent = Requirements.PackageRequirementSource.DependenciesFile dependenciesFilename}

    match existingDepFile with
    | None ->
        let packages = packagesToAdd |> List.map (fun (name,v) -> nugetPackageRequirement(name,v))
        DependenciesFile(dependenciesFilename, InstallOptions.Default, packages, []).Save()
    | Some depFile ->
        if not (packagesToAdd |> List.isEmpty)
            then (packagesToAdd |> List.fold (fun (d : DependenciesFile) (name,version) -> d.Add(PackageName name,version)) depFile).Save()
        else tracefn "%s is up to date" depFile.FileName

let private convertNugetToRefFile(nugetPackagesConfig) =
    let refFilePath = Path.Combine(nugetPackagesConfig.File.DirectoryName, Constants.ReferencesFile)
    let existingRefFile = if File.Exists refFilePath then Some <| ReferencesFile.FromFile(refFilePath) else None

    let confictingRefs, refsToAdd =
        match existingRefFile with
        | Some refFile -> 
            nugetPackagesConfig.Packages 
            |> List.partition (fun (name,_) -> 
                                    refFile.NugetPackages |> List.exists (fun (NormalizedPackageName np) -> np = NormalizedPackageName (PackageName name)))
        | _ -> [], nugetPackagesConfig.Packages
    
    for (name,_) in confictingRefs do traceWarnfn "Reference %s is already defined in %s" name refFilePath
            
    match existingRefFile with 
    | None -> {ReferencesFile.FileName = refFilePath; NugetPackages = refsToAdd |> List.map fst |> List.map PackageName; RemoteFiles = []}.Save()
    | Some refFile ->
        if not (refsToAdd |> List.isEmpty)
            then (refsToAdd |> List.fold (fun (refFile : ReferencesFile) (name,_) -> refFile.AddNuGetReference(PackageName name)) refFile).Save()
        else tracefn "%s is up to date" refFilePath

/// Converts all projects from NuGet to Paket
let ConvertFromNuget(dependenciesFileName, force, installAfter, initAutoRestore, credsMigrationMode) =
    if File.Exists dependenciesFileName && not force then failwithf "%s already exists, use --force to overwrite" dependenciesFileName
    let root =
        if dependenciesFileName = Constants.DependenciesFileName then
            "."
        else
            Path.GetDirectoryName dependenciesFileName

    let nugetPackagesConfigs = FindAllFiles(root, "packages.config") |> Seq.map NuGetV2.ReadPackagesConfig
    let nugetConfig = readNugetConfig()
    FindAllFiles(root, "nuget.config") |> Seq.iter (fun f -> removeFile f.FullName)

    let migrateCredentials sourceName auth =
        let credsMigrationMode = defaultArg credsMigrationMode Encrypt
        match credsMigrationMode with
        | Encrypt -> 
            ConfigAuthentication(auth.Username, auth.Password)
        | Plaintext -> 
            PlainTextAuthentication(auth.Username, auth.Password)
        | Selective -> 
            let question =
                sprintf "Credentials for source '%s': " sourceName  + 
                    "[encrypt and save in config (Yes) " + 
                    sprintf "| save as plaintext in %s (No)]" Constants.DependenciesFileName
                    
            match Utils.askYesNo question with
            | true -> ConfigAuthentication(auth.Username, auth.Password)
            | false -> PlainTextAuthentication(auth.Username, auth.Password)

    let sources = 
        nugetConfig.PackageSources 
        |> List.map (fun (name,auth) -> 
                        PackageSource.Parse(name, auth |> Option.map (migrateCredentials name)))

    convertNugetsToDepFile(dependenciesFileName, nugetPackagesConfigs, sources)
        
    for nugetPackagesConfig in nugetPackagesConfigs do
        let packageFile = nugetPackagesConfig.File
        match nugetPackagesConfig.Type with
        | ProjectLevel ->
            let refFile = Path.Combine(packageFile.DirectoryName, Constants.ReferencesFile)
            if File.Exists refFile && not force then failwithf "%s already exists, use --force to overwrite" refFile
            convertNugetToRefFile(nugetPackagesConfig)
        | SolutionLevel -> ()

    for slnFile in FindAllFiles(".", "*.sln") do
        let solution = SolutionFile(slnFile.FullName)
        solution.RemoveNugetEntries()
        let relativePath = createRelativePath solution.FileName Environment.CurrentDirectory 
        solution.AddPaketFolder(Path.Combine(relativePath, dependenciesFileName), 
                                if installAfter then Some(Path.Combine(relativePath, "paket.lock")) else None)
        solution.Save()

    for project in ProjectFile.FindAllProjects root do
        project.ReplaceNugetPackagesFile()
        project.RemoveNugetTargetsEntries()
        project.Save()

    for packagesConfigFile in nugetPackagesConfigs |> Seq.map (fun f -> f.File) do
        removeFile packagesConfigFile.FullName

    let autoVsNugetRestore = nugetConfig.PackageRestoreEnabled && nugetConfig.PackageRestoreAutomatic
    let nugetTargets = FindAllFiles(root, "nuget.targets") |> Seq.firstOrDefault
    
    match nugetTargets with
    | Some nugetTargets ->
        removeFile nugetTargets.FullName
        let nugetExe = Path.Combine(nugetTargets.DirectoryName, "nuget.exe")
        if File.Exists nugetExe then 
            traceWarnfn "Removing NuGet.exe and adding Nuget.CommandLine as dependency instead. Please check all paths."
            removeFile nugetExe
            let nugetCommandLine = PackageName "NuGet.CommandLine"
            let depFile = DependenciesFile.ReadFromFile dependenciesFileName
            if not <| depFile.HasPackage(nugetCommandLine) then 
                depFile.Add(nugetCommandLine, "").Save()
            
        if Directory.EnumerateFileSystemEntries(nugetTargets.DirectoryName) |> Seq.isEmpty 
            then Directory.Delete nugetTargets.DirectoryName
    | None -> ()

    if initAutoRestore && (autoVsNugetRestore || nugetTargets.IsSome) then 
        VSIntegration.InitAutoRestore dependenciesFileName

    if installAfter then
        UpdateProcess.Update(dependenciesFileName,true,false,true)
