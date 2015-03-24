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
open Paket.Requirements
open Chessie.ErrorHandling

type CredsMigrationMode =
    | Encrypt
    | Plaintext
    | Selective

    static member parse(s : string) = 
        match s with 
        | "encrypt" -> ok Encrypt
        | "plaintext" -> ok  Plaintext
        | "selective" -> ok Selective
        | _ ->  InvalidCredentialsMigrationMode s |> fail

    static member toAuthentication mode sourceName auth =
        match mode with
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

/// Represents type of NuGet packages.config file
type NugetPackagesConfigType = ProjectLevel | SolutionLevel

/// Represents NuGet packages.config file
type NugetPackagesConfig = {
    File: FileInfo
    Packages: (string*SemVerInfo) list
    Type: NugetPackagesConfigType
}
    
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

    static member getConfigNode (file : FileInfo) =  
        try 
            let doc = XmlDocument()
            doc.Load(file.FullName)
            (doc |> getNode "configuration").Value |> ok
        with _ -> 
            file
            |> NugetConfigFileParseError
            |> fail

    static member overrideConfig nugetConfig (configNode : XmlNode) =
        let clearSources = configNode.SelectSingleNode("//packageSources/clear") <> null

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

            configNode 
            |> getNode "packageSourceCredentials" 
            |> optGetNode (XmlConvert.EncodeLocalName key) 
            |> Option.bind getAuth'

        let disabledSources =
            configNode |> getNode "disabledPackageSources"
            |> Option.toList
            |> List.collect getKeyValueList
            |> List.filter (fun (_,disabled) -> disabled.Equals("true", StringComparison.OrdinalIgnoreCase))
            |> List.map fst
            |> Set.ofList
            
        let sources = 
            configNode |> getNode "packageSources"
            |> Option.toList
            |> List.collect getKeyValueList
            |> List.filter (fun (key,_) -> Set.contains key disabledSources |> not)
            |> List.map (fun (key,value) -> value, getAuth key)

        { PackageSources = if clearSources then sources else nugetConfig.PackageSources @ sources
          PackageRestoreEnabled = 
            match configNode |> getNode "packageRestore" |> Option.bind (tryGetValue "enabled") with
            | Some value -> bool.Parse(value)
            | None -> nugetConfig.PackageRestoreEnabled
          PackageRestoreAutomatic = 
            match configNode |> getNode "packageRestore" |> Option.bind (tryGetValue "automatic") with
            | Some value -> bool.Parse(value)
            | None -> nugetConfig.PackageRestoreAutomatic }

type NugetEnv = 
    { RootDirectory : DirectoryInfo
      NugetConfig : NugetConfig
      NugetConfigFiles : list<FileInfo>
      NugetProjectFiles : list<ProjectFile * NugetPackagesConfig>
      NugetTargets : option<FileInfo>
      NugetExe : option<FileInfo> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NugetEnv = 
    let create rootDirectory configFiles targets exe config packagesFiles = 
        { RootDirectory = rootDirectory
          NugetConfig = config
          NugetConfigFiles = configFiles
          NugetProjectFiles = packagesFiles
          NugetTargets = targets
          NugetExe = exe
        }
        
    let readNugetConfig(rootDirectory : DirectoryInfo) =
        DirectoryInfo(Path.Combine(rootDirectory.FullName, ".nuget"))
        |> Seq.unfold (fun di -> if di = null 
                                    then None 
                                    else Some(FileInfo(Path.Combine(di.FullName, "nuget.config")), di.Parent)) 
        |> Seq.toList
        |> List.rev
        |> List.append [FileInfo(Path.Combine(Constants.AppDataFolder, "nuget", "nuget.config"))]
        |> List.filter (fun fi -> fi.Exists)
        |> List.fold (fun config file -> 
                        config
                        |> bind (fun config ->
                            file 
                            |> NugetConfig.getConfigNode 
                            |> lift (fun node -> NugetConfig.overrideConfig config node)))
                        (ok NugetConfig.empty)

    let readNugetPackages(rootDirectory : DirectoryInfo) =
        let readSingle(file : FileInfo) = 
            try
                let doc = XmlDocument()
                doc.Load file.FullName
    
                { File = file
                  Type = if file.Directory.Name = ".nuget" then SolutionLevel else ProjectLevel
                  Packages = [for node in doc.SelectNodes("//package") ->
                                    node.Attributes.["id"].Value, node.Attributes.["version"].Value |> SemVer.Parse ]}
                |> ok 
            with _ -> fail (NugetPackagesConfigParseError file)

        ProjectFile.FindAllProjects rootDirectory.FullName 
        |> List.ofArray
        |> List.map (fun p -> p, Path.Combine(Path.GetDirectoryName(p.FileName), "packages.config"))
        |> List.filter (fun (p,packages) -> File.Exists packages)
        |> List.map (fun (p,packages) -> readSingle(FileInfo(packages)) |> lift (fun packages -> (p,packages)))
        |> collect

    let read (rootDirectory : DirectoryInfo) = trial {
        let configs = FindAllFiles(rootDirectory.FullName, "nuget.config") |> Array.toList
        let targets = FindAllFiles(rootDirectory.FullName, "nuget.targets") |> Seq.firstOrDefault
        let exe = FindAllFiles(rootDirectory.FullName, "nuget.exe") |> Seq.firstOrDefault
        let! config = readNugetConfig rootDirectory
        let! packages = readNugetPackages rootDirectory

        return create rootDirectory configs targets exe config packages
    }

type ConvertResultR = 
    { NugetEnv : NugetEnv
      PaketEnv : PaketEnv
      SolutionFiles : list<SolutionFile> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ConvertResultR =
    let create nugetEnv paketEnv solutionFiles = 
        { NugetEnv = nugetEnv
          PaketEnv = paketEnv
          SolutionFiles = solutionFiles }

let createPackageRequirement packageName version sources dependenciesFileName = 
     { Name = PackageName packageName
       VersionRequirement = VersionRequirement(VersionRange.Exactly version, PreReleaseStatus.No)
       Sources = sources
       ResolverStrategy = ResolverStrategy.Max
       Settings = InstallSettings.Default
       Parent = PackageRequirementSource.DependenciesFile dependenciesFileName }

let createDependenciesFileR (rootDirectory : DirectoryInfo) nugetEnv mode =
    
    let dependenciesFileName = Path.Combine(rootDirectory.FullName, Constants.DependenciesFileName)
    
    let allVersions =
        nugetEnv.NugetProjectFiles
        |> Seq.collect (fun (_,c) -> c.Packages)
        |> Seq.groupBy fst
        |> Seq.map (fun (name, packages) -> name, packages |> Seq.map snd |> Seq.distinct)
        |> Seq.sortBy (fun (name,_) -> name.ToLower())
    
    for (name, versions) in allVersions do
        if Seq.length versions > 1 
        then traceWarnfn "Package %s is referenced multiple times in different versions: %A. Paket will choose the latest one." 
                            name    
                            (versions |> Seq.map string |> Seq.toList)
    
    let latestVersions = 
        allVersions
        |> Seq.map (fun (name, versions) -> name, versions |> Seq.max |> string)
        |> Seq.toList

    let packages = 
        match nugetEnv.NugetExe with 
        | Some _ -> ("Nuget.CommandLine","2.8.3") :: latestVersions
        | _ -> latestVersions

    let read() =
        let addPackages dependenciesFile = 
            packages
            |> List.map (fun (name, v) -> PackageName name, v)
            |> List.fold DependenciesFile.add dependenciesFile
        try 
            DependenciesFile.ReadFromFile dependenciesFileName
            |> ok
        with _ -> DependenciesFileParseError (FileInfo(dependenciesFileName)) |> fail
        |> lift addPackages

    let create() =
        let sources = 
            if nugetEnv.NugetConfig.PackageSources = [] then [Constants.DefaultNugetStream,None] else nugetEnv.NugetConfig.PackageSources
            |> List.map (fun (n, auth) -> n, auth |> Option.map (CredsMigrationMode.toAuthentication mode n))
            |> List.map (fun source -> 
                            try source |> PackageSource.Parse |> ok
                            with _ -> source |> fst |> PackageSourceParseError |> fail
                            |> successTee PackageSource.warnIfNoConnection)
                            
            |> collect

        sources
        |> lift (fun sources -> 
            let packages = packages |> List.map (fun (name,v) -> createPackageRequirement name v sources dependenciesFileName)
            Paket.DependenciesFile(dependenciesFileName, InstallOptions.Default, sources, packages, [], []))

    if File.Exists dependenciesFileName then read() else create()

let convertPackagesConfigToReferences projectFileName packagesConfig =     
    let referencesFile = ProjectFile.FindOrCreateReferencesFile(FileInfo projectFileName)

    packagesConfig.Packages
    |> List.map (fst >> PackageName)
    |> List.fold (fun (r : ReferencesFile) packageName -> r.AddNuGetReference(packageName)) 
                 referencesFile

let convertProjects nugetEnv =
    [for project,packagesConfig in nugetEnv.NugetProjectFiles do 
        project.ReplaceNuGetPackagesFile()
        project.RemoveNuGetTargetsEntries()
        yield project, convertPackagesConfigToReferences project.FileName packagesConfig]

let createPaketEnv rootDirectory nugetEnv credsMirationMode = trial {

    let! depFile = createDependenciesFileR rootDirectory nugetEnv credsMirationMode
    return PaketEnv.create rootDirectory depFile None (convertProjects nugetEnv)
}

let updateSolutions (rootDirectory : DirectoryInfo) = 
    let dependenciesFileName = Path.Combine(rootDirectory.FullName, Constants.DependenciesFileName)
    let solutions =
        FindAllFiles(rootDirectory.FullName, "*.sln")
        |> Array.map(fun fi -> SolutionFile(fi.FullName))
        |> Array.toList

    for solution in solutions do
        let dependenciesFileRef = createRelativePath solution.FileName dependenciesFileName
        solution.RemoveNugetEntries()
        solution.AddPaketFolder(dependenciesFileRef, None)

    solutions

let createResult(rootDirectory, nugetEnv, credsMirationMode) = trial {

    let! paketEnv = createPaketEnv rootDirectory nugetEnv credsMirationMode
    return ConvertResultR.create nugetEnv paketEnv (updateSolutions rootDirectory)
}

let convertR rootDirectory force credsMigrationMode = trial {

    let! credsMigrationMode =
        defaultArg 
            (credsMigrationMode |> Option.map CredsMigrationMode.parse)
            (ok Encrypt)

    let! nugetEnv = NugetEnv.read rootDirectory

    let! rootDirectory = 
        if force then ok rootDirectory
        else PaketEnv.ensureNotExists rootDirectory

    return! createResult(rootDirectory, nugetEnv, credsMigrationMode)
}

let replaceNugetWithPaket initAutoRestore installAfter result = 
    
    let remove (fi : FileInfo) = 
        tracefn "Removing %s" fi.FullName
        fi.Delete()

    result.NugetEnv.NugetConfigFiles |> List.iter remove
    result.NugetEnv.NugetProjectFiles |> List.map (fun (_,n) -> n.File) |> List.iter remove
    result.NugetEnv.NugetTargets |> Option.iter remove
    result.NugetEnv.NugetExe 
    |> Option.iter 
            (fun nugetExe -> 
            remove nugetExe
            traceWarnfn "Removed %s and added %s as dependency instead. Please check all paths." 
                nugetExe.FullName "Nuget.CommandLine")

    match result.NugetEnv.NugetTargets ++ result.NugetEnv.NugetExe with
    | Some fi when fi.Directory.EnumerateFileSystemInfos() |> Seq.isEmpty ->
        fi.Directory.Delete()
    | _ -> ()

    result.PaketEnv.DependenciesFile.Save()
    result.PaketEnv.Projects |> List.iter (fun (project, referencesFile) -> 
                                                project.Save()
                                                referencesFile.Save())
    result.SolutionFiles |> List.iter (fun s -> s.Save())

    let autoVSPackageRestore = 
        result.NugetEnv.NugetConfig.PackageRestoreAutomatic &&
        result.NugetEnv.NugetConfig.PackageRestoreEnabled
    
    if initAutoRestore && (autoVSPackageRestore || result.NugetEnv.NugetTargets.IsSome) then 
        VSIntegration.TurnOnAutoRestore result.PaketEnv |> returnOrFail

    if installAfter then
        UpdateProcess.Update(result.PaketEnv.DependenciesFile.FileName,true,true,true)