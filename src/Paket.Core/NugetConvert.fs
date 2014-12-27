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

type ConvertMessage =
    | UnknownCredentialsMigrationMode of string
    | NugetPackagesConfigParseError of FileInfo
    | NugetConfigFileParseError  of FileInfo
    | DependenciesFileAlreadyExists of FileInfo
    | ReferencesFileAlreadyExists of FileInfo
    | DependenciesFileParseError of string
    | PackageSourceParseError of string

type CredsMigrationMode =
    | Encrypt
    | Plaintext
    | Selective

    static member parse(s : string) = 
        match s with 
        | "encrypt" -> Rop.succeed Encrypt
        | "plaintext" -> Rop.succeed  Plaintext
        | "selective" -> Rop.succeed Selective
        | _ ->  UnknownCredentialsMigrationMode s |> Rop.failure

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
            |> Rop.failure

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

type ConvertResult = 
    { DependenciesFile : DependenciesFile
      ReferencesFiles : list<ReferencesFile>
      ProjectFiles : list<ProjectFile>
      SolutionFiles : list<SolutionFile>
      NugetConfig : NugetConfig
      NugetConfigFiles : list<FileInfo>
      NugetPackagesFiles : list<NugetPackagesConfig>
      Force : bool
      CredsMigrationMode: CredsMigrationMode
      NugetTargets : option<FileInfo>
      NugetExe : option<FileInfo> }
    
    static member empty(dependenciesFileName, force, credsMigrationMode) = 
        { DependenciesFile = 
            Paket.DependenciesFile(
                dependenciesFileName, 
                InstallOptions.Default, 
                [],
                [],
                [])
          ReferencesFiles = []
          ProjectFiles = []
          SolutionFiles = []
          NugetConfig = NugetConfig.empty
          NugetConfigFiles = []
          NugetPackagesFiles = [] 
          Force = force
          CredsMigrationMode = credsMigrationMode
          NugetTargets = None
          NugetExe = None }

let readNugetConfig(convertResult) =
    let root = Path.GetDirectoryName(convertResult.DependenciesFile.FileName)
    
    DirectoryInfo(Path.Combine(root, ".nuget"))
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
    |> Rop.lift (fun config -> {convertResult with NugetConfig = config})
                     
let readNugetPackages(convertResult) =
    let readSingle(file : FileInfo) = 
        try
            let doc = XmlDocument()
            doc.Load file.FullName
    
            { File = file
              Type = if file.Directory.Name = ".nuget" then SolutionLevel else ProjectLevel
              Packages = [for node in doc.SelectNodes("//package") ->
                                node.Attributes.["id"].Value, node.Attributes.["version"].Value |> SemVer.Parse ]}
            |> Rop.succeed 
        with _ -> Rop.failure (NugetPackagesConfigParseError file)

    FindAllFiles(Path.GetDirectoryName convertResult.DependenciesFile.FileName, "packages.config")
    |> Array.map readSingle
    |> Rop.collect
    |> Rop.lift (fun xs -> {convertResult with NugetPackagesFiles = xs})

let collectNugetConfigs(convertResult) = 
    let nugetConfigs = 
        FindAllFiles(Path.GetDirectoryName convertResult.DependenciesFile.FileName, "nuget.config") 
        |> Array.toList

    Rop.succeed {convertResult with NugetConfigFiles = nugetConfigs}

let ensureNotAlreadyConverted(convertResult) =
    if convertResult.Force then Rop.succeed convertResult
    else 
        let depFile = 
            if File.Exists(convertResult.DependenciesFile.FileName) then 
                Rop.failure (DependenciesFileAlreadyExists(FileInfo(convertResult.DependenciesFile.FileName)))
            else Rop.succeed()

        let refFiles =
            convertResult.NugetPackagesFiles
            |> List.map (fun fi -> Path.Combine(fi.File.Directory.Name, Constants.ReferencesFile))
            |> List.map (fun r -> 
                   if File.Exists(r) then Rop.failure (ReferencesFileAlreadyExists <| FileInfo(r))
                   else Rop.succeed ())
            |> Rop.collect

        depFile 
        |> Rop.bind(fun _ -> 
            refFiles 
            |> Rop.bind (fun _ -> Rop.succeed convertResult))

let createDependenciesFile(convertResult) =
    
    let dependenciesFileName = convertResult.DependenciesFile.FileName
    let root = Path.GetDirectoryName dependenciesFileName
    
    let allVersions =
        convertResult.NugetPackagesFiles
        |> Seq.collect (fun c -> c.Packages)
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

    let nugetTargets = FindAllFiles(root, "nuget.targets") |> Seq.firstOrDefault
    let nugetExe = FindAllFiles(root, "nuget.exe") |> Seq.firstOrDefault

    let packages = 
        match nugetExe with 
        | Some _ -> ("Nuget.CommandLine","") :: latestVersions
        | _ -> latestVersions

    let addPackages dependenciesFile = 
        packages
        |> List.map (fun (name, v) -> PackageName name, v)
        |> List.fold DependenciesFile.add dependenciesFile

    let read() =
        try DependenciesFile.ReadFromFile dependenciesFileName |> Rop.succeed
        with _ -> DependenciesFileParseError dependenciesFileName |> Rop.failure

    let create() =
        let mode = convertResult.CredsMigrationMode
        let sources = 
            convertResult.NugetConfig.PackageSources
            |> List.map (fun (n, auth) -> n, auth |> Option.map (CredsMigrationMode.toAuthentication mode n))
            |> List.map (fun source -> 
                            try source |> PackageSource.Parse |> Rop.succeed
                            with _ -> source |> fst |> PackageSourceParseError |> Rop.failure)
            |> Rop.collect

        sources
        |> Rop.lift (fun sources -> 
            Paket.DependenciesFile(dependenciesFileName, InstallOptions.Default, sources, [], []))

    if File.Exists dependenciesFileName then read() else create()
    |> Rop.lift (fun dependenciesFile -> 
           { convertResult with DependenciesFile = addPackages dependenciesFile
                                NugetTargets = nugetTargets
                                NugetExe = nugetExe })

let createReferencesFiles(convertResult) =
    let createSingle packagesConfig = 
        let fileName = Path.Combine(packagesConfig.File.Directory.Name, Constants.ReferencesFile)
        packagesConfig.Packages
        |> List.map (fst >> PackageName)
        |> List.fold (fun (r : ReferencesFile) packageName -> r.AddNuGetReference(packageName)) 
                     (ReferencesFile.New(fileName))

    let referencesFiles = 
        convertResult.NugetPackagesFiles 
        |> List.filter (fun c -> c.Type <> SolutionLevel)
        |> List.map createSingle

    Rop.succeed {convertResult with ReferencesFiles = referencesFiles}
   
let convertSolutions(convertResult) = 
    let dependenciesFileName = convertResult.DependenciesFile.FileName
    let root = Path.GetDirectoryName dependenciesFileName
    let solutions =
        FindAllFiles(root, "*.sln")
        |> Array.map(fun fi -> SolutionFile(fi.FullName))
        |> Array.toList

    for solution in solutions do
        let dependenciesFileRef = createRelativePath solution.FileName dependenciesFileName
        solution.RemoveNugetEntries()
        solution.AddPaketFolder(dependenciesFileRef, None)

    Rop.succeed {convertResult with SolutionFiles = solutions}

let convertProjects(convertResult) = 
    let dependenciesFileName = convertResult.DependenciesFile.FileName
    let root = Path.GetDirectoryName dependenciesFileName
    let projects = ProjectFile.FindAllProjects root |> Array.toList
    for project in projects do 
        project.ReplaceNuGetPackagesFile()
        project.RemoveNuGetTargetsEntries()

    Rop.succeed {convertResult with ProjectFiles = projects}
    
let convert(dependenciesFileName, force, credsMigrationMode) =
    let credsMigrationMode =
        defaultArg 
            (credsMigrationMode |> Option.map CredsMigrationMode.parse)
            (Rop.succeed Encrypt)
    
    credsMigrationMode 
    |> Rop.bind (fun mode -> ConvertResult.empty(dependenciesFileName, force, mode) |> Rop.succeed)
    |> Rop.bind readNugetConfig
    |> Rop.bind readNugetPackages
    |> Rop.bind collectNugetConfigs
    |> Rop.bind ensureNotAlreadyConverted
    |> Rop.bind createDependenciesFile
    |> Rop.bind createReferencesFiles
    |> Rop.bind convertSolutions
    |> Rop.bind convertProjects
