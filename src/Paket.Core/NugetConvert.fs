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
open Rop

type CredsMigrationMode =
    | Encrypt
    | Plaintext
    | Selective

    static member parse(s : string) = 
        match s with 
        | "encrypt" -> Rop.succeed Encrypt
        | "plaintext" -> Rop.succeed  Plaintext
        | "selective" -> Rop.succeed Selective
        | _ ->  InvalidCredentialsMigrationMode s |> Rop.fail

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
            (doc |> getNode "configuration").Value |> Rop.succeed
        with _ -> 
            file
            |> NugetConfigFileParseError
            |> Rop.fail

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

        let sources = 
            configNode |> getNode "packageSources"
            |> Option.toList
            |> List.collect getKeyValueList
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
                        |> Rop.bind (fun config ->
                            file 
                            |> NugetConfig.getConfigNode 
                            |> Rop.lift (fun node -> NugetConfig.overrideConfig config node)))
                        (Rop.succeed NugetConfig.empty)

    let readNugetPackages(rootDirectory : DirectoryInfo) =
        let readSingle(file : FileInfo) = 
            try
                let doc = XmlDocument()
                doc.Load file.FullName
    
                { File = file
                  Type = if file.Directory.Name = ".nuget" then SolutionLevel else ProjectLevel
                  Packages = [for node in doc.SelectNodes("//package") ->
                                    node.Attributes.["id"].Value, node.Attributes.["version"].Value |> SemVer.Parse ]}
                |> Rop.succeed 
            with _ -> Rop.fail (NugetPackagesConfigParseError file)

        ProjectFile.FindAllProjects rootDirectory.FullName 
        |> List.ofArray
        |> List.map (fun p -> p, Path.Combine(Path.GetDirectoryName(p.FileName), "packages.config"))
        |> List.filter (fun (p,packages) -> File.Exists packages)
        |> List.map (fun (p,packages) -> readSingle(FileInfo(packages)) |> lift (fun packages -> (p,packages)))
        |> Rop.collect

    let read (rootDirectory : DirectoryInfo) = 
        let configs = FindAllFiles(rootDirectory.FullName, "nuget.config") |> Array.toList 
        let targets = FindAllFiles(rootDirectory.FullName, "nuget.targets") |> Seq.firstOrDefault
        let exe = FindAllFiles(rootDirectory.FullName, "nuget.exe") |> Seq.firstOrDefault

        create rootDirectory configs targets exe
        <!> readNugetConfig rootDirectory
        <*> readNugetPackages rootDirectory


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

let ensureNoPaketEnv rootDirectory =
    match PaketEnv.fromRootDirectory rootDirectory with
    | Success(_) -> fail (PaketEnvAlreadyExistsInDirectory rootDirectory)
    | Failure(msgs) -> 
        let filtered = 
            msgs
            |> List.filter (function
                | DependenciesFileNotFoundInDir _ -> false
                | _ -> true )
        if filtered |> List.isEmpty then succeed rootDirectory
        else Failure(filtered)

let createPackageRequirement packageName version sources dependenciesFileName = 
     { Name = PackageName packageName
       VersionRequirement = VersionRequirement(VersionRange.Exactly version, PreReleaseStatus.No)
       Sources = sources
       ResolverStrategy = Max
       FrameworkRestrictions = []
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
            |> Rop.succeed
        with _ -> DependenciesFileParseError (FileInfo(dependenciesFileName)) |> Rop.fail
        |> lift addPackages

    let create() =
        let sources = 
            if nugetEnv.NugetConfig.PackageSources = [] then [Constants.DefaultNugetStream,None] else nugetEnv.NugetConfig.PackageSources
            |> List.map (fun (n, auth) -> n, auth |> Option.map (CredsMigrationMode.toAuthentication mode n))
            |> List.map (fun source -> 
                            try source |> PackageSource.Parse |> Rop.succeed
                            with _ -> source |> fst |> PackageSourceParseError |> Rop.fail)
            |> Rop.collect

        sources
        |> Rop.lift (fun sources -> 
            let packages = packages |> List.map (fun (name,v) -> createPackageRequirement name v sources dependenciesFileName)
            Paket.DependenciesFile(dependenciesFileName, InstallOptions.Default, sources, packages, []))

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
        yield project, convertPackagesConfigToReferences project.FileName packagesConfig] |> succeed

let createPaketEnv rootDirectory nugetEnv credsMirationMode = 
    
    PaketEnv.create rootDirectory
    <!> createDependenciesFileR rootDirectory nugetEnv credsMirationMode
    <*> succeed None
    <*> convertProjects nugetEnv

let updateSolutions (rootDirectory : DirectoryInfo) = 
    let dependenciesFileName = Path.Combine(rootDirectory.FullName, Constants.DependenciesFileName)
    let root = Path.GetDirectoryName dependenciesFileName
    let solutions =
        FindAllFiles(rootDirectory.FullName, "*.sln")
        |> Array.map(fun fi -> SolutionFile(fi.FullName))
        |> Array.toList

    for solution in solutions do
        let dependenciesFileRef = createRelativePath solution.FileName dependenciesFileName
        solution.RemoveNugetEntries()
        solution.AddPaketFolder(dependenciesFileRef, None)

    solutions |> succeed

let createResult(rootDirectory, nugetEnv, credsMirationMode) =

    ConvertResultR.create nugetEnv
    <!> createPaketEnv rootDirectory nugetEnv credsMirationMode
    <*> updateSolutions rootDirectory

let convertR rootDirectory force credsMigrationMode  =

    let credsMigrationMode =
        defaultArg 
            (credsMigrationMode |> Option.map CredsMigrationMode.parse)
            (Rop.succeed Encrypt)
    
    let nugetEnv = NugetEnv.read rootDirectory

    let rootDirectory = 
        if force then succeed rootDirectory
        else ensureNoPaketEnv rootDirectory

    let triple x y z = x,y,z

    triple
    <!> rootDirectory
    <*> nugetEnv
    <*> credsMigrationMode
    >>= createResult

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

    match result.NugetEnv.NugetTargets |> orElse result.NugetEnv.NugetExe with
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
    
    let dependenciesFileName = result.PaketEnv.DependenciesFile.FileName
    if initAutoRestore && (autoVSPackageRestore || result.NugetEnv.NugetTargets.IsSome) then 
        VSIntegration.InitAutoRestore dependenciesFileName

    if installAfter then
        UpdateProcess.Update(dependenciesFileName,true,true,true)