/// Paket invokation for In-Script reference loading
namespace ReferenceLoading.Internals
module Logic =

    let paketVersionSortForNugetCacheFolder somethingAndNames =
        somethingAndNames 
        |> Seq.map 
            (fun (something, (name: string)) ->
                let splits = name.Split([|'-'|], 2)
                something,
                match splits with
                | [|numbers|] ->
                    let splits = numbers.Split('.')
                    [|
                      for split in splits do
                        match System.UInt64.TryParse split with
                        | false,_ -> ()
                        | true, v -> v
                    |], []

                | [|numbers;rest|] ->
                    let splits = numbers.Split('.')
                    [|
                      for split in splits do
                        match System.UInt64.TryParse split with
                        | false,_ -> ()
                        | true, v -> v
                    |], [ rest]
                | _ -> 
                    [||], []
                )
        |> Seq.groupBy (fst << snd)
        |> Seq.map (fun (number, versions) ->
            let struo =
                versions
                |> Seq.map(fun (something, (version, rest) ) -> something, rest)
                // a release comes before its prereleases
                |> Seq.sortByDescending (fun (_, rest) -> List.isEmpty rest, rest)
            number, struo)
        |> Seq.sortByDescending fst
        //|> Seq.concat
        //|> Seq.map fst

    /// What the content of a tool manifest says of paket: the version it declares, if any, and
    /// whether it is "isRoot", which dotnet doesn't look above
    let paketOfToolManifest (content: string) =
        let options = System.Text.RegularExpressions.RegexOptions.IgnoreCase
        let paketVersion =
            System.Text.RegularExpressions.Regex.Match(content, "\"paket\"\\s*:\\s*\\{[^}]*?\"version\"\\s*:\\s*\"([^\"]+)\"", options)
        let isRoot = System.Text.RegularExpressions.Regex.IsMatch(content, "\"isRoot\"\\s*:\\s*true", options)
        (if paketVersion.Success then Some paketVersion.Groups.[1].Value else None), isRoot

    /// Where to look for dotnet, in order: the host that runs the script (DOTNET_HOST_PATH, the
    /// running process when it is dotnet, the root of the running runtime), then DOTNET_ROOT, the
    /// PATH and the default install folders. A relative path, which would depend on the current
    /// folder, or on the current drive on Windows, is left out.
    let dotnetCandidates
        (isWindows: bool)
        (getEnvironmentVariable: string -> string)
        (processPath: string option)
        (runtimeDirectory: string option)
        (programFiles: string)
        (userProfile: string)
        =
        let dotnetExe = if isWindows then "dotnet.exe" else "dotnet"
        // file names are compared without case on Windows only
        let nameComparison = if isWindows then System.StringComparison.OrdinalIgnoreCase else System.StringComparison.Ordinal
        let inFolder (folders: string list) =
            if List.exists System.String.IsNullOrEmpty folders
            then None
            else
                // on .NET Framework, Path.Combine throws on invalid characters, such as quotes
                try
                    Some(System.IO.Path.Combine(Array.ofList (folders @ [ dotnetExe ])))
                with _ -> None
        let isFullyQualified (path: string) =
            if isWindows then
                // a drive or a UNC path, and not \folder or C:folder, which depend on the current drive or on
                // its current folder
                path.StartsWith(@"\\", System.StringComparison.Ordinal)
                || path.StartsWith("//", System.StringComparison.Ordinal)
                || (path.Length >= 3 && System.Char.IsLetter path.[0] && path.[1] = ':' && (path.[2] = '\\' || path.[2] = '/'))
            else
                System.IO.Path.IsPathRooted path
        [
            match getEnvironmentVariable "DOTNET_HOST_PATH" with
            | null | "" -> ()
            | path -> yield Some path
            // `dotnet fsi`
            match processPath with
            | Some exe when System.String.Equals(System.IO.Path.GetFileName exe, dotnetExe, nameComparison) -> yield Some exe
            | _ -> ()
            // the root of the runtime that runs the script, <root>/shared/Microsoft.NETCore.App/<version>/,
            // and not the folder of the .NET Framework or of a self-contained app
            match runtimeDirectory with
            | None -> ()
            | Some runtimeDirectory ->
                yield
                    try
                        let app = System.IO.DirectoryInfo(runtimeDirectory).Parent
                        let shared = if isNull app then null else app.Parent
                        if not (isNull shared)
                           && not (isNull shared.Parent)
                           && System.String.Equals(app.Name, "Microsoft.NETCore.App", nameComparison)
                           && System.String.Equals(shared.Name, "shared", nameComparison)
                        then inFolder [ shared.Parent.FullName ]
                        else None
                    with _ -> None
            yield inFolder [ getEnvironmentVariable "DOTNET_ROOT" ]
            match getEnvironmentVariable "PATH" with
            | null -> ()
            | path -> yield! path.Split(System.IO.Path.PathSeparator) |> Seq.map (fun folder -> inFolder [ folder.Trim '"' ])
            if isWindows then
                // the Program Files of a 32-bit process is Program Files (x86)
                yield inFolder [ getEnvironmentVariable "ProgramW6432"; "dotnet" ]
                yield inFolder [ programFiles; "dotnet" ]
                // the default of dotnet-install.ps1
                yield inFolder [ getEnvironmentVariable "LOCALAPPDATA"; "Microsoft"; "dotnet" ]
            else
                yield inFolder [ "/usr/local/share/dotnet" ]
                yield inFolder [ "/usr/share/dotnet" ]
                yield inFolder [ "/usr/lib/dotnet" ]
                yield inFolder [ "/usr/lib64/dotnet" ]
            // the default of dotnet-install.sh
            yield inFolder [ userProfile; ".dotnet" ]
        ]
        |> List.choose id
        |> List.filter isFullyQualified
        |> List.distinct

    /// The version of Microsoft.NETCore.App that an app asks for in its runtimeconfig.json, and
    /// whether the app runs on a later major version when that one is missing
    type NetCoreApp = { Major: int; Minor: int; Patch: int; RollsForwardToLaterMajor: bool }

    /// The major, minor and patch numbers of a version, and whether it is a prerelease
    let private parseVersion (version: string) =
        let m = System.Text.RegularExpressions.Regex.Match(version, "^(\\d+)\\.(\\d+)\\.(\\d+)(-|$)")
        let number (group: int) =
            match System.Int32.TryParse m.Groups.[group].Value with
            | true, number -> Some number
            | false, _ -> None
        if not m.Success then None else
        match number 1, number 2, number 3 with
        | Some major, Some minor, Some patch -> Some((major, minor, patch), m.Groups.[4].Value = "-")
        | _ -> None

    /// The Microsoft.NETCore.App that the content of a runtimeconfig.json asks for. It reads the
    /// runtimeconfig.json that the SDK writes, with "name" before "version" and without comments.
    /// A rollForwardOnNoCandidateFx of 2 rolls forward as a rollForward of Major, as the hosts
    /// released so far do. The host of .NET 12 ignores it: a rollForward of Major in its place in the
    /// runtimeconfig.json of the tool fixes that, as the hosts before .NET 12 refuse both.
    let netCoreAppOfRuntimeConfig (content: string) =
        let options = System.Text.RegularExpressions.RegexOptions.IgnoreCase
        let version =
            System.Text.RegularExpressions.Regex.Match(content, "\"name\"\\s*:\\s*\"Microsoft\\.NETCore\\.App\"[^}]*?\"version\"\\s*:\\s*\"([^\"]+)\"", options)
        let rollsForwardToLaterMajor =
            System.Text.RegularExpressions.Regex.IsMatch(content, "\"rollForward\"\\s*:\\s*\"(Latest)?Major\"", options)
            || System.Text.RegularExpressions.Regex.IsMatch(content, "\"rollForwardOnNoCandidateFx\"\\s*:\\s*2\\b", options)
        if not version.Success then None else
        parseVersion version.Groups.[1].Value
        |> Option.map (fun ((major, minor, patch), _) ->
            { Major = major; Minor = minor; Patch = patch; RollsForwardToLaterMajor = rollsForwardToLaterMajor })

    /// The Microsoft.NETCore.App of an app with the DOTNET_ROLL_FORWARD of the environment, which
    /// the host takes over the roll forward of the runtimeconfig.json
    let withRollForwardVariable (rollForward: string) (netCoreApp: NetCoreApp) =
        match rollForward with
        | null | "" -> netCoreApp
        | rollForward ->
            let is (value: string) = System.String.Equals(rollForward, value, System.StringComparison.OrdinalIgnoreCase)
            { netCoreApp with RollsForwardToLaterMajor = is "Major" || is "LatestMajor" }

    /// Whether one of the runtimes of a dotnet, the folders of its shared/Microsoft.NETCore.App
    /// named after their version, runs an app that asks for that Microsoft.NETCore.App. As the host,
    /// it takes a runtime of the same major version, or of a later one when the app rolls forward,
    /// that isn't below the version asked for, as a prerelease of that version is. The roll forward
    /// of LatestPatch or Disable, which keep the minor version (Disable the whole version), is taken
    /// as Minor.
    let runsApp (netCoreApp: NetCoreApp) (runtimes: string seq) =
        let asked = netCoreApp.Major, netCoreApp.Minor, netCoreApp.Patch
        runtimes
        |> Seq.choose parseVersion
        |> Seq.exists (fun ((major, _, _) as version, isPrerelease) ->
            (if isPrerelease then version > asked else version >= asked)
            && (major = netCoreApp.Major || (netCoreApp.RollsForwardToLaterMajor && major > netCoreApp.Major)))

    /// The runtimes of a dotnet: the versions in the shared/Microsoft.NETCore.App folder next to it
    /// that have their Microsoft.NETCore.App.deps.json, without which the host passes over them.
    /// None when there is no such folder, as next to a link like /usr/bin/dotnet.
    let runtimesOfDotnet (dotnet: string) =
        let runtimes =
            System.IO.DirectoryInfo(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName dotnet, "shared", "Microsoft.NETCore.App"))
        if not runtimes.Exists then None else
        runtimes.GetDirectories()
        |> Array.filter (fun runtime ->
            System.IO.File.Exists(System.IO.Path.Combine(runtime.FullName, "Microsoft.NETCore.App.deps.json")))
        |> Array.map (fun runtime -> runtime.Name)
        |> List.ofArray
        |> Some

    /// The first dotnet known to run the app, or else the first one whose runtimes are unknown, as
    /// for a link like /usr/bin/dotnet, or else the first one, which says what runtime is missing.
    /// It stops at the first dotnet known to run the app, without looking at the next ones.
    let pickDotnet (runsTheApp: string -> bool option) (dotnets: string seq) =
        let rated = dotnets |> Seq.map (fun dotnet -> dotnet, runsTheApp dotnet) |> Seq.cache
        rated
        |> Seq.tryFind (fun (_, runs) -> runs = Some true)
        |> Option.orElseWith (fun () -> rated |> Seq.tryFind (fun (_, runs) -> Option.isNone runs))
        |> Option.orElseWith (fun () -> Seq.tryHead rated)
        |> Option.map fst

    /// Whether a dotnet has an SDK, which `dotnet nuget` needs: a folder of the sdk folder next to it
    /// with the dotnet.dll of an SDK. None when there is no shared folder next to it either, as next
    /// to a link like /usr/bin/dotnet.
    let hasSdk (dotnet: string) =
        let folder = System.IO.Path.GetDirectoryName dotnet
        let sdks = System.IO.DirectoryInfo(System.IO.Path.Combine(folder, "sdk"))
        if sdks.Exists
           && sdks.GetDirectories() |> Array.exists (fun sdk -> System.IO.File.Exists(System.IO.Path.Combine(sdk.FullName, "dotnet.dll")))
        then Some true
        elif System.IO.Directory.Exists(System.IO.Path.Combine(folder, "shared")) then Some false
        else None

    /// The paket.dll files that `dotnet tool restore` recorded for a version of paket, in the content
    /// of the .dotnet/toolResolverCache/1/paket file of the dotnet home folder, which `dotnet tool run`
    /// reads: the PathToExecutable of the paket command of that version that dotnet runs, for any
    /// target framework. It reads the JSON that the SDK writes, an array of objects without nested ones.
    let paketDllsOfToolResolverCache (version: string) (content: string) =
        let jsonString = "\"((?:[^\"\\\\]|\\\\.)*)\""
        let value (name: string) (row: string) =
            let m = System.Text.RegularExpressions.Regex.Match(row, "\"" + name + "\"\\s*:\\s*" + jsonString)
            if m.Success then Some(System.Text.RegularExpressions.Regex.Unescape m.Groups.[1].Value) else None
        System.Text.RegularExpressions.Regex.Matches(content, "\\{(?:[^{}\"]|" + jsonString + ")*\\}")
        |> Seq.cast<System.Text.RegularExpressions.Match>
        |> Seq.map (fun row -> row.Value)
        |> Seq.filter (fun row ->
            value "Name" row = Some "paket"
            // and not an executable, as a tool package for a runtime can have
            && value "Runner" row = Some "dotnet"
            && value "Version" row |> Option.exists (fun recorded -> System.String.Equals(recorded, version, System.StringComparison.OrdinalIgnoreCase)))
        |> Seq.choose (value "PathToExecutable")
        |> List.ofSeq

    /// The folders of the Paket .NET tool in a NuGet packages folder and in the one of the user
    /// profile, ~/.nuget/packages, in that order and once each, without case on Windows. A folder
    /// with characters that .NET Framework refuses in a path is left out.
    let paketFolders (isWindows: bool) (nugetPackages: string option) (userProfile: string) =
        [ match nugetPackages with
          | Some nugetPackages -> yield [| nugetPackages; "paket" |]
          | None -> ()
          if not (System.String.IsNullOrEmpty userProfile) then
              yield [| userProfile; ".nuget"; "packages"; "paket" |] ]
        |> List.choose (fun parts ->
            try Some(System.IO.Path.GetFullPath(System.IO.Path.Combine parts)) with _ -> None)
        |> List.distinctBy (fun folder -> if isWindows then folder.ToUpperInvariant() else folder)

    /// The folder in the output of `dotnet nuget locals global-packages --list`, which is
    /// global-packages: <folder>, or info : global-packages: <folder> before NuGet 5.5 (SDK 3.1.200).
    /// A relative folder, or one with a character that the output couldn't encode, is left out.
    let globalPackagesOfNuGetLocals (output: string) =
        let info = "info : "
        let prefix = "global-packages:"
        output.Split([| '\r'; '\n' |], System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun line -> if line.StartsWith(info, System.StringComparison.Ordinal) then line.Substring info.Length else line)
        |> Array.tryPick (fun line ->
            if line.StartsWith(prefix, System.StringComparison.Ordinal)
            then Some(line.Substring(prefix.Length).Trim())
            else None)
        // U+FFFD for bytes that aren't UTF-8, as from the OEM code page of the console on Windows before
        // 10 1909, where the SDK doesn't switch its output to UTF-8
        |> Option.filter (fun folder -> folder.IndexOf '\uFFFD' < 0 && System.IO.Path.IsPathRooted folder)

    /// Where a global tool of paket is, in ~/.dotnet/tools and then ~/.paket. None without a user profile,
    /// and not relative to the current folder when HOME is empty. A path with characters that .NET Framework
    /// refuses in a path, such as quotes, is left out.
    let globalToolPaths (executable: string) (userProfile: string) =
        if System.String.IsNullOrEmpty userProfile then [] else
        [ [| userProfile; ".dotnet"; "tools"; executable |]
          [| userProfile; ".paket"; executable |] ]
        |> List.choose (fun parts -> try Some(System.IO.Path.Combine parts) with _ -> None)

    /// The error when #r "paket:" finds no Paket: the message that the local tool isn't restored, when the
    /// manifest declares paket, or else that Paket wasn't found, with the locations looked in, if any. There
    /// is none without a user profile, NUGET_PACKAGES or a dotnet that tells the NuGet packages folder.
    let paketNotFound (notRestored: string option) (scriptDir: string) (locations: string list) =
        match notRestored with
        | Some notRestored -> notRestored
        | None ->
        let locations =
            match locations with
            | [] -> "."
            | locations ->
                ", or in those locations:\n\n"
                + (locations |> List.map (fun location -> " - " + location) |> String.concat System.Environment.NewLine)
        $"Paket was not found in '%s{scriptDir}' or a parent directory, as a local tool%s{locations}\n\nPlease install the Paket .NET tool: dotnet tool install paket"

    /// The Paket that #r "paket:" runs
    type PaketToRun =
        /// an executable: Paket installed with --tool-path, or a global tool
        | PaketExe of string
        /// a paket.dll that dotnet runs: the local tool, or the most recent Paket .NET tool restored
        | PaketDll of string

    /// The Paket that #r "paket:" runs, or None when there is none, with the warnings it gives on the way.
    /// localTool is the manifest that declares paket, with the paket.dll of its local tool or else the
    /// message that it isn't restored. A Paket installed with --tool-path only runs without such a
    /// manifest, as in the targets: it can be an older Paket, such as the paket.exe or bootstrapper of
    /// Paket 11, and a warning says that it is skipped, unless it is the global tool of ~/.paket. When the
    /// local tool isn't restored, a global tool still runs, or else the most recent Paket .NET tool
    /// restored, with a warning that it may be another version. globalTool and restoredTool are only
    /// looked for when needed, in that order.
    let choosePaket
            (warn: string -> unit)
            (localTool: (string * Result<string, string>) option)
            (toolPathExe: string option)
            (isGlobalToolPath: string -> bool)
            (globalTool: unit -> string option)
            (restoredTool: unit -> string option) =
        match localTool, toolPathExe with
        | None, Some paketExe -> Some (PaketExe paketExe)
        | _ ->

        match localTool, toolPathExe with
        | Some (manifest, paketDll), Some paketExe when not (isGlobalToolPath paketExe) ->
            // when the local tool isn't restored, the next warning tells what runs instead, or else the error asks
            // to restore it
            let runsInstead = match paketDll with Ok _ -> ", which runs instead" | Error _ -> ""
            warn $"Ignoring %s{paketExe}: '%s{manifest}' declares paket%s{runsInstead}. Delete it if it is an older Paket, such as the paket.exe or bootstrapper of Paket 11."
        | _ -> ()

        match localTool with
        | Some (_, Ok paketDll) -> Some (PaketDll paketDll)
        | _ ->

        let insteadOfLocalTool (paket: string) =
            match localTool with
            | Some (_, Error notRestored) -> warn $"%s{notRestored} Running %s{paket} instead, which may be another version."
            | _ -> ()
        match globalTool () with
        | Some paketExe ->
            insteadOfLocalTool "a global tool"
            Some (PaketExe paketExe)
        | None ->
        match restoredTool () with
        | Some paketDll ->
            insteadOfLocalTool "the most recent Paket .NET tool restored"
            Some (PaketDll paketDll)
        | None -> None

namespace ReferenceLoading
module internal PaketHandler =

    open System
    open System.IO

    let PM_DIR = ".paket"

    let userProfile =
        let res = Environment.GetEnvironmentVariable("USERPROFILE")
        if System.String.IsNullOrEmpty res then
            Environment.GetEnvironmentVariable("HOME")
        else res

    let tweakTargetFramework =
        function
            | "net5.0" -> "net50"
            | "netcoreapp5.0" -> "net50"
            | targetFramework -> targetFramework

    let MakeDependencyManagerCommand scriptType packageManagerTargetFramework projectRootDirArgument = 
        let packageManagerTargetFramework = tweakTargetFramework packageManagerTargetFramework

        sprintf "install --generate-load-scripts --load-script-type %s --load-script-framework %s project-root \"%s\""
            scriptType packageManagerTargetFramework (System.IO.Path.GetFullPath projectRootDirArgument)

    let getDirectoryAndAllParentDirectories (directory: DirectoryInfo) =
        let rec allParents (directory: DirectoryInfo) =
            seq {
                match directory.Parent with
                | null -> ()
                | parent -> 
                    yield parent
                    yield! allParents parent
            }

        seq {
            yield directory
            yield! allParents directory
        }

    let isWindows =
        System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)

    /// The executable `dotnet tool install paket --tool-path` writes
    let toolPathExecutable = if isWindows then "paket.exe" else "paket"

    /// Walks up directory structure and tries to find paket installed with --tool-path
    let findPaketExe (prioritizedSearchPaths: string seq) (baseDir: DirectoryInfo) =
        let dirs = [
            yield! Seq.map DirectoryInfo prioritizedSearchPaths
            yield! getDirectoryAndAllParentDirectories baseDir
        ]

        // for each given directory, we look for paket and .paket/paket
        dirs
        |> Seq.collect (fun dir -> [dir; yield! dir.GetDirectories(PM_DIR)])
        |> Seq.map (fun dir -> Path.Combine(dir.FullName, toolPathExecutable))
        |> Seq.tryFind File.Exists

    /// Whether paket installed with --tool-path is the global tool of ~/.paket, which the walk up from
    /// a script in the home folder finds too
    let isGlobalToolPath (paketExe: string) =
        not (String.IsNullOrEmpty userProfile)
        && String.Equals(
            Path.GetDirectoryName paketExe,
            Path.GetFullPath(Path.Combine(userProfile, PM_DIR)),
            StringComparison.OrdinalIgnoreCase)

    /// paket.dll of a version of the Paket .NET tool restored into the NuGet packages folder
    let tryFindToolDll (versionFolder: DirectoryInfo) =
        let tools = DirectoryInfo(Path.Combine(versionFolder.FullName, "tools"))
        if not tools.Exists then None else
        tools.GetDirectories()
        |> Seq.map (fun frameworkFolder -> Path.Combine(frameworkFolder.FullName, "any", "paket.dll"))
        |> Seq.tryFind File.Exists

    /// The first tool manifest up from the directory that has paket, with the version of paket and
    /// the folder that the manifest is for: the one of dotnet-tools.json or of its .config folder,
    /// where `dotnet tool restore` is run. As dotnet, it doesn't look above an "isRoot" manifest.
    let findLocalTool (baseDir: DirectoryInfo) =
        getDirectoryAndAllParentDirectories baseDir
        |> Seq.collect (fun dir ->
            [ dir.FullName, Path.Combine(dir.FullName, ".config", "dotnet-tools.json")
              dir.FullName, Path.Combine(dir.FullName, "dotnet-tools.json") ])
        |> Seq.filter (snd >> File.Exists)
        |> Seq.map (fun (folder, manifest) -> folder, manifest, Internals.Logic.paketOfToolManifest (File.ReadAllText manifest))
        |> Seq.tryFind (fun (_, _, (version, isRoot)) -> Option.isSome version || isRoot)
        |> Option.bind (fun (folder, manifest, (version, _)) -> version |> Option.map (fun version -> manifest, version, folder))

    /// The dotnetCandidates that exist, each one checked for existence once, when an enumeration
    /// first reaches it
    let existingDotnets () =
        let processPath =
            try
                use current = System.Diagnostics.Process.GetCurrentProcess()
                use mainModule = current.MainModule
                Option.ofObj mainModule.FileName
            with _ -> None
        let runtimeDirectory =
            try Some(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()) with _ -> None
        Internals.Logic.dotnetCandidates
            isWindows
            Environment.GetEnvironmentVariable
            processPath
            runtimeDirectory
            (Environment.GetFolderPath Environment.SpecialFolder.ProgramFiles)
            userProfile
        |> Seq.filter File.Exists
        |> Seq.cache

    /// The dotnet to run a framework-dependent paket.dll: of the dotnets of existingDotnets, the one
    /// that pickDotnet picks for the Microsoft.NETCore.App that its runtimeconfig.json asks for, with
    /// the DOTNET_ROLL_FORWARD of the environment
    let findDotnet (dotnets: string seq) (paketDll: string) =
        let netCoreApp =
            try
                Path.ChangeExtension(paketDll, ".runtimeconfig.json")
                |> File.ReadAllText
                |> Internals.Logic.netCoreAppOfRuntimeConfig
                |> Option.map (Internals.Logic.withRollForwardVariable (Environment.GetEnvironmentVariable "DOTNET_ROLL_FORWARD"))
            with _ -> None
        let runsPaket (dotnet: string) =
            match netCoreApp with
            | None -> None
            | Some netCoreApp ->
                try
                    Internals.Logic.runtimesOfDotnet dotnet
                    |> Option.map (Internals.Logic.runsApp netCoreApp)
                with _ -> None
        dotnets
        |> Internals.Logic.pickDotnet runsPaket

    /// The dotnet to run `dotnet nuget`: of the dotnets of existingDotnets, the one that pickDotnet picks
    /// with hasSdk
    let findSdkDotnet (dotnets: string seq) =
        dotnets
        // an sdk folder that can't be listed is unknown
        |> Internals.Logic.pickDotnet (fun dotnet -> try Internals.Logic.hasSdk dotnet with _ -> None)

    /// Kills a process with its child processes, as the one of NuGet that `dotnet nuget` starts, on the
    /// .NET Core 3.0 and later that has Process.Kill(bool); else the process only
    let killProcessTree (p: System.Diagnostics.Process) =
        try
            match typeof<System.Diagnostics.Process>.GetMethod("Kill", [| typeof<bool> |]) with
            | null -> p.Kill()
            | kill -> kill.Invoke(p, [| box true |]) |> ignore
        with _ -> ()

    /// The NuGet packages folder of a folder, as `dotnet nuget locals global-packages --list` gives it
    /// there from the nuget.config files, run with sdkDotnet, the dotnet of findSdkDotnet, which is only
    /// looked for when needed. NUGET_PACKAGES, which NuGet takes first, is taken without running dotnet,
    /// and only when it is a full path, as NuGet requires. None otherwise, or when dotnet doesn't tell it:
    /// there is no dotnet, the command fails or takes more than 30 seconds, or its output has no full path.
    let nugetPackagesFolder (sdkDotnet: Lazy<string option>) (folder: string) =
        match Environment.GetEnvironmentVariable "NUGET_PACKAGES" with
        | null | "" ->
            sdkDotnet.Value
            |> Option.bind (fun dotnet ->
                try
                    let newStartInfo () =
                        System.Diagnostics.ProcessStartInfo(
                            FileName = dotnet,
                            Arguments = "nuget locals global-packages --list",
                            WorkingDirectory = folder,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            UseShellExecute = false)
                    let startInfo =
                        try
                            let startInfo = newStartInfo ()
                            startInfo.Environment.["DOTNET_NOLOGO"] <- "1"
                            startInfo.Environment.["DOTNET_CLI_UI_LANGUAGE"] <- "en"
                            // the first use of an SDK doesn't create the ASP.NET Core development certificate,
                            // nor check the workloads, whose message would be lost
                            startInfo.Environment.["DOTNET_GENERATE_ASPNET_CERTIFICATE"] <- "false"
                            startInfo.Environment.["DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK"] <- "true"
                            startInfo
                        // on .NET Framework, Environment throws when two variables differ only by case, as the
                        // TMP and tmp of Git Bash, and keeps the variables read until then: a new start info
                        // runs dotnet with all the variables of F# Interactive instead
                        with _ -> newStartInfo ()
                    // what recent SDKs write on Windows 10 1909 and later, and elsewhere with a UTF-8 locale
                    startInfo.StandardOutputEncoding <- Text.Encoding.UTF8
                    use p = System.Diagnostics.Process.Start startInfo
                    // both read while it runs, so that neither pipe fills up
                    let output = p.StandardOutput.ReadToEndAsync()
                    p.StandardError.ReadToEndAsync() |> ignore
                    // an SDK that hangs doesn't block the script, nor stays running
                    if not (p.WaitForExit 30000 && output.Wait 5000) then
                        killProcessTree p
                        None
                    elif p.ExitCode <> 0 then None
                    else Internals.Logic.globalPackagesOfNuGetLocals output.Result
                with _ -> None)
        // as NuGet, which refuses a relative one; on .NET Framework, IsPathRooted throws on quotes
        | folder when (try Path.IsPathRooted folder with _ -> false) -> Some folder
        | _ -> None

    /// The folders of the Paket .NET tool in the NuGet packages folders of a folder: the one of
    /// nugetPackagesFolder, then the one of ~/.nuget/packages, for a tool restored from another folder
    /// or when nugetPackagesFolder gives none
    let paketFoldersOfNuGetPackages (sdkDotnet: Lazy<string option>) (folder: string) =
        Internals.Logic.paketFolders isWindows (nugetPackagesFolder sdkDotnet folder) userProfile

    /// The paket.dll of a version of the local tool that `dotnet tool run` runs: the one that
    /// `dotnet tool restore` recorded in the toolResolverCache of the dotnet home folder, DOTNET_CLI_HOME
    /// or else the user profile, if it exists
    let paketDllOfToolResolverCache (version: string) =
        let dotnetHome =
            match Environment.GetEnvironmentVariable "DOTNET_CLI_HOME" with
            | null | "" -> userProfile
            | dotnetHome -> dotnetHome
        try
            if String.IsNullOrEmpty dotnetHome then None else
            let cache = Path.Combine(dotnetHome, ".dotnet", "toolResolverCache", "1", "paket")
            if not (File.Exists cache) then None else
            File.ReadAllText cache
            |> Internals.Logic.paketDllsOfToolResolverCache version
            |> List.tryFind File.Exists
        with _ -> None

    /// A program as a command line shows it, in quotes when its path has a space, as
    /// C:\Program Files\dotnet\dotnet.exe
    let quoteProgram (path: string) =
        if path.Contains " " then "\"" + path + "\"" else path

    /// The dotnet of findDotnet and the arguments that run a framework-dependent paket.dll; fails
    /// when there is no dotnet
    let dotnetCommand (dotnets: string seq) (paketDll: string) =
        match findDotnet dotnets paketDll with
        | Some dotnet -> dotnet, $"\"%s{paketDll}\" "
        | None ->
            failwith $"No dotnet was found to run '%s{paketDll}'. Please set DOTNET_ROOT to the folder of dotnet in the environment of the IDE or app that runs F# Interactive, or add that folder to its PATH."

    /// Resolves absolute load script location: something like
    /// baseDir/.paket/load/scriptName
    /// or
    /// baseDir/.paket/load/frameworkDir/scriptName 
    let GetPaketLoadScriptLocation baseDir optionalFrameworkDir scriptName =
        let paketLoadFolder = System.IO.Path.Combine(PM_DIR,"load")
        let frameworkDir =
            match optionalFrameworkDir with 
            | None -> paketLoadFolder 
            | Some frameworkDir -> System.IO.Path.Combine(paketLoadFolder, frameworkDir)

        System.IO.Path.Combine(baseDir, frameworkDir, scriptName)

    
    /// Resolve packages loaded into scripts using `paket:` in `#r` directives such as `#r @"paket: nuget AmazingNugetPackage"`. 
    /// <remarks>This function will throw if the resolution is not successful, or if the tool or a dotnet to run it wasn't found</remarks>
    /// <param name="fileType">A string given to paket command to select the output language. Can be `fsx` or `csx`</param>
    /// <param name="targetFramework">A string given to paket command to fix the framework.</param>
    /// <param name="prioritizedSearchPaths">List of directories which are checked first to resolve paket installed with --tool-path.</param>
    /// <param name="scriptDir"The folder containing the script</param>
    /// <param name="scriptName">filename for the script (not necessarily existing if interactive evaluation)</param>
    /// <param name="packageManagerTextLinesFromScript">Package manager text lines from script, those are meant to be just the inner part, without `#r "paket:` prefix</param>
    let ResolveDependenciesForLanguage(fileType, targetFramework:string, prioritizedSearchPaths: string seq, scriptDir: string, scriptName: string, packageManagerTextLinesFromScript: string seq) =
        let hashString (str: string) =
          // https://andrewlock.net/why-is-string-gethashcode-different-each-time-i-run-my-program-in-net-core/
          let mutable hash1 = (5381 <<< 16) + 5381
          let mutable hash2 = hash1
          if str.Length = 0 then 
            hash2 
          else
            for i in 0 .. 2 .. str.Length do
              if i < str.Length then
                hash1 <- ((hash1 <<< 5) + hash1) ^^^ (int str.[i])
              if i + 1 < str.Length then
                hash2 <- ((hash2 <<< 5) + hash2) ^^^ (int str.[i + 1])
            hash1 + (hash2 * 1566083941)
        let scriptDirHash = abs (hashString (scriptDir + scriptName))
        let workingDir = Path.Combine(Path.GetTempPath(), "script-packages", string scriptDirHash)
        let depsFileName = "paket.dependencies"
        let workingDirSpecFile = FileInfo(Path.Combine(workingDir,depsFileName))
        if not (Directory.Exists workingDir) then
            Directory.CreateDirectory workingDir |> ignore

        let packageManagerTextLinesFromScript = 
            packageManagerTextLinesFromScript
            |> Seq.toList
            |> List.filter (not << String.IsNullOrWhiteSpace)
        
        let rootDir,packageManagerTextLines =
            let rec findSpecFile dir =
                let fi = FileInfo(Path.Combine(dir,depsFileName))
                if fi.Exists then
                    let lockfileName = "paket.lock"
                    let lockFile = FileInfo(Path.Combine(fi.Directory.FullName,lockfileName))
                    let depsFileLines = File.ReadAllLines fi.FullName
                    if lockFile.Exists then
                        let originalDepsFile = FileInfo(workingDirSpecFile.FullName + ".original")
                        if not originalDepsFile.Exists ||
                            File.ReadAllLines originalDepsFile.FullName <> depsFileLines
                        then
                            File.Copy(fi.FullName,originalDepsFile.FullName,true)
                            let targetLockFile = FileInfo(Path.Combine(workingDir,lockfileName))
                            File.Copy(lockFile.FullName,targetLockFile.FullName,true)
                    
                    let lines = 
                        if List.isEmpty packageManagerTextLinesFromScript then 
                            Array.toList depsFileLines
                        else
                            (Array.toList depsFileLines) @ ("group Main" :: packageManagerTextLinesFromScript)

                    fi.Directory.FullName, lines
                elif not (isNull fi.Directory.Parent) then
                    findSpecFile fi.Directory.Parent.FullName
                else
                    let withImplicitSource = 
                        match packageManagerTextLinesFromScript with
                        | line::_ when line.StartsWith "source" -> packageManagerTextLinesFromScript
                        | _  -> "source https://nuget.org/api/v2" :: packageManagerTextLinesFromScript
                    workingDir, ("framework: " + targetFramework) :: withImplicitSource
           
            findSpecFile scriptDir

        /// hardcoded to load the "Main" group (implicit in paket)
        let loadScript = GetPaketLoadScriptLocation workingDir (Some (tweakTargetFramework targetFramework)) ("main.group." + fileType)
        let additionalIncludeFolders() =
            [Path.Combine(workingDir,"paket-files")]
            |> List.filter Directory.Exists

        if workingDirSpecFile.Exists && 
            (File.ReadAllLines(workingDirSpecFile.FullName) |> Array.toList) = packageManagerTextLines && 
            File.Exists loadScript
        then 
            (loadScript,additionalIncludeFolders())
        else 
            // a line after the :paket> prefix, with only the prefix in that color
            let writeLine (color: ConsoleColor) (message: string) =
                Console.ForegroundColor <- color
                Console.Write ":paket>"
                Console.ResetColor()
                Console.WriteLine(" " + message)
            let warn = writeLine ConsoleColor.Yellow
            // looked for once, when needed: the dotnets, both for the one that runs a paket.dll and for the one
            // with an SDK that `dotnet nuget locals` needs
            let dotnets = lazy (existingDotnets ())
            let sdkDotnet = lazy (findSdkDotnet dotnets.Value)
            // the manifest that declares paket, with the paket.dll of its local tool or else the message that
            // it isn't restored
            let localTool =
                findLocalTool (DirectoryInfo scriptDir)
                |> Option.map (fun (manifest, version, manifestFolder) ->
                    manifest,
                    match paketDllOfToolResolverCache version with
                    | Some paketDll -> Ok paketDll
                    | None ->
                    // else where `dotnet tool restore` restores it when run in the folder of the manifest, or
                    // where it was restored with another dotnet home folder
                    let versionFolders =
                        paketFoldersOfNuGetPackages sdkDotnet manifestFolder
                        |> List.map (fun paketFolder -> Path.Combine(paketFolder, version.ToLowerInvariant()))
                    // a folder that can't be read doesn't stop the lookup in the next one
                    match versionFolders |> List.tryPick (fun folder -> try tryFindToolDll (DirectoryInfo folder) with _ -> None) with
                    | Some paketDll -> Ok paketDll
                    | None ->
                        let notRestoredIn =
                            match versionFolders with
                            | [] -> ""
                            | folders -> " in " + (folders |> List.map (fun folder -> $"'%s{folder}'") |> String.concat " or ")
                        Error $"Paket %s{version}, the local tool of '%s{manifest}', is not restored%s{notRestoredIn}. Please run 'dotnet tool restore' in '%s{manifestFolder}'.")

            // the message that the local tool isn't restored, which the errors of a paket that runs in its place
            // repeat: it may be another version, which only a warning told
            let notRestoredLocalTool = match localTool with Some (_, Error notRestored) -> Some notRestored | _ -> None
            let withLocalToolHint (message: string) =
                match notRestoredLocalTool with
                | Some notRestored -> message + Environment.NewLine + notRestored
                | None -> message

            // we try to resolve paket installed with --tool-path any place up in the folder structure from current script
            let toolPathExe = findPaketExe prioritizedSearchPaths (DirectoryInfo scriptDir)

            let globalTools = Internals.Logic.globalToolPaths toolPathExecutable userProfile

            let paketFolders = lazy (paketFoldersOfNuGetPackages sdkDotnet scriptDir)
            // the newest Paket .NET tool restored in the NuGet packages folders
            let restoredTool () =
                paketFolders.Value
                |> Seq.map DirectoryInfo
                |> Seq.filter (fun paketFolder -> paketFolder.Exists)
                |> Seq.collect (fun paketFolder -> try paketFolder.GetDirectories() with _ -> [||])
                |> Seq.map (fun d -> d, d.Name)
                |> Internals.Logic.paketVersionSortForNugetCacheFolder
                |> Seq.map snd
                |> Seq.concat
                |> Seq.map fst
                |> Seq.tryPick (fun versionFolder -> try tryFindToolDll versionFolder with _ -> None)

            let toolPath, toolArguments =
                // the warnings come before dotnetCommand, which can fail
                match Internals.Logic.choosePaket warn localTool toolPathExe isGlobalToolPath (fun () -> globalTools |> List.tryFind File.Exists) restoredTool with
                | Some (Internals.Logic.PaketExe paketExe) -> paketExe, ""
                | Some (Internals.Logic.PaketDll paketDll) ->
                    try dotnetCommand dotnets.Value paketDll
                    with e when Option.isSome notRestoredLocalTool -> failwith (withLocalToolHint e.Message)
                | None ->
                    // restoredTool ran, which looked for paketFolders
                    failwith (Internals.Logic.paketNotFound notRestoredLocalTool scriptDir [ yield! globalTools; yield! paketFolders.Value ])

            writeLine ConsoleColor.Green (sprintf "using %s" ((quoteProgram toolPath + " " + toolArguments).TrimEnd()))
        
            try File.Delete(loadScript) with _ -> ()
            File.WriteAllLines(workingDirSpecFile.FullName, packageManagerTextLines)
            let startInfo = 
                System.Diagnostics.ProcessStartInfo(
                    FileName = toolPath,
                    WorkingDirectory = workingDir, 
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Arguments = toolArguments + MakeDependencyManagerCommand fileType targetFramework rootDir,
                    CreateNoWindow = true,
                    UseShellExecute = false)
                
            use p = new System.Diagnostics.Process()
            let errors = ResizeArray<_>()
            let log = ResizeArray<_>()
            p.StartInfo <- startInfo
            p.ErrorDataReceived.Add(fun d -> if not (isNull d.Data) then errors.Add d.Data)
            p.OutputDataReceived.Add(fun d -> 
                if not (isNull d.Data) then
                    writeLine ConsoleColor.Green d.Data
                    log.Add d.Data
            )
            // as a global tool that isn't executable
            try p.Start() |> ignore
            // its message only, as with dotnetCommand: an inner exception would come after the restore message
            with e when Option.isSome notRestoredLocalTool -> failwith (withLocalToolHint e.Message)
            p.BeginErrorReadLine()
            p.BeginOutputReadLine()
            p.WaitForExit()
            
            if p.ExitCode <> 0 then
                let msg = String.Join(Environment.NewLine, errors)
                sprintf "Package resolution using '%s %s' failed, see directory '%s'.%s%s"
                    (quoteProgram startInfo.FileName) startInfo.Arguments workingDir Environment.NewLine msg
                |> withLocalToolHint
                |> failwith
            else
                // the result of a paket that ran in place of the local tool, maybe another version, isn't kept: with
                // a comment, paket.dependencies no longer equals the lines of the script, and the next resolution runs
                // paket again, the local tool once it is restored, which installs the versions of the paket.lock left
                // here, as paket install does. It isn't deleted, as another resolution of the script may have started
                // a paket that waits for the lock of this folder to read it.
                if Option.isSome notRestoredLocalTool then
                    try File.AppendAllLines(workingDirSpecFile.FullName, [ "// installed by a paket that ran in place of the local tool" ]) with _ -> ()
                (loadScript,additionalIncludeFolders())

    /// Resolve packages loaded into scripts using `paket:` in `#r` directives such as `#r @"paket: nuget AmazingNugetPackage"`. 
    /// <remarks>This function will throw if the resolution is not successful, or if the tool or a dotnet to run it wasn't found</remarks>
    /// <param name="targetFramework">A string given to paket command to fix the framework.</param>
    /// <param name="scriptDir"The folder containing the script</param>
    /// <param name="scriptName">filename for the script (not necessarily existing if interactive evaluation)</param>
    /// <param name="packageManagerTextLinesFromScript">Package manager text lines from script, those are meant to be just the inner part, without `#r "paket:` prefix</param>
    let ResolveDependencies(targetFramework:string, scriptDir: string, scriptName: string,packageManagerTextLinesFromScript: string seq) =
        let extension = 
            if scriptName.ToLowerInvariant().EndsWith(".fsx") then "fsx"
            elif scriptName.ToLowerInvariant().EndsWith(".csx") then "csx"
            else
                // default to F# in case the calling process doesn't honor giving the script name to discriminate on 
                "fsx"
        
        ResolveDependenciesForLanguage(extension, targetFramework, Seq.empty, scriptDir, scriptName, packageManagerTextLinesFromScript)
