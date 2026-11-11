/// <exclude/>
module Paket.Program

open System
open System.Diagnostics
open System.IO

open Paket.Logging
open Paket.Commands

open Argu
open PackageSources

let sw = Stopwatch.StartNew()

type PaketExiter() =
    interface IExiter with
        member __.Name = "paket exiter"
        member __.Exit (msg,code) =
            if code = ErrorCode.HelpText then
                tracen msg ; exit 0
            else traceError msg ; exit 1

let paketVersion =
  let attrs = System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(false)

  attrs
  |> Seq.pick (fun a -> match a with | :? System.Reflection.AssemblyInformationalVersionAttribute as i -> Some i.InformationalVersion | _ -> None)

let mutable tracedVersion = false

let tracePaketVersion silent =
    if not silent && not tracedVersion then
        tracedVersion <- true
        tracefn "Paket version %s" paketVersion

let processWithValidationEx printUsage silent validateF commandF result =
    tracePaketVersion silent
    if not (validateF result) then
        traceError "Command was:"
        traceError ("  " + String.Join(" ",Environment.GetCommandLineArgs()))
        printUsage result

        Environment.ExitCode <- 1
    else
        try
            commandF result
        finally
            sw.Stop()
            if not silent then
                let realTime = sw.Elapsed
                let groupedResults =
                    Profile.events
                    |> Seq.groupBy (fun ev -> ev.Category)
                    |> Seq.map (fun (cat, group) ->
                        let l = group |> Seq.toList
                        let eventBoundaries = l |> List.collect(fun ev -> [ev.Start; ev.End])
                        let mergedSpans = Profile.getCoalescedEventTimeSpans(eventBoundaries |> List.toArray)
                        let mergedSpanLengths = mergedSpans |> Array.fold (+) (TimeSpan())

                        cat, l.Length, mergedSpanLengths)
                    |> Seq.toList
                let blockedRaw =
                    groupedResults
                    |> List.filter (function Profile.Category.ResolverAlgorithmBlocked _, _, _ -> true | _ -> false)
                let blocked =
                    blockedRaw
                    |> List.map (fun (_,_,t) -> t)
                    |> Seq.fold (+) (TimeSpan())
                let resolver =
                    match groupedResults |> List.tryPick (function Profile.Category.ResolverAlgorithm, _, s -> Some s | _ -> None) with
                    | Some s -> s
                    | None -> TimeSpan()
                let totalTimeElapsed =
                    groupedResults
                    |> List.choose (fun (cat, _, elapsed) ->
                        match cat with
                        | Profile.Category.ResolverAlgorithmBlocked _
                        | Profile.Category.ResolverAlgorithmNotBlocked _ -> None
                        | _ -> Some elapsed)
                    |> List.fold (+) TimeSpan.Zero
                tracefn "Total time taken: %s" (Utils.TimeSpanToReadableString totalTimeElapsed)
                groupedResults
                |> List.sortBy (fun (cat,_,_) ->
                    match cat with
                    | Profile.Category.ResolverAlgorithm -> 1
                    | Profile.Category.ResolverAlgorithmBlocked b -> 2
                    | Profile.Category.ResolverAlgorithmNotBlocked b -> 3
                    | Profile.Category.FileIO -> 4
                    | Profile.Category.NuGetDownload -> 5
                    | Profile.Category.NuGetRequest -> 6
                    | Profile.Category.Other -> 7)
                |> List.iter (fun (cat, num, elapsed) ->
                    let reason b =
                        match b with
                        | Profile.BlockReason.PackageDetails -> "retrieving package details"
                        | Profile.BlockReason.GetVersion -> "retrieving package versions"
                    match cat with
                    | Profile.Category.ResolverAlgorithm ->
                        verbosefn " - Resolver: %s (%d runs)" (Utils.TimeSpanToReadableString elapsed) num
                        let realTime = resolver - blocked
                        verbosefn "    - Runtime: %s" (Utils.TimeSpanToReadableString realTime)
                    | Profile.Category.ResolverAlgorithmBlocked b ->
                        let reason = reason b
                        verbosefn "    - Blocked (%s): %s (%d times)" reason (Utils.TimeSpanToReadableString elapsed) num
                    | Profile.Category.ResolverAlgorithmNotBlocked b ->
                        let reason = reason b
                        verbosefn "    - Not Blocked (%s): %d times" reason num
                    | Profile.Category.FileIO ->
                        verbosefn " - Disk IO: %s" (Utils.TimeSpanToReadableString elapsed)
                    | Profile.Category.NuGetDownload ->
                        let avg = TimeSpan.FromTicks(elapsed.Ticks / int64 num)
                        verbosefn " - Average Download Time: %s" (Utils.TimeSpanToReadableString avg)
                        verbosefn " - Number of downloads: %d" num
                    | Profile.Category.NuGetRequest ->
                        let avg = TimeSpan.FromTicks(elapsed.Ticks / int64 num)
                        verbosefn " - Average Request Time: %s" (Utils.TimeSpanToReadableString avg)
                        verbosefn " - Number of Requests: %d" num
                    | Profile.Category.Other ->
                        verbosefn "  - Other: %s" (Utils.TimeSpanToReadableString elapsed)
                    )

                verbosefn " - Runtime: %s" (Utils.TimeSpanToReadableString realTime)
                let omitted = Logging.getOmittedWarningCount()
                if not verbose && omitted > 0 then
                    traceWarnfn "Paket omitted %d warnings. You can see them in verbose mode." omitted

let processWithValidation silent validateF commandF (result : ParseResults<'T>) =
    processWithValidationEx (fun (r:ParseResults<'T>) -> r.Parser.PrintUsage() |> traceError) silent validateF commandF result

let processCommand silent commandF result =
    processWithValidation silent (fun _ -> true) commandF result

let add (results : ParseResults<_>) =
    let packageName = results.GetResult AddArgs.NuGet
    let version = defaultArg (results.TryGetResult AddArgs.Version) ""
    let force = results.Contains AddArgs.Force
    let redirects = results.Contains AddArgs.Redirects
    let createNewBindingFiles = results.Contains AddArgs.Create_New_Binding_Files
    let cleanBindingRedirects = results.Contains AddArgs.Clean_Redirects
    let group = results.TryGetResult AddArgs.Group
    let noInstall = results.Contains AddArgs.No_Install
    let noResolve = results.Contains AddArgs.No_Resolve
    let semVerUpdateMode =
        if results.Contains AddArgs.Keep_Patch then SemVerUpdateMode.KeepPatch else
        if results.Contains AddArgs.Keep_Minor then SemVerUpdateMode.KeepMinor else
        if results.Contains AddArgs.Keep_Major then SemVerUpdateMode.KeepMajor else
        SemVerUpdateMode.NoRestriction
    let touchAffectedRefs = results.Contains AddArgs.Touch_Affected_Refs
    let project = results.TryGetResult AddArgs.Project
    let packageKind =
        match results.GetResult (AddArgs.Type, defaultValue = AddArgsDependencyType.Nuget) with
        | AddArgsDependencyType.Nuget -> Requirements.PackageRequirementKind.Package
        | AddArgsDependencyType.Clitool -> Requirements.PackageRequirementKind.DotnetCliTool

    let dependencies =
        Dependencies.TryLocate ()
        |> Option.defaultWith (fun () ->
            Dependencies.Init ()
            Dependencies.Locate ())

    // If no project was supplied, check if there's a project in the current folder and if so, use that.
    let project =
        match project with
        | Some _ ->
            project
        | None ->
            match Directory.GetCurrentDirectory() |> ProjectFile.FindAllProjects |> Array.toList with
            | [] ->
                None
            | project :: _ ->
                Some project.FileName

    match project with
    | Some projectName ->
        dependencies.AddToProject(group, packageName, version, force, redirects, cleanBindingRedirects, createNewBindingFiles, projectName, noInstall |> not, semVerUpdateMode, touchAffectedRefs, noResolve |> not, packageKind)
    | None ->
        let interactive = results.Contains AddArgs.Interactive
        dependencies.Add(group, packageName, version, force, redirects, cleanBindingRedirects, createNewBindingFiles, interactive, noInstall |> not, semVerUpdateMode, touchAffectedRefs, noResolve |> not, packageKind)

let github (results : ParseResults<_>) =
    match results.GetResult GithubArgs.Add with
    | add ->
        let group =
            add.TryGetResult AddGithubArgs.Group
        let repository =
            add.GetResult AddGithubArgs.Repository
        let file =
            match add.TryGetResult AddGithubArgs.File with
            | Some f -> f
            | None -> ""
        let version =
            match add.TryGetResult AddGithubArgs.Version with
            | Some v -> v
            | None -> ""

        Dependencies
            .Locate()
            .AddGithub(group, repository, file, version)

let git (results : ParseResults<_>) =
    match results.GetResult GitArgs.Add with
    | add ->
        let group =
            add.TryGetResult AddGitArgs.Group
        let repository =
            add.GetResult AddGitArgs.Repository
        let version =
            match add.TryGetResult AddGitArgs.Version with
            | Some v -> v
            | None -> ""

        Dependencies
            .Locate()
            .AddGit(group, repository, version)

let validateConfig (results : ParseResults<_>) =
    let credential = results.Contains ConfigArgs.AddCredentials
    let token = results.Contains ConfigArgs.AddToken
    match credential, token with
    | true, _ -> results.GetResults ConfigArgs.AddCredentials |> List.isEmpty |> not
    | _, true -> results.GetResults ConfigArgs.AddToken |> List.isEmpty |> not
    | _ -> false

let config (results : ParseResults<_>) =
    let credentials = results.Contains ConfigArgs.AddCredentials
    let token = results.Contains ConfigArgs.AddToken
    match credentials, token with
    | true, _ ->
      let args = results.GetResults ConfigArgs.AddCredentials
      let source = args.Item 0
      let username, password = results.GetResult (ConfigArgs.Username, ""), results.GetResult (ConfigArgs.Password, "")
      let authType = results.GetResult (ConfigArgs.AuthType, "")
      let verify = results.Contains ConfigArgs.Verify

      Dependencies(".").AddCredentials(source, username, password, authType, verify)
    | _, true ->
      let args = results.GetResults ConfigArgs.AddToken
      let source, token = args.Item 0
      Dependencies(".").AddToken(source, token)
    | _ -> ()

let autoRestore (results : ParseResults<_>) =
    match results.GetResult Flags with
    | On -> Dependencies.Locate().TurnOnAutoRestore()
    | Off -> Dependencies.Locate().TurnOffAutoRestore()

let convert (results : ParseResults<_>) =
    let force = results.Contains ConvertFromNugetArgs.Force
    let noInstall = results.Contains ConvertFromNugetArgs.No_Install
    let noAutoRestore = results.Contains ConvertFromNugetArgs.No_Auto_Restore
    let credsMigrationMode = results.TryGetResult ConvertFromNugetArgs.Migrate_Credentials

    Dependencies.ConvertFromNuget(force, noInstall |> not, noAutoRestore |> not, credsMigrationMode)

let findRefs (results : ParseResults<_>) =
    let packages = results.GetResult FindRefsArgs.NuGets
    let group = defaultArg (results.TryGetResult FindRefsArgs.Group) (Constants.MainDependencyGroup.ToString())
    packages |> List.map (fun p -> group,p)
    |> Dependencies.Locate().ShowReferencesFor

let init (results : ParseResults<InitArgs>) =
    Dependencies.Init(Directory.GetCurrentDirectory())

let clearCache (results : ParseResults<ClearCacheArgs>) =
    let clearLocal = results.Contains ClearCacheArgs.ClearLocal
    Dependencies.ClearCache(clearLocal)

let install (results : ParseResults<_>) =
    let force = results.Contains InstallArgs.Force
    let withBindingRedirects = results.Contains InstallArgs.Redirects
    let createNewBindingFiles = results.Contains InstallArgs.Create_New_Binding_Files
    let cleanBindingRedirects = results.Contains InstallArgs.Clean_Redirects
    let installOnlyReferenced = results.Contains InstallArgs.Install_Only_Referenced
    let generateLoadScripts = results.Contains InstallArgs.Generate_Load_Scripts
    let alternativeProjectRoot = results.TryGetResult InstallArgs.Project_Root
    let providedFrameworks = results.GetResults InstallArgs.Load_Script_Framework
    let providedScriptTypes =
        results.GetResults InstallArgs.Load_Script_Type
        |> List.map (fun l -> l.ToString().ToLowerInvariant())
    let semVerUpdateMode =
        if results.Contains InstallArgs.Keep_Patch then SemVerUpdateMode.KeepPatch else
        if results.Contains InstallArgs.Keep_Minor then SemVerUpdateMode.KeepMinor else
        if results.Contains InstallArgs.Keep_Major then SemVerUpdateMode.KeepMajor else
        SemVerUpdateMode.NoRestriction
    let touchAffectedRefs = results.Contains InstallArgs.Touch_Affected_Refs

    Dependencies.Locate().Install(
        force,
        withBindingRedirects,
        cleanBindingRedirects,
        createNewBindingFiles,
        installOnlyReferenced,
        semVerUpdateMode,
        touchAffectedRefs,
        generateLoadScripts,
        providedFrameworks,
        providedScriptTypes,
        alternativeProjectRoot)

let outdated (results : ParseResults<_>) =
    let force = results.Contains OutdatedArgs.Force
    let strict = results.Contains OutdatedArgs.Ignore_Constraints |> not
    let includePrereleases = results.Contains OutdatedArgs.Include_Prereleases
    let group = results.TryGetResult OutdatedArgs.Group
    Dependencies.Locate().ShowOutdated(strict, force, includePrereleases, group)

let remove (results : ParseResults<_>) =
    let packageName = results.GetResult RemoveArgs.NuGet
    let force = results.Contains RemoveArgs.Force
    let noInstall = results.Contains RemoveArgs.No_Install
    let group = results.TryGetResult RemoveArgs.Group
    let project = results.TryGetResult RemoveArgs.Project

    match project with
    | Some projectName ->
        Dependencies.Locate()
                    .RemoveFromProject(group, packageName, force, projectName, noInstall |> not)
    | None ->
        let interactive = results.Contains RemoveArgs.Interactive
        Dependencies.Locate().Remove(group, packageName, force, interactive, noInstall |> not)

let restore (results : ParseResults<_>) =
    let force = results.Contains RestoreArgs.Force
    let files = results.GetResults RestoreArgs.References_File
    let project = results.TryGetResult RestoreArgs.Project
    let group = results.TryGetResult RestoreArgs.Group
    let installOnlyReferenced = results.Contains RestoreArgs.Install_Only_Referenced
    let touchAffectedRefs = results.Contains RestoreArgs.Touch_Affected_Refs
    let ignoreChecks = results.Contains RestoreArgs.Ignore_Checks
    let failOnChecks = results.Contains RestoreArgs.Fail_On_Checks
    let targetFramework = results.TryGetResult RestoreArgs.Target_Framework
    let outputPath = results.TryGetResult RestoreArgs.Output_Path

    match project with
    | Some project ->
        Dependencies.Locate().Restore(force, group, project, touchAffectedRefs, ignoreChecks, failOnChecks, targetFramework, outputPath)
    | None ->
        if List.isEmpty files then
            Dependencies.Locate().Restore(force, group, installOnlyReferenced, touchAffectedRefs, ignoreChecks, failOnChecks, targetFramework, outputPath)
        else
            Dependencies.Locate().Restore(force, group, files, touchAffectedRefs, ignoreChecks, failOnChecks, targetFramework, outputPath)

let simplify (results : ParseResults<_>) =
    let interactive = results.Contains SimplifyArgs.Interactive
    Dependencies.Locate().Simplify(interactive)

let update (results : ParseResults<_>) =
    let createNewBindingFiles = results.Contains UpdateArgs.Create_New_Binding_Files
    let group = results.TryGetResult UpdateArgs.Group

    let force = results.Contains UpdateArgs.Force
    let noInstall = results.Contains UpdateArgs.No_Install
    let withBindingRedirects = results.Contains UpdateArgs.Redirects
    let cleanBindingRedirects = results.Contains UpdateArgs.Clean_Redirects
    let semVerUpdateMode =
        if results.Contains UpdateArgs.Keep_Patch then SemVerUpdateMode.KeepPatch else
        if results.Contains UpdateArgs.Keep_Minor then SemVerUpdateMode.KeepMinor else
        if results.Contains UpdateArgs.Keep_Major then SemVerUpdateMode.KeepMajor else
        SemVerUpdateMode.NoRestriction
    let touchAffectedRefs = results.Contains UpdateArgs.Touch_Affected_Refs
    let filter = results.Contains UpdateArgs.Filter

    let nuget = results.TryGetResult UpdateArgs.NuGet

    match nuget with
    | Some packageName ->
        let version = results.TryGetResult UpdateArgs.Version

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
    let outputPath = results.GetResult PackArgs.Output
    let buildConfig = results.TryGetResult PackArgs.Build_Config
    let buildPlatform = results.TryGetResult PackArgs.Build_Platform
    let cliVersion = results.TryGetResult PackArgs.Version
    let specificVersions = results.GetResults PackArgs.Specific_Version
    let cliReleaseNotes = results.TryGetResult PackArgs.Release_Notes
    let templateFile = results.TryGetResult PackArgs.Template_File
    let excludedTemplates = results.GetResults PackArgs.Exclude_Template
    let lockDependencies = results.Contains PackArgs.Lock_Dependencies
    let minimumFromLockFile = results.Contains PackArgs.Lock_Dependencies_To_Minimum
    let pinProjectReferences = results.Contains PackArgs.Pin_Project_References
    if pinProjectReferences then
        traceWarn "Please use the new syntax: '--pin-project-references' has been replaced by '--interproject-references'."
    let interprojectReferencesConstraint =
        match results.TryGetResult PackArgs.Interproject_References with
        | Some Min -> Some InterprojectReferencesConstraint.Min
        | Some Fix -> Some InterprojectReferencesConstraint.Fix
        | Some Keep_Major -> Some InterprojectReferencesConstraint.KeepMajor
        | Some Keep_Minor -> Some InterprojectReferencesConstraint.KeepMinor
        | Some Keep_Patch -> Some InterprojectReferencesConstraint.KeepPatch
        | None -> None
    let symbols = results.Contains PackArgs.Symbols
    let includeReferencedProjects = results.Contains PackArgs.Include_Referenced_Projects
    let projectUrl = results.TryGetResult PackArgs.Project_Url
    let fileReleaseNotes =
        match results.TryGetResult PackArgs.Release_Notes_File with
        | Some file -> file |> Fake.Core.ReleaseNotes.load |> Some
        | _ -> None
    let releaseNotes =
        match cliReleaseNotes with
        | Some notes -> Some notes
        | None ->
            match fileReleaseNotes with
            | Some notes -> notes.Notes |> String.concat "\n" |> Some
            | None -> None
    let version =
        match cliVersion with
        | Some v -> Some v
        | None ->
            match fileReleaseNotes with
            | Some notes -> Some notes.NugetVersion
            | None -> None

    Dependencies.Locate()
                .Pack(outputPath,
                      ?buildConfig = buildConfig,
                      ?buildPlatform = buildPlatform,
                      ?version = version,
                      specificVersions = specificVersions,
                      ?releaseNotes = releaseNotes,
                      ?templateFile = templateFile,
                      excludedTemplates = excludedTemplates,
                      workingDir = System.IO.Directory.GetCurrentDirectory(),
                      lockDependencies = lockDependencies,
                      minimumFromLockFile = minimumFromLockFile,
                      pinProjectReferences = pinProjectReferences,
                      interprojectReferencesConstraint = interprojectReferencesConstraint,
                      symbols = symbols,
                      includeReferencedProjects = includeReferencedProjects,
                      ?projectUrl = projectUrl)

/// This is source-discovering logic shared between `findPackages` and `findPackageVersions`
let discoverPackageSources explicitSource (dependencies: Dependencies option) =
    match explicitSource, dependencies with
    | Some source, _ ->
        [PackageSource.NuGetV2Source source]
    | _, Some dependencies ->
        PackageSources.DefaultNuGetSource ::
        (dependencies.GetSources() |> Seq.map (fun kv -> kv.Value) |> List.concat)
    | _ ->
        failwithf "Could not find '%s' at or above current directory, and no explicit source was given as parameter (e.g. 'paket.exe find-packages --source https://www.nuget.org/api/v2')."
            Constants.DependenciesFileName

let findPackages silent (results : ParseResults<_>) =
    let maxResults = defaultArg (results.TryGetResult FindPackagesArgs.Max_Results) 10000
    let sources  =
        let dependencies = Dependencies.TryLocate()
        discoverPackageSources (results.TryGetResult FindPackagesArgs.Source) dependencies

    let searchAndPrint searchText =
        for p in Dependencies.FindPackagesByName(sources,searchText,maxResults) do
            tracefn "%s" p

    let search = results.TryGetResult FindPackagesArgs.Search

    match search with
    | None ->
        Console.CancelKeyPress.Add (fun c -> c.Cancel <- true)
        let rec repl () =
            if not silent then
                tracefn " - Please enter search text (press Ctrl+C to exit):"

            match Console.ReadLine() with
            | null -> ()
            | searchText ->
                searchAndPrint searchText
                repl ()

        repl ()

    | Some searchText -> searchAndPrint searchText

open Paket.Requirements

let fixNuspecs silent (results : ParseResults<_>) =
    let nuspecFiles =
        results.GetResult FixNuspecsArgs.Files
        |> List.collect (fun s -> s.Split([|';'|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList)
        |> List.map (fun s -> s.Trim())
    let conditions =
        results.TryGetResult FixNuspecsArgs.Conditions
        |> Option.defaultValue []
        |> List.collect (fun s -> s.Split([|';'|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList)
        |> List.map (fun s -> s.Trim())

    let projectFile = Paket.ProjectFile.LoadFromFile(results.GetResult FixNuspecsArgs.ProjectFile)
    let refFile = RestoreProcess.FindOrCreateReferencesFile projectFile
    Dependencies.FixNuspecs (projectFile, refFile, nuspecFiles, conditions)

// separated out from showInstalledPackages to allow Paket.PowerShell to get the types
let getInstalledPackages (results : ParseResults<_>) =
    let project = results.TryGetResult ShowInstalledPackagesArgs.Project
    let showAll = results.Contains ShowInstalledPackagesArgs.All
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

let showConditions (results : ParseResults<ShowConditionsArgs>) =
    let dependenciesFile = Dependencies.Locate()
    for condition in dependenciesFile.GetConditions() do
        tracefn "%s" condition

let findPackageVersions (results : ParseResults<_>) =
    let maxResults = defaultArg (results.TryGetResult FindPackageVersionsArgs.Max_Results) 10000
    let dependencies = Dependencies.TryLocate()
    let name = results.GetResult FindPackageVersionsArgs.NuGet
    let sources =
        discoverPackageSources (results.TryGetResult FindPackageVersionsArgs.Source) dependencies

    let root =
        match dependencies with
        | Some d ->
            d.RootPath
        | None ->
            traceWarnfn "Could not find '%s' at or above current directory. Using current directory as project root." Constants.DependenciesFileName
            Directory.GetCurrentDirectory()

    for p in Dependencies.FindPackageVersions(root,sources,name,maxResults) do
        tracefn "%s" p

let push paketVersion (results : ParseResults<_>) =
    let fileName = results.GetResult PushArgs.Package
    let url = results.TryGetResult PushArgs.Url
    let endpoint = results.TryGetResult PushArgs.Endpoint
    let apiKey = results.TryGetResult PushArgs.Api_Key
    let ignoreConflicts = results.Contains PushArgs.Ignore_Conflicts

    Dependencies.Push(fileName,
                      ?url = url,
                      ?endPoint = endpoint,
                      ?apiKey = apiKey,
                      ignoreConflicts = ignoreConflicts)

let generateLoadScripts (results : ParseResults<GenerateLoadScriptsArgs>) =
    let providedFrameworks = results.GetResults GenerateLoadScriptsArgs.Framework
    let providedScriptTypes =
        results.GetResults GenerateLoadScriptsArgs.Type
        |> List.map (fun l -> l.ToString().ToLowerInvariant())
    let providedGroups = results.GetResults GenerateLoadScriptsArgs.Group

    Dependencies.Locate().GenerateLoadScripts providedGroups providedFrameworks providedScriptTypes

let info (results : ParseResults<InfoArgs>) =
    if results.Contains InfoArgs.Paket_Dependencies_Dir then
        match Dependencies.TryLocate() with
        | None -> ()
        | Some deps -> tracefn "%s" deps.RootPath

let generateNuspec (results:ParseResults<GenerateNuspecArgs>) =
    let projectFile = results.GetResult GenerateNuspecArgs.Project
    let dependenciesPath = results.GetResult GenerateNuspecArgs.DependenciesFile
    let output = defaultArg  (results.TryGetResult GenerateNuspecArgs.Output) (Directory.GetCurrentDirectory())
    let dependencies = DependenciesFile.ReadFromFile dependenciesPath
    let filename, nuspec = Nuspec.FromProject(projectFile,dependencies)
    let nuspecString = nuspec.ToString()
    File.WriteAllText (Path.Combine (output,filename), nuspecString)

let why (results: ParseResults<WhyArgs>) =
    let packageName = results.GetResult WhyArgs.NuGet |> Domain.PackageName
    let groupName =
        results.TryGetResult WhyArgs.Group
        |> Option.map Domain.GroupName
        |> Option.defaultValue Constants.MainDependencyGroup
    let dependencies = Dependencies.Locate()
    let lockFile = dependencies.GetLockFile()
    let directDeps =
        dependencies
            .GetDependenciesFile()
            .GetDependenciesInGroup(groupName)
            |> Seq.map (fun pair -> pair.Key)
            |> Set.ofSeq
    let options =
        { Why.WhyOptions.Details = results.Contains WhyArgs.Details }

    Why.ohWhy(packageName, directDeps, lockFile, groupName, results.Parser.PrintUsage(), options)

let restriction (results: ParseResults<RestrictionArgs>) =
    let restrictionRaw = results.GetResult RestrictionArgs.Restriction
    let restriction, parseProblems = Requirements.parseRestrictions restrictionRaw

    for problem in parseProblems |> Seq.map (fun x -> x.AsMessage) do
        Logging.traceWarnfn "Problem: %s" problem

    Logging.tracefn "Restriction: %s" restrictionRaw
    Logging.tracefn "Simplified: %s" (restriction.ToString())
    Logging.tracefn "Frameworks: [ "
    for framework in restriction.RepresentedFrameworks do
        Logging.tracefn "   %s" framework.CompareString
    Logging.tracefn "]"



let waitForDebugger () =
    while not(System.Diagnostics.Debugger.IsAttached) do
        System.Threading.Thread.Sleep(100)

let handleCommand silent command =
    match command with
    | Add r -> processCommand silent add r
    | Github r -> processCommand silent github r
    | Git r -> processCommand silent git r
    | ClearCache r -> processCommand silent clearCache r
    | Config r -> processWithValidation silent validateConfig config r
    | ConvertFromNuget r -> processCommand silent convert r
    | FindRefs r -> processCommand silent findRefs r
    | Init r -> processCommand silent init r
    | AutoRestore r -> processCommand silent autoRestore r
    | Install r -> processCommand silent install r
    | Outdated r -> processCommand silent outdated r
    | Remove r -> processCommand silent remove r
    | Restore r -> processCommand silent restore r
    | Simplify r -> processCommand silent simplify r
    | Update r -> processCommand silent update r
    | FindPackages r -> processCommand silent (findPackages silent) r
    | FindPackageVersions r -> processCommand silent findPackageVersions r
    | FixNuspecs r -> processCommand silent (fixNuspecs silent) r
    | ShowInstalledPackages r -> processCommand silent showInstalledPackages r
    | ShowGroups r -> processCommand silent showGroups r
    | ShowConditions r -> processCommand silent showConditions r
    | Pack r -> processCommand silent pack r
    | Push r -> processCommand silent (push paketVersion) r
    | GenerateLoadScripts r -> processCommand silent generateLoadScripts r
    | GenerateNuspec r -> processCommand silent generateNuspec r
    | Why r -> processCommand silent why r
    | Restriction r -> processCommand silent restriction r
    | Info r -> processCommand silent info r
    // global options; list here in order to maintain compiler warnings
    // in case of new subcommands added
    | Verbose
    | Silent
    | From_Bootstrapper
    | Version
    | Log_File _ -> failwithf "internal error: this code should never be reached."

let main() =
    let waitDebuggerEnvVar = Environment.GetEnvironmentVariable "PAKET_WAIT_DEBUGGER"
    if waitDebuggerEnvVar = "1" then
        waitForDebugger()

    let resolution = Environment.GetEnvironmentVariable "PAKET_DISABLE_RUNTIME_RESOLUTION"
    Logging.verboseWarnings <- Environment.GetEnvironmentVariable "PAKET_DETAILED_WARNINGS" = "true"
    if System.String.IsNullOrEmpty resolution then
        Environment.SetEnvironmentVariable ("PAKET_DISABLE_RUNTIME_RESOLUTION", "true")
    use consoleTrace = Logging.event.Publish |> Observable.subscribe Logging.traceToConsole

    try
    let args = Environment.GetCommandLineArgs()
    match args with
    | [| _; "restore" |] | [| _; "--from-bootstrapper"; "restore" |] ->
        // Global restore fast route, see https://github.com/fsprojects/Argu/issues/90
        processWithValidationEx
            ignore
            false
            (fun _ -> true)
            (fun _ -> Dependencies.Locate().Restore()) ()
    | [| _; "restore"; "--project"; project |] | [| _; "--from-bootstrapper"; "restore"; "--project"; project |] ->
        // Project restore fast route, see https://github.com/fsprojects/Argu/issues/90
        processWithValidationEx
            ignore
            false
            (fun _ -> true)
            (fun _ -> Dependencies.Locate().Restore(false, None, project, false, false, false, None, None)) ()
    | [| _; "restore"; "--project"; project; "--output-path"; outputPath; "--target-framework"; targetFramework |]
    | [| _; "--from-bootstrapper"; "restore"; "--project"; project; "--output-path"; outputPath; "--target-framework"; targetFramework |] ->
        // Project restore fast route, see https://github.com/fsprojects/Argu/issues/90
        processWithValidationEx
            ignore
            false
            (fun _ -> true)
            (fun _ -> Dependencies.Locate().Restore(false, None, project, false, false, false, Some targetFramework, Some outputPath)) ()
    | [| _; "install" |] | [| _; "--from-bootstrapper"; "install" |] ->
        // Global restore fast route, see https://github.com/fsprojects/Argu/issues/90
        processWithValidationEx
            ignore
            false
            (fun _ -> true)
            (fun _ -> Dependencies.Locate().Install(false, false, false, false, false, SemVerUpdateMode.NoRestriction, false, false, [], [], None)) ()
    | _ ->
        let parser = ArgumentParser.Create<Command>(programName = "paket",
                                                    helpTextMessage = sprintf "Paket version %s%sHelp was requested:" paketVersion Environment.NewLine,
                                                    errorHandler = new PaketExiter(),
                                                    checkStructure = false)

        let results = parser.ParseCommandLine(raiseOnUsage = true)
        let silent = results.Contains Silent
        tracePaketVersion silent

        if results.Contains Verbose then
            Logging.verbose <- true
            Logging.verboseWarnings <- true

        let version = results.Contains Version
        if not version then

            use fileTrace =
                match results.TryGetResult Log_File with
                | Some lf -> setLogFile lf
                | None -> null

            handleCommand silent (results.GetSubCommand())
    with
    | exn when not (exn :? System.NullReferenceException) ->
        Environment.ExitCode <- 1
        traceErrorfn "Paket failed with"
        if Environment.GetEnvironmentVariable "PAKET_DETAILED_ERRORS" = "true" then
            printErrorExt true true true exn
        else printError exn

main()
