/// Contains methods for NuGet conversion
module Paket.NuGetConvert

open Paket
open System
open System.IO
open System.Xml
open Paket.Domain
open Paket.Logging
open Paket.Xml
open Paket.PackageSources
open Paket.Requirements
open Chessie.ErrorHandling
open Paket.PackagesConfigFile

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
    Packages: NugetPackage list
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
    { PackageSources : Map<string, string * Auth option>
      PackageRestoreEnabled : bool
      PackageRestoreAutomatic : bool }

    static member empty =
        { PackageSources = Map.empty
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
            |> List.map (fun (key,value) -> key, (String.quoted value, getAuth key))
            |> Map.ofList
        
        { PackageSources = if clearSources then sources
                           else 
                              Map.fold 
                                (fun acc k v -> Map.add k v acc)
                                nugetConfig.PackageSources
                                sources
                           |> Map.filter (fun k _ -> Set.contains k disabledSources |> not)
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
        |> List.unfold (fun di -> if di = null 
                                    then None 
                                    else Some(FileInfo(Path.Combine(di.FullName, "nuget.config")), di.Parent)) 
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
                { File = file
                  Type = if file.Directory.Name = ".nuget" then SolutionLevel else ProjectLevel
                  Packages = PackagesConfigFile.Read file.FullName }
                |> ok 
            with _ -> fail (NugetPackagesConfigParseError file)

        ProjectFile.FindAllProjects rootDirectory.FullName 
        |> Array.map (fun p -> p, Path.Combine(Path.GetDirectoryName(p.FileName), Constants.PackagesConfigFile))
        |> Array.filter (fun (p,packages) -> File.Exists packages)
        |> Array.map (fun (p,packages) -> readSingle(FileInfo(packages)) |> lift (fun packages -> (p,packages)))
        |> collect

    let read (rootDirectory : DirectoryInfo) = trial {
        let configs = FindAllFiles(rootDirectory.FullName, "nuget.config") |> Array.toList
        let targets = FindAllFiles(rootDirectory.FullName, "nuget.targets") |> Array.tryHead
        let exe = FindAllFiles(rootDirectory.FullName, "nuget.exe") |> Array.tryHead
        let! config = readNugetConfig rootDirectory
        let! packages = readNugetPackages rootDirectory

        return create rootDirectory configs targets exe config packages
    }

type ConvertResultR = 
    { NugetEnv : NugetEnv
      PaketEnv : PaketEnv
      SolutionFiles : SolutionFile [] }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ConvertResultR =
    let create nugetEnv paketEnv solutionFiles = 
        { NugetEnv = nugetEnv
          PaketEnv = paketEnv
          SolutionFiles = solutionFiles }

let createPackageRequirement (packageName, version, restrictions) sources dependenciesFileName = 
     { Name = PackageName packageName
       VersionRequirement =
            if version = "" then
                VersionRequirement(VersionRange.Minimum <| SemVer.Parse "0", PreReleaseStatus.No)
            else
                VersionRequirement(VersionRange.Exactly version, PreReleaseStatus.No)
       Sources = sources
       ResolverStrategy = ResolverStrategy.Max
       Settings = { InstallSettings.Default with FrameworkRestrictions = restrictions }
       Parent = PackageRequirementSource.DependenciesFile dependenciesFileName }

let createDependenciesFileR (rootDirectory : DirectoryInfo) nugetEnv mode =
    
    let dependenciesFileName = Path.Combine(rootDirectory.FullName, Constants.DependenciesFileName)

    let allVersionsGroupped =
        nugetEnv.NugetProjectFiles
        |> List.collect (fun (_,c) -> c.Packages)
        |> List.groupBy (fun p -> p.Id)

    let findDistinctPackages selector =
        allVersionsGroupped
        |> List.map (fun (name, packages) -> name, packages |> selector)
        |> List.sortBy (fun (name,_) -> name.ToLower())

    let findWarnings searchBy message =
        for name, versions in findDistinctPackages searchBy do
            if List.length versions > 1 then 
              traceWarnfn message name versions    

    findWarnings (List.map (fun p -> p.Version) >> List.distinct >> List.map string) 
        "Package %s is referenced multiple times in different versions: %A. Paket will choose the latest one." 
    findWarnings (List.map (fun p -> p.TargetFramework) >> List.distinct >> List.choose (fun target -> target) >> List.map string) 
        "Package %s is referenced multiple times with different target frameworks : %A. Paket may disregard target framework."
    
    let latestVersions = 
        findDistinctPackages (List.map (fun p -> p.Version, p.TargetFramework) >> List.distinct)
        |> List.map (fun (name, versions) ->
            let latestVersion, _ = versions |> List.maxBy fst
            let restrictions =
                match versions with
                | [ version, targetFramework ] -> targetFramework |> Option.toList |> List.collect Requirements.parseRestrictions 
                | _ -> []
            name, string latestVersion, restrictions)

    let packages = 
        match nugetEnv.NugetExe with 
        | Some _ -> ("Nuget.CommandLine","",[]) :: latestVersions
        | _ -> latestVersions

    let read() =
        let addPackages dependenciesFile = 
            packages
            |> List.map (fun (name, v, restrictions) -> Constants.MainDependencyGroup, PackageName name, v, { InstallSettings.Default with FrameworkRestrictions = restrictions})
            |> List.fold (fun (dependenciesFile:DependenciesFile) (groupName, packageName,version,installSettings) -> dependenciesFile.Add(groupName, packageName,version,installSettings)) dependenciesFile
        try 
            DependenciesFile.ReadFromFile dependenciesFileName
            |> ok
        with _ -> DependenciesFileParseError (FileInfo dependenciesFileName) |> fail
        |> lift addPackages

    let create() =
        let sources = 
            if nugetEnv.NugetConfig.PackageSources = Map.empty then [ Constants.DefaultNugetStream, None ]
            else 
                (nugetEnv.NugetConfig.PackageSources
                 |> Map.toList
                 |> List.map snd)
            |> List.map (fun (n, auth) -> n, auth |> Option.map (CredsMigrationMode.toAuthentication mode n))
            |> List.map (fun source -> 
                            try source |> PackageSource.Parse |> ok
                            with _ -> source |> fst |> PackageSourceParseError |> fail
                            |> successTee PackageSource.WarnIfNoConnection)
                            
            |> collect

        sources
        |> lift (fun sources -> 
            let sourceLines = sources |> List.map (fun s -> DependenciesFileSerializer.sourceString(s.ToString()))
            let packageLines = 
                packages 
                |> List.map (fun (name,v,restr) -> 
                    let vr = createPackageRequirement (name, v, restr) sources dependenciesFileName
                    DependenciesFileSerializer.packageString vr.Name vr.VersionRequirement vr.ResolverStrategy vr.Settings)

            let newLines = sourceLines @ [""] @ packageLines |> Seq.toArray

            Paket.DependenciesFile(DependenciesFileParser.parseDependenciesFile dependenciesFileName newLines))

    if File.Exists dependenciesFileName then read() else create()

let convertPackagesConfigToReferences projectFileName packagesConfig =     
    let referencesFile = ProjectFile.FindOrCreateReferencesFile(FileInfo projectFileName)

    packagesConfig.Packages
    |> List.map ((fun p -> p.Id) >> PackageName)
    |> List.fold (fun (r : ReferencesFile) packageName -> r.AddNuGetReference(Constants.MainDependencyGroup,packageName)) 
                 referencesFile

let convertProjects nugetEnv =
    [for project,packagesConfig in nugetEnv.NugetProjectFiles do 
        project.ReplaceNuGetPackagesFile()
        project.RemoveNuGetTargetsEntries()
        project.RemoveImportAndTargetEntries(packagesConfig.Packages |> List.map (fun p -> p.Id, p.Version))
        yield project, convertPackagesConfigToReferences project.FileName packagesConfig]

let createPaketEnv rootDirectory nugetEnv credsMirationMode = trial {

    let! depFile = createDependenciesFileR rootDirectory nugetEnv credsMirationMode
    return PaketEnv.create rootDirectory depFile None (convertProjects nugetEnv)
}

let updateSolutions (rootDirectory : DirectoryInfo) = 
    let dependenciesFileName = Path.Combine(rootDirectory.FullName, Constants.DependenciesFileName)
    let solutions =
        FindAllFiles(rootDirectory.FullName, "*.sln")
        |> Array.map (fun fi -> SolutionFile(fi.FullName))

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
    result.SolutionFiles |> Array.iter (fun s -> s.Save())

    let autoVSPackageRestore = 
        result.NugetEnv.NugetConfig.PackageRestoreAutomatic &&
        result.NugetEnv.NugetConfig.PackageRestoreEnabled
    
    if initAutoRestore && (autoVSPackageRestore || result.NugetEnv.NugetTargets.IsSome) then 
        VSIntegration.TurnOnAutoRestore result.PaketEnv |> returnOrFail

    if installAfter then
        UpdateProcess.Update(
            result.PaketEnv.DependenciesFile.FileName,
            { UpdaterOptions.Default with Common = { InstallerOptions.Default with Force = true; Hard = true; Redirects = true }})
