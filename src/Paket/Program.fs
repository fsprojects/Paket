/// [omit]
module Paket.Program

open System
open System.Diagnostics
open System.Reflection
open System.IO

open Paket.Logging
open Paket.Commands
open Paket.Releases

open Argu
open PackageSources

let private stopWatch = new Stopwatch()
stopWatch.Start()

type PaketExiter() =
    interface IExiter with
        member __.Name = "paket exiter"
        member __.Exit (msg,code) =
            if code = ErrorCode.HelpText then 
                tracen msg ; exit 0
            else traceError msg ; exit 1

let processWithValidation silent validateF commandF (result : ParseResults<'T>) =
    if not <| validateF result then
        traceError "Command was:"
        traceError ("  " + String.Join(" ",Environment.GetCommandLineArgs()))
        result.Parser.PrintUsage() |> traceError
        Environment.ExitCode <- 1
    else
        commandF result
        let elapsedTime = Utils.TimeSpanToReadableString stopWatch.Elapsed
        if not silent then
            tracefn "%s - ready." elapsedTime

let processCommand silent commandF result =
    processWithValidation silent (fun _ -> true) commandF result

let add (results : ParseResults<_>) =
    let packageName = results.GetResult <@ AddArgs.Nuget @>
    let version = defaultArg (results.TryGetResult <@ AddArgs.Version @>) ""
    let force = results.Contains <@ AddArgs.Force @>
    let redirects = results.Contains <@ AddArgs.Redirects @>
    let createNewBindingFiles = results.Contains <@ AddArgs.CreateNewBindingFiles @>
    let cleanBindingRedirects = results.Contains <@ AddArgs.Clean_Redirects @>
    let group = results.TryGetResult <@ AddArgs.Group @>
    let noInstall = results.Contains <@ AddArgs.No_Install @>
    let semVerUpdateMode =
        if results.Contains <@ AddArgs.Keep_Patch @> then SemVerUpdateMode.KeepPatch else
        if results.Contains <@ AddArgs.Keep_Minor @> then SemVerUpdateMode.KeepMinor else
        if results.Contains <@ AddArgs.Keep_Major @> then SemVerUpdateMode.KeepMajor else
        SemVerUpdateMode.NoRestriction
    let touchAffectedRefs = results.Contains <@ AddArgs.Touch_Affected_Refs @>

    match results.TryGetResult <@ AddArgs.Project @> with
    | Some projectName ->
        Dependencies.Locate().AddToProject(group, packageName, version, force, redirects, cleanBindingRedirects, createNewBindingFiles, projectName, noInstall |> not, semVerUpdateMode, touchAffectedRefs)
    | None ->
        let interactive = results.Contains <@ AddArgs.Interactive @>
        Dependencies.Locate().Add(group, packageName, version, force, redirects, cleanBindingRedirects, createNewBindingFiles, interactive, noInstall |> not, semVerUpdateMode, touchAffectedRefs)

let validateConfig (results : ParseResults<_>) =
    let credential = results.Contains <@ ConfigArgs.AddCredentials @>
    let token = results.Contains <@ ConfigArgs.AddToken @>
    match credential, token with
    | true, _ -> results.GetResults <@ ConfigArgs.AddCredentials @> |> List.isEmpty |> not
    | _, true -> results.GetResults <@ ConfigArgs.AddToken @> |> List.isEmpty |> not
    | _ -> false

let config (results : ParseResults<_>) =
    let credentials = results.Contains <@ ConfigArgs.AddCredentials @>
    let token = results.Contains <@ ConfigArgs.AddToken @>
    match credentials, token with
    | true, _ -> 
      let args = results.GetResults <@ ConfigArgs.AddCredentials @>
      let source = args.Item 0
      let username, password = results.GetResult (<@ ConfigArgs.Username @>, ""), results.GetResult (<@ ConfigArgs.Password @>, "") 
            
      Dependencies(".").AddCredentials(source, username, password)
    | _, true ->
      let args = results.GetResults <@ ConfigArgs.AddToken @>
      let source, token = args.Item 0
      Dependencies(".").AddToken(source, token)
    | _ -> ()

let validateAutoRestore (results : ParseResults<_>) =
    results.GetAllResults().Length = 1

let autoRestore (results : ParseResults<_>) =
    match results.GetAllResults() with
    | [On] -> Dependencies.Locate().TurnOnAutoRestore()
    | [Off] -> Dependencies.Locate().TurnOffAutoRestore()
    | _ -> failwith "expected only one argument"

let convert (results : ParseResults<_>) =
    let force = results.Contains <@ ConvertFromNugetArgs.Force @>
    let noInstall = results.Contains <@ ConvertFromNugetArgs.No_Install @>
    let noAutoRestore = results.Contains <@ ConvertFromNugetArgs.No_Auto_Restore @>
    let credsMigrationMode = results.TryGetResult <@ ConvertFromNugetArgs.Creds_Migration @>
    Dependencies.ConvertFromNuget(force, noInstall |> not, noAutoRestore |> not, credsMigrationMode)

let findRefs (results : ParseResults<_>) =
    let packages = results.GetResult <@ FindRefsArgs.Packages @>
    let group = defaultArg (results.TryGetResult <@ FindRefsArgs.Group @>) (Constants.MainDependencyGroup.ToString())
    packages |> List.map (fun p -> group,p)
    |> Dependencies.Locate().ShowReferencesFor

let init (results : ParseResults<InitArgs>) =
    Dependencies.Init()
    Dependencies.Locate().DownloadLatestBootstrapper()

let clearCache (results : ParseResults<ClearCacheArgs>) =
    Dependencies.ClearCache()

let install (results : ParseResults<_>) =
    let force = results.Contains <@ InstallArgs.Force @>
    let withBindingRedirects = results.Contains <@ InstallArgs.Redirects @>
    let createNewBindingFiles = results.Contains <@ InstallArgs.CreateNewBindingFiles @>
    let cleanBindingRedirects = results.Contains <@ InstallArgs.Clean_Redirects @>
    let installOnlyReferenced = results.Contains <@ InstallArgs.Install_Only_Referenced @>
    let semVerUpdateMode =
        if results.Contains <@ InstallArgs.Keep_Patch @> then SemVerUpdateMode.KeepPatch else
        if results.Contains <@ InstallArgs.Keep_Minor @> then SemVerUpdateMode.KeepMinor else
        if results.Contains <@ InstallArgs.Keep_Major @> then SemVerUpdateMode.KeepMajor else
        SemVerUpdateMode.NoRestriction
    let touchAffectedRefs = results.Contains <@ InstallArgs.Touch_Affected_Refs @>

    Dependencies.Locate().Install(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, installOnlyReferenced, semVerUpdateMode, touchAffectedRefs)

let outdated (results : ParseResults<_>) =
    let strict = results.Contains <@ OutdatedArgs.Ignore_Constraints @> |> not
    let includePrereleases = results.Contains <@ OutdatedArgs.Include_Prereleases @>
    Dependencies.Locate().ShowOutdated(strict, includePrereleases)

let remove (results : ParseResults<_>) =
    let packageName = results.GetResult <@ RemoveArgs.Nuget @>
    let force = results.Contains <@ RemoveArgs.Force @>
    let noInstall = results.Contains <@ RemoveArgs.No_Install @>
    let group = results.TryGetResult <@ RemoveArgs.Group @>
    match results.TryGetResult <@ RemoveArgs.Project @> with
    | Some projectName ->
        Dependencies.Locate()
                    .RemoveFromProject(group, packageName, force, projectName, noInstall |> not)
    | None ->
        let interactive = results.Contains <@ RemoveArgs.Interactive @>
        Dependencies.Locate().Remove(group, packageName, force, interactive, noInstall |> not)

let restore (results : ParseResults<_>) =
    let force = results.Contains <@ RestoreArgs.Force @>
    let files = results.GetResult (<@ RestoreArgs.References_Files @>, defaultValue = [])
    let group = results.TryGetResult <@ RestoreArgs.Group @>
    let installOnlyReferenced = results.Contains <@ RestoreArgs.Install_Only_Referenced @>
    let touchAffectedRefs = results.Contains <@ RestoreArgs.Touch_Affected_Refs @>
    let ignoreChecks = results.Contains <@ RestoreArgs.Ignore_Checks @>
    if List.isEmpty files then Dependencies.Locate().Restore(force, group, installOnlyReferenced, touchAffectedRefs, ignoreChecks)
    else Dependencies.Locate().Restore(force, group, files, touchAffectedRefs, ignoreChecks)

let simplify (results : ParseResults<_>) =
    let interactive = results.Contains <@ SimplifyArgs.Interactive @>
    Dependencies.Locate().Simplify(interactive)

let update (results : ParseResults<_>) =
    let force = results.Contains <@ UpdateArgs.Force @>
    let noInstall = results.Contains <@ UpdateArgs.No_Install @>
    let group = results.TryGetResult <@ UpdateArgs.Group @>
    let withBindingRedirects = results.Contains <@ UpdateArgs.Redirects @>
    let cleanBindingRedirects = results.Contains <@ UpdateArgs.Clean_Redirects @>
    let createNewBindingFiles = results.Contains <@ UpdateArgs.CreateNewBindingFiles @>
    let semVerUpdateMode =
        if results.Contains <@ UpdateArgs.Keep_Patch @> then SemVerUpdateMode.KeepPatch else
        if results.Contains <@ UpdateArgs.Keep_Minor @> then SemVerUpdateMode.KeepMinor else
        if results.Contains <@ UpdateArgs.Keep_Major @> then SemVerUpdateMode.KeepMajor else
        SemVerUpdateMode.NoRestriction
    let touchAffectedRefs = results.Contains <@ UpdateArgs.Touch_Affected_Refs @>
    let filter = results.Contains <@ UpdateArgs.Filter @>

    match results.TryGetResult <@ UpdateArgs.Nuget @> with
    | Some packageName ->
        let version = results.TryGetResult <@ UpdateArgs.Version @>
        if filter then
            Dependencies.Locate().UpdateFilteredPackages(group, packageName, version, force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, noInstall |> not, semVerUpdateMode, touchAffectedRefs)
        else
            Dependencies.Locate().UpdatePackage(group, packageName, version, force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, noInstall |> not, semVerUpdateMode, touchAffectedRefs)
    | _ ->
        match group with
        | Some groupName -> 
            Dependencies.Locate().UpdateGroup(groupName, force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, noInstall |> not, semVerUpdateMode, touchAffectedRefs)
        | None ->
            Dependencies.Locate().Update(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, noInstall |> not, semVerUpdateMode, touchAffectedRefs)

let pack (results : ParseResults<_>) =
    let outputPath = results.GetResult <@ PackArgs.Output @>
    Dependencies.Locate()
                .Pack(outputPath,
                      ?buildConfig = results.TryGetResult <@ PackArgs.BuildConfig @>,
                      ?buildPlatform = results.TryGetResult <@ PackArgs.BuildPlatform @>,
                      ?version = results.TryGetResult <@ PackArgs.Version @>,
                      specificVersions = results.GetResults <@ PackArgs.SpecificVersion @>,
                      ?releaseNotes = results.TryGetResult <@ PackArgs.ReleaseNotes @>,
                      ?templateFile = results.TryGetResult <@ PackArgs.TemplateFile @>,
                      excludedTemplates = results.GetResults <@ PackArgs.ExcludedTemplate @>,
                      workingDir = Environment.CurrentDirectory,
                      lockDependencies = results.Contains <@ PackArgs.LockDependencies @>,
                      minimumFromLockFile = results.Contains <@ PackArgs.LockDependenciesToMinimum @>,
                      symbols = results.Contains <@ PackArgs.Symbols @>,
                      includeReferencedProjects = results.Contains <@ PackArgs.IncludeReferencedProjects @>,
                      ?projectUrl = results.TryGetResult <@ PackArgs.ProjectUrl @>)

let findPackages silent (results : ParseResults<_>) =
    let maxResults = defaultArg (results.TryGetResult <@ FindPackagesArgs.MaxResults @>) 10000
    let sources  =
        match results.TryGetResult <@ FindPackagesArgs.Source @> with
        | Some source -> [PackageSource.NuGetV2Source source]
        | _ -> PackageSources.DefaultNuGetSource :: 
                (Dependencies.Locate().GetSources() |> Seq.map (fun kv -> kv.Value) |> List.concat)

    let searchAndPrint searchText =
        for p in Dependencies.FindPackagesByName(sources,searchText,maxResults) do
            tracefn "%s" p

    match results.TryGetResult <@ FindPackagesArgs.SearchText @> with
    | None ->
        let searchText = ref ""
        while !searchText <> ":q" do
            if not silent then
                tracefn " - Please enter search text (:q for exit):"
            searchText := Console.ReadLine()
            searchAndPrint !searchText

    | Some searchText -> searchAndPrint searchText

// separated out from showInstalledPackages to allow Paket.PowerShell to get the types
let getInstalledPackages (results : ParseResults<_>) =
    let project = results.TryGetResult <@ ShowInstalledPackagesArgs.Project @>
    let showAll = results.Contains <@ ShowInstalledPackagesArgs.All @>
    let dependenciesFile = Dependencies.Locate()
    match project with
    | None ->
        if showAll then dependenciesFile.GetInstalledPackages()
        else dependenciesFile.GetDirectDependencies()
    | Some project ->
        match ProjectFile.FindReferencesFile(FileInfo project) with
        | None -> []
        | Some referencesFile ->
            let referencesFile = ReferencesFile.FromFile referencesFile
            if showAll then dependenciesFile.GetInstalledPackages(referencesFile)
            else dependenciesFile.GetDirectDependencies(referencesFile)

let showInstalledPackages (results : ParseResults<_>) =
    for groupName,name,version in getInstalledPackages results do
        tracefn "%s %s - %s" groupName name version

let showGroups (results : ParseResults<ShowGroupsArgs>) =
    let dependenciesFile = Dependencies.Locate()
    for groupName in dependenciesFile.GetGroups() do
        tracefn "%s" groupName

let findPackageVersions (results : ParseResults<_>) =
    let maxResults = defaultArg (results.TryGetResult <@ FindPackageVersionsArgs.MaxResults @>) 10000
    let dependencies = Dependencies.Locate()
    let name = 
        match results.TryGetResult <@ FindPackageVersionsArgs.NuGet @> with
        | Some name -> name
        | None -> results.GetResult <@ FindPackageVersionsArgs.Name @>
    let sources  =
        match results.TryGetResult <@ FindPackageVersionsArgs.Source @> with
        | Some source -> [PackageSource.NuGetV2Source source]
        | _ -> dependencies.GetSources() |> Seq.map (fun kv -> kv.Value) |> List.concat

    for p in dependencies.FindPackageVersions(sources,name,maxResults) do
        tracefn "%s" p

let push (results : ParseResults<_>) =
    let fileName = results.GetResult <@ PushArgs.FileName @>
    Dependencies.Push(fileName, ?url = results.TryGetResult <@ PushArgs.Url @>,
                      ?endPoint = results.TryGetResult <@ PushArgs.EndPoint @>,
                      ?apiKey = results.TryGetResult <@ PushArgs.ApiKey @>)

let generateIncludeScripts (results : ParseResults<GenerateIncludeScriptsArgs>) =
    
    let providedFrameworks = results.GetResults <@ GenerateIncludeScriptsArgs.Framework @>
    let providedScriptTypes = results.GetResults <@ GenerateIncludeScriptsArgs.ScriptType @>
    
    let dependencies = 
        Dependencies.Locate()
        |> fun d -> DependenciesFile.ReadFromFile(d.DependenciesFile)
        |> Paket.UpdateProcess.detectProjectFrameworksForDependenciesFile
    
    let rootFolder = DirectoryInfo(dependencies.RootPath)
    
    let frameworksForDependencyGroups = lazy (
        dependencies.Groups
            |> Seq.map (fun f -> f.Value.Options.Settings.FrameworkRestrictions)
            |> Seq.map(function 
                | Paket.Requirements.AutoDetectFramework -> failwithf "couldn't detect framework"
                | Paket.Requirements.FrameworkRestrictionList list ->
                  list |> Seq.collect (
                    function
                    | Paket.Requirements.FrameworkRestriction.Exactly framework
                    | Paket.Requirements.FrameworkRestriction.AtLeast framework -> Seq.singleton framework
                    | Paket.Requirements.FrameworkRestriction.Between (bottom,top) -> [bottom; top] |> Seq.ofList //TODO: do we need to cap the list of generated frameworks based on this? also see todo in Requirements.fs for potential generation of range for 'between'
                    | Paket.Requirements.FrameworkRestriction.Portable portable -> failwithf "unhandled portable framework %s" portable
                  )
              )
            |> Seq.concat
    )

    let environmentFramework = lazy (
        // HACK: resolve .net version based on environment
        // list of match is incomplete / inaccurate
        let version = Environment.Version
        match version.Major, version.Minor, version.Build, version.Revision with
        | 4, 0, 30319, 42000 -> DotNetFramework (FrameworkVersion.V4_6)
        | 4, 0, 30319, _ -> DotNetFramework (FrameworkVersion.V4_5)
        | _ -> DotNetFramework (FrameworkVersion.V4_5) // paket.exe is compiled for framework 4.5
    )
    let tupleMap f v = (v, f v)
    let failOnMismatch toParse parsed f message =
        if List.length toParse <> List.length parsed then
            toParse
            |> Seq.map (tupleMap f)
            |> Seq.filter (snd >> Option.isNone)
            |> Seq.map fst
            |> String.concat ", "
            |> sprintf "%s: %s. Cannot generate include scripts." message
            |> failwith

    let frameworksToGenerate =
        let targetFrameworkList = providedFrameworks |> List.choose FrameworkDetection.Extract
        
        failOnMismatch providedFrameworks targetFrameworkList FrameworkDetection.Extract "Unrecognized Framework(s)"
        
        if targetFrameworkList |> Seq.isEmpty |> not then targetFrameworkList |> Seq.ofList
        else if frameworksForDependencyGroups.Value |> Seq.isEmpty |> not then frameworksForDependencyGroups.Value 
        else Seq.singleton environmentFramework.Value 
    
    let scriptTypesToGenerate = 
      let parsedScriptTypes = providedScriptTypes |> List.choose Paket.LoadingScripts.ScriptGeneration.ScriptType.TryCreate
      
      failOnMismatch providedScriptTypes parsedScriptTypes Paket.LoadingScripts.ScriptGeneration.ScriptType.TryCreate "Unrecognized Script Type(s)"

      match parsedScriptTypes with
      | [] -> [Paket.LoadingScripts.ScriptGeneration.CSharp; Paket.LoadingScripts.ScriptGeneration.FSharp]
      | xs -> xs

    let workaround() = null |> ignore
    for framework in frameworksToGenerate do
        tracefn "generating scripts for framework %s" (framework.ToString())
        workaround() // https://github.com/Microsoft/visualfsharp/issues/759#issuecomment-162243299
        for scriptType in scriptTypesToGenerate do
            Paket.LoadingScripts.ScriptGeneration.generateScriptsForRootFolder scriptType framework rootFolder
    

let main() =
    use consoleTrace = Logging.event.Publish |> Observable.subscribe Logging.traceToConsole
    let paketVersion =
        let assembly = Assembly.GetExecutingAssembly()
        let fvi = FileVersionInfo.GetVersionInfo(assembly.Location)
        fvi.FileVersion

    try
        let parser = ArgumentParser.Create<Command>(programName = "paket", 
                                                    helpTextMessage = sprintf "Paket version %s%sHelp was requested:" paketVersion Environment.NewLine,
                                                    errorHandler = new PaketExiter())

        let results = parser.ParseCommandLine(raiseOnUsage = true)
        let silent = results.Contains <@ Silent @>

        if not silent then tracefn "Paket version %s" paketVersion

        elif results.Contains <@ Verbose @> then
            Logging.verbose <- true

        use fileTrace =
            match results.TryGetResult <@ Log_File @> with
            | Some lf -> setLogFile lf
            | None -> null

        match results.GetSubCommand() with
        | Add r -> processCommand silent add r
        | ClearCache r -> processCommand silent clearCache r
        | Config r -> processWithValidation silent validateConfig config r
        | ConvertFromNuget r -> processCommand silent convert r
        | FindRefs r -> processCommand silent findRefs r
        | Init r -> processCommand silent init r
        | AutoRestore r -> processWithValidation silent validateAutoRestore autoRestore r
        | Install r -> processCommand silent install r
        | Outdated r -> processCommand silent outdated r
        | Remove r -> processCommand silent remove r
        | Restore r -> processCommand silent restore r
        | Simplify r -> processCommand silent simplify r
        | Update r -> processCommand silent update r
        | FindPackages r -> processCommand silent (findPackages silent) r
        | FindPackageVersions r -> processCommand silent findPackageVersions r
        | ShowInstalledPackages r -> processCommand silent showInstalledPackages r
        | ShowGroups r -> processCommand silent showGroups r
        | Pack r -> processCommand silent pack r
        | Push r -> processCommand silent push r
        | GenerateIncludeScripts r -> processCommand silent generateIncludeScripts r
        // global options; list here in order to maintain compiler warnings
        // in case of new subcommands added
        | Verbose
        | Silent
        | Log_File _ -> failwith "internal error: this code should never be reached."

    with
    | exn when not (exn :? System.NullReferenceException) ->
        Environment.ExitCode <- 1
        traceErrorfn "Paket failed with:%s\t%s" Environment.NewLine exn.Message

        if verbose then
            traceErrorfn "StackTrace:%s  %s" Environment.NewLine exn.StackTrace

main()