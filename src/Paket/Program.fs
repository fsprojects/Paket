/// [omit]
module Paket.Program

open System
open System.Diagnostics
open System.IO

open Paket.Logging
open Paket.Commands

open Argu
open PackageSources
open System.Xml
open Paket.Domain

let sw = Stopwatch.StartNew()

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
#if NETCOREAPP1_0
        // Environment.ExitCode not supported in netcoreapp1.0
#else
        Environment.ExitCode <- 1
#endif
    else
        try
            commandF result
        finally
            sw.Stop()
            if not silent then
                let realTime = sw.Elapsed
                let groupedResults =
                    Profile.events
                    |> Seq.groupBy (fun (ev) -> ev.Category)
                    |> Seq.map (fun (cat, group) ->
                        let l = group |> Seq.toList
                        cat, l.Length, l |> Seq.map (fun ev -> ev.Duration) |> Seq.fold (+) (TimeSpan()))
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
                tracefn "Performance:"
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
                        tracefn " - Resolver: %s (%d runs)" (Utils.TimeSpanToReadableString elapsed) num
                        let realTime = resolver - blocked
                        tracefn "    - Runtime: %s" (Utils.TimeSpanToReadableString realTime)
                    | Profile.Category.ResolverAlgorithmBlocked b ->
                        let reason = reason b
                        tracefn "    - Blocked (%s): %s (%d times)" reason (Utils.TimeSpanToReadableString elapsed) num
                    | Profile.Category.ResolverAlgorithmNotBlocked b ->
                        let reason = reason b
                        tracefn "    - Not Blocked (%s): %d times" reason num
                    | Profile.Category.FileIO ->
                        tracefn " - Disk IO: %s" (Utils.TimeSpanToReadableString elapsed)
                    | Profile.Category.NuGetDownload ->
                        let avg = TimeSpan.FromTicks(elapsed.Ticks / int64 num)
                        tracefn " - Average Download Time: %s" (Utils.TimeSpanToReadableString avg)
                        tracefn " - Number of downloads: %d" num
                    | Profile.Category.NuGetRequest ->
                        let avg = TimeSpan.FromTicks(elapsed.Ticks / int64 num)
                        tracefn " - Average Request Time: %s" (Utils.TimeSpanToReadableString avg)
                        tracefn " - Number of Requests: %d" num
                    | Profile.Category.Other ->
                        tracefn "  - Other: %s" (Utils.TimeSpanToReadableString elapsed)
                    )

                tracefn " - Runtime: %s" (Utils.TimeSpanToReadableString realTime)

let processCommand silent commandF result =
    processWithValidation silent (fun _ -> true) commandF result

let warnObsolete o n =
    traceWarn (sprintf "Please use the new syntax: %s -> %s" o n)

let failObsolete o n =
    failwithf "You cannot use the old and new syntax at the same time: %s <-> %s" o n

let legacyBool (results : ParseResults<_>) newSyntax oldSyntax (list : bool*bool) =
    match list with
    | (true, false) ->
        true
    | (false, true) ->
        warnObsolete oldSyntax newSyntax
        true
    | (true, true) ->
        failObsolete oldSyntax newSyntax
    | (false, false) ->
        false

let legacyOption (results : ParseResults<_>) newSyntax oldSyntax list =
    match list with
    | (Some id, None) ->
        Some id
    | (None, Some id) ->
        warnObsolete oldSyntax newSyntax
        Some id
    | (Some _, Some _) ->
        failObsolete oldSyntax newSyntax
    | (_, _) -> None

let add (results : ParseResults<_>) =
    let packageName =
        let arg = (results.TryGetResult <@ AddArgs.NuGet @>,
                   results.TryGetResult <@ AddArgs.NuGet_Legacy @>)
                  |> legacyOption results "(omit, option is the new default argument)" "nuget"
        match arg with
        | Some(id) -> id
        | _ -> results.GetResult <@ AddArgs.NuGet @>
    let version =
        let arg = (results.TryGetResult <@ AddArgs.Version @>,
                   results.TryGetResult <@ AddArgs.Version_Legacy @>)
                  |> legacyOption results "--version" "version"
        defaultArg arg ""
    let force = results.Contains <@ AddArgs.Force @>
    let redirects = results.Contains <@ AddArgs.Redirects @>
    let createNewBindingFiles =
        (results.Contains <@ AddArgs.Create_New_Binding_Files @>,
         results.Contains <@ AddArgs.Create_New_Binding_Files_Legacy @>)
        |> legacyBool results "--create-new-binding-files" "--createnewbindingfiles"
    let cleanBindingRedirects = results.Contains <@ AddArgs.Clean_Redirects @>
    let group =
        (results.TryGetResult <@ AddArgs.Group @>,
         results.TryGetResult <@ AddArgs.Group_Legacy @>)
        |> legacyOption results "--group" "group"
    let noInstall = results.Contains <@ AddArgs.No_Install @>
    let semVerUpdateMode =
        if results.Contains <@ AddArgs.Keep_Patch @> then SemVerUpdateMode.KeepPatch else
        if results.Contains <@ AddArgs.Keep_Minor @> then SemVerUpdateMode.KeepMinor else
        if results.Contains <@ AddArgs.Keep_Major @> then SemVerUpdateMode.KeepMajor else
        SemVerUpdateMode.NoRestriction
    let touchAffectedRefs = results.Contains <@ AddArgs.Touch_Affected_Refs @>
    let project =
        (results.TryGetResult <@ AddArgs.Project @>,
         results.TryGetResult <@ AddArgs.Project_Legacy @>)
        |> legacyOption results "--project" "project"

    match project with
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

let autoRestore (fromBootstrapper:bool) (results : ParseResults<_>) =
    match results.GetResult <@ Flags @> with
    | On -> Dependencies.Locate().TurnOnAutoRestore(fromBootstrapper)
    | Off -> Dependencies.Locate().TurnOffAutoRestore()

let convert (fromBootstrapper:bool) (results : ParseResults<_>) =
    let force = results.Contains <@ ConvertFromNugetArgs.Force @>
    let noInstall = results.Contains <@ ConvertFromNugetArgs.No_Install @>
    let noAutoRestore = results.Contains <@ ConvertFromNugetArgs.No_Auto_Restore @>
    let credsMigrationMode =
        (results.TryGetResult <@ ConvertFromNugetArgs.Migrate_Credentials @>,
         results.TryGetResult <@ ConvertFromNugetArgs.Migrate_Credentials @>)
        |> legacyOption results "--migrate-credentials" "--creds-migration"

    Dependencies.ConvertFromNuget(force, noInstall |> not, noAutoRestore |> not, credsMigrationMode, fromBootstrapper=fromBootstrapper)

let findRefs (results : ParseResults<_>) =
    let packages = results.GetResult <@ FindRefsArgs.Packages @>
    let group = defaultArg (results.TryGetResult <@ FindRefsArgs.Group @>) (Constants.MainDependencyGroup.ToString())
    packages |> List.map (fun p -> group,p)
    |> Dependencies.Locate().ShowReferencesFor

let init (fromBootstrapper:bool) (results : ParseResults<InitArgs>) =
    Dependencies.Init(Directory.GetCurrentDirectory(),fromBootstrapper)

let clearCache (results : ParseResults<ClearCacheArgs>) =
    Dependencies.ClearCache()

let install (results : ParseResults<_>) =
    let force = results.Contains <@ InstallArgs.Force @>
    let withBindingRedirects = results.Contains <@ InstallArgs.Redirects @>
    let createNewBindingFiles = results.Contains <@ InstallArgs.Create_New_Binding_Files @>
    let cleanBindingRedirects = results.Contains <@ InstallArgs.Clean_Redirects @>
    let installOnlyReferenced = results.Contains <@ InstallArgs.Install_Only_Referenced @>
    let generateLoadScripts = results.Contains <@ InstallArgs.Generate_Load_Scripts @>
    let alternativeProjectRoot = results.TryGetResult <@ InstallArgs.Project_Root @>
    let providedFrameworks = results.GetResults <@ InstallArgs.Load_Script_Framework @>
    let providedScriptTypes = results.GetResults <@ InstallArgs.Load_Script_Type @>
    let semVerUpdateMode =
        if results.Contains <@ InstallArgs.Keep_Patch @> then SemVerUpdateMode.KeepPatch else
        if results.Contains <@ InstallArgs.Keep_Minor @> then SemVerUpdateMode.KeepMinor else
        if results.Contains <@ InstallArgs.Keep_Major @> then SemVerUpdateMode.KeepMajor else
        SemVerUpdateMode.NoRestriction
    let touchAffectedRefs = results.Contains <@ InstallArgs.Touch_Affected_Refs @>

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
    let strict = results.Contains <@ OutdatedArgs.Ignore_Constraints @> |> not
    let includePrereleases = results.Contains <@ OutdatedArgs.Include_Prereleases @>
    let group = results.TryGetResult <@ OutdatedArgs.Group @>
    Dependencies.Locate().ShowOutdated(strict, includePrereleases, group)

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
    let project = results.TryGetResult (<@ RestoreArgs.Project @>)
    let group = results.TryGetResult <@ RestoreArgs.Group @>
    let installOnlyReferenced = results.Contains <@ RestoreArgs.Install_Only_Referenced @>
    let touchAffectedRefs = results.Contains <@ RestoreArgs.Touch_Affected_Refs @>
    let ignoreChecks = results.Contains <@ RestoreArgs.Ignore_Checks @>
    let failOnChecks = results.Contains <@ RestoreArgs.Fail_On_Checks @>
    let targetFramework = results.TryGetResult <@ RestoreArgs.Target_Framework @>

    match project with
    | Some project ->
        Dependencies.Locate().Restore(force, group, project, touchAffectedRefs, ignoreChecks, failOnChecks, targetFramework)
    | None ->
        if List.isEmpty files then
            Dependencies.Locate().Restore(force, group, installOnlyReferenced, touchAffectedRefs, ignoreChecks, failOnChecks, targetFramework)
        else
            Dependencies.Locate().Restore(force, group, files, touchAffectedRefs, ignoreChecks, failOnChecks, targetFramework)

let simplify (results : ParseResults<_>) =
    let interactive = results.Contains <@ SimplifyArgs.Interactive @>
    Dependencies.Locate().Simplify(interactive)

let update (results : ParseResults<_>) =
    let force = results.Contains <@ UpdateArgs.Force @>
    let noInstall = results.Contains <@ UpdateArgs.No_Install @>
    let group = results.TryGetResult <@ UpdateArgs.Group @>
    let withBindingRedirects = results.Contains <@ UpdateArgs.Redirects @>
    let cleanBindingRedirects = results.Contains <@ UpdateArgs.Clean_Redirects @>
    let createNewBindingFiles = results.Contains <@ UpdateArgs.Create_New_Binding_Files @>
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
                      workingDir = System.IO.Directory.GetCurrentDirectory(),
                      lockDependencies = results.Contains <@ PackArgs.LockDependencies @>,
                      minimumFromLockFile = results.Contains <@ PackArgs.LockDependenciesToMinimum @>,
                      pinProjectReferences = results.Contains <@ PackArgs.PinProjectReferences @>,
                      symbols = results.Contains <@ PackArgs.Symbols @>,
                      includeReferencedProjects = results.Contains <@ PackArgs.IncludeReferencedProjects @>,
                      ?projectUrl = results.TryGetResult <@ PackArgs.ProjectUrl @>)

let findPackages silent (results : ParseResults<_>) =
    let maxResults =
        let arg = (results.TryGetResult <@ FindPackagesArgs.Max_Results @>,
                   results.TryGetResult <@ FindPackagesArgs.Max_Results_Legacy @>)
                  |> legacyOption results "--max" "max"
        defaultArg arg 10000
    let sources  =
        let dependencies = Dependencies.TryLocate()
        let arg = (results.TryGetResult <@ FindPackagesArgs.Source @>,
                   results.TryGetResult <@ FindPackagesArgs.Source_Legacy @>)
                  |> legacyOption results "--source" "source"

        match arg, dependencies with
        | Some source, _ ->
            [PackageSource.NuGetV2Source source]
        | _, Some dependencies ->
            dependencies.GetSources() |> Seq.map (fun kv -> kv.Value) |> List.concat
        | _ ->
            failwithf "Could not find '%s' at or above current directory, and no explicit source was given as parameter (e.g. 'paket.exe find-packages --source https://www.nuget.org/api/v2')."
                Constants.DependenciesFileName

    let searchAndPrint searchText =
        for p in Dependencies.FindPackagesByName(sources,searchText,maxResults) do
            tracefn "%s" p

    let search =
        (results.TryGetResult <@ FindPackagesArgs.Search @>,
         results.TryGetResult <@ FindPackagesArgs.Search_Legacy @>)
        |> legacyOption results "(omit, option is the new default argument)" "searchtext"

    match search with
    | None ->
        let rec repl () =
            if not silent then
                tracefn " - Please enter search text (:q for exit):"

            match Console.ReadLine() with
            | ":q" -> ()
            | searchText ->
                searchAndPrint searchText
                repl ()

        repl ()

    | Some searchText -> searchAndPrint searchText

let fixNuspecs silent (results : ParseResults<_>) =
    let referenceFile = results.GetResult <@ FixNuspecsArgs.ReferencesFile @>
    let nuspecFiles = results.GetResult <@ FixNuspecsArgs.Files @>
    Dependencies.FixNuspecs (referenceFile, nuspecFiles)

// For Backwards compatibility
let fixNuspec silent (results : ParseResults<_>) =
    let fileString = results.GetResult <@ FixNuspecArgs.File @>
    let refFile = results.GetResult <@ FixNuspecArgs.ReferencesFile @>
    let nuspecList = fileString.Split([|';'|])|>List.ofArray
    Dependencies.FixNuspecs (refFile, nuspecList)

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
    let maxResults =
        let arg = (results.TryGetResult <@ FindPackageVersionsArgs.Max_Results @>,
                   results.TryGetResult <@ FindPackageVersionsArgs.Max_Results_Legacy @>)
                  |> legacyOption results "--max" "max"
        defaultArg arg 10000
    let dependencies = Dependencies.TryLocate()
    let name =
        let arg = (results.TryGetResult <@ FindPackageVersionsArgs.NuGet @>,
                   results.TryGetResult <@ FindPackageVersionsArgs.NuGet_Legacy @>)
                  |> legacyOption results "(omit, option is the new default argument)" "nuget"
        match arg with
        | Some(id) -> id
        | _ -> results.GetResult <@ FindPackageVersionsArgs.NuGet @>
    let sources =
        let arg = (results.TryGetResult <@ FindPackageVersionsArgs.Source @>,
                   results.TryGetResult <@ FindPackageVersionsArgs.Source_Legacy @>)
                  |> legacyOption results "--source" "source"
        match arg, dependencies with
        | Some source, _ ->
            [PackageSource.NuGetV2Source source]
        | _, Some dependencies ->
            dependencies.GetSources() |> Seq.map (fun kv -> kv.Value) |> List.concat
        | _ ->
            failwithf "Could not find '%s' at or above current directory, and no explicit source was given as parameter (e.g. 'paket.exe find-package-versions --source https://www.nuget.org/api/v2')."
                Constants.DependenciesFileName
    let root =
        match dependencies with
        | Some d ->
            d.RootPath
        | None ->
            traceWarnfn "Could not find '%s' at or above current directory. Using current directory as project root." Constants.DependenciesFileName
            Directory.GetCurrentDirectory()

    for p in Dependencies.FindPackageVersions(root,sources,name,maxResults) do
        tracefn "%s" p

let push (results : ParseResults<_>) =
    let fileName = results.GetResult <@ PushArgs.FileName @>
    Dependencies.Push(fileName, ?url = results.TryGetResult <@ PushArgs.Url @>,
                      ?endPoint = results.TryGetResult <@ PushArgs.EndPoint @>,
                      ?apiKey = results.TryGetResult <@ PushArgs.ApiKey @>)

let generateLoadScripts (results : ParseResults<GenerateLoadScriptsArgs>) =
    let providedFrameworks = results.GetResults <@ GenerateLoadScriptsArgs.Framework @>
    let providedScriptTypes = results.GetResults <@ GenerateLoadScriptsArgs.ScriptType @>
    let providedGroups = defaultArg (results.TryGetResult<@ GenerateLoadScriptsArgs.Groups @>) []
    Dependencies.Locate().GenerateLoadScripts providedGroups providedFrameworks providedScriptTypes

let generateNuspec (results:ParseResults<GenerateNuspecArgs>) =
    let projectFile = results.GetResult <@ GenerateNuspecArgs.Project @>
    let dependencies = results.GetResult <@ GenerateNuspecArgs.DependenciesFile @>
    let output = defaultArg  (results.TryGetResult <@ GenerateNuspecArgs.Output @>) (Directory.GetCurrentDirectory())
    let filename, nuspec = Nuspec.FromProject(projectFile,dependencies)
    let nuspecString = nuspec.ToString()
    File.WriteAllText (Path.Combine (output,filename), nuspecString)

let why (results: ParseResults<WhyArgs>) =
    let packageName =
        let arg = (results.TryGetResult <@ WhyArgs.NuGet @>,
                   results.TryGetResult <@ WhyArgs.NuGet_Legacy @>)
                  |> legacyOption results "(omit, option is the new default argument)" "nuget"
        match arg with
        | Some(id) -> id
        | _ -> results.GetResult <@ WhyArgs.NuGet @>
        |> Domain.PackageName
    let groupName =
        let arg = (results.TryGetResult <@ WhyArgs.Group @>,
                   results.TryGetResult <@ WhyArgs.Group_Legacy @>)
                  |> legacyOption results "--group" "group"
                  |> Option.map Domain.GroupName
        defaultArg arg Constants.MainDependencyGroup
    let dependencies = Dependencies.Locate()
    let lockFile = dependencies.GetLockFile()
    let directDeps =
        dependencies
            .GetDependenciesFile()
            .GetDependenciesInGroup(groupName)
            |> Seq.map (fun pair -> pair.Key)
            |> Set.ofSeq
    let options =
        { Why.WhyOptions.Details = results.Contains <@ WhyArgs.Details @> }

    Why.ohWhy(packageName, directDeps, lockFile, groupName, results.Parser.PrintUsage(), options)

let main() =
    let resolution = Environment.GetEnvironmentVariable ("PAKET_DISABLE_RUNTIME_RESOLUTION")
    if System.String.IsNullOrEmpty resolution then
        Environment.SetEnvironmentVariable ("PAKET_DISABLE_RUNTIME_RESOLUTION", "true")
    use consoleTrace = Logging.event.Publish |> Observable.subscribe Logging.traceToConsole
    let paketVersion = AssemblyVersionInformation.AssemblyInformationalVersion

    try
        let parser = ArgumentParser.Create<Command>(programName = "paket",
                                                    helpTextMessage = sprintf "Paket version %s%sHelp was requested:" paketVersion Environment.NewLine,
                                                    errorHandler = new PaketExiter())

        let results = parser.ParseCommandLine(raiseOnUsage = true)
        let silent = results.Contains <@ Silent @>

        if not silent then tracefn "Paket version %s" paketVersion

        if results.Contains <@ Verbose @> then
            Logging.verbose <- true

        let fromBootstrapper = results.Contains <@ From_Bootstrapper @>

        let version = results.Contains <@ Version @>
        if not version then

            use fileTrace =
                match results.TryGetResult <@ Log_File @> with
                | Some lf -> setLogFile lf
                | None -> null

            match results.GetSubCommand() with
            | Add r -> processCommand silent add r
            | ClearCache r -> processCommand silent clearCache r
            | Config r -> processWithValidation silent validateConfig config r
            | ConvertFromNuget r -> processCommand silent (convert fromBootstrapper) r
            | FindRefs r -> processCommand silent findRefs r
            | Init r -> processCommand silent (init fromBootstrapper) r
            | AutoRestore r -> processWithValidation silent validateAutoRestore (autoRestore fromBootstrapper) r
            | Install r -> processCommand silent install r
            | Outdated r -> processCommand silent outdated r
            | Remove r -> processCommand silent remove r
            | Restore r -> processCommand silent restore r
            | Simplify r -> processCommand silent simplify r
            | Update r -> processCommand silent update r
            | FindPackages r -> processCommand silent (findPackages silent) r
            | FindPackageVersions r -> processCommand silent findPackageVersions r
            | FixNuspec r -> processCommand silent (fixNuspec silent) r
            | FixNuspecs r -> processCommand silent (fixNuspecs silent) r
            | ShowInstalledPackages r -> processCommand silent showInstalledPackages r
            | ShowGroups r -> processCommand silent showGroups r
            | Pack r -> processCommand silent pack r
            | Push r -> processCommand silent push r
            | GenerateIncludeScripts r -> traceWarn "please use generate-load-scripts" ; processCommand silent generateLoadScripts r
            | GenerateLoadScripts r -> processCommand silent generateLoadScripts r
            | GenerateNuspec r -> processCommand silent generateNuspec r
            | Why r -> processCommand silent why r
            // global options; list here in order to maintain compiler warnings
            // in case of new subcommands added
            | Verbose
            | Silent
            | From_Bootstrapper
            | Version
            | Log_File _ -> failwithf "internal error: this code should never be reached."

    with
    | exn when not (exn :? System.NullReferenceException) ->
#if NETCOREAPP1_0
        // Environment.ExitCode not supported
#else
        Environment.ExitCode <- 1
#endif
        traceErrorfn "Paket failed with:"
        if Environment.GetEnvironmentVariable "PAKET_DETAILED_ERRORS" = "true" then
            printErrorExt true true false exn
        else printError exn

main()
