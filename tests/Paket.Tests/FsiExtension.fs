namespace Paket.Tests
module FsiExtension =
    open NUnit.Framework

    [<Test>]
    let ``.nuget resolution logic`` () =
        let dirs = 
            [|
                "5.241.5"
                "5.242.2"
                "5.244.1"
                "5.247.2"
                "6.0.0-alpha042"
                "6.0.0-beta2"
                "6.0.4"
                "7.1.5"
                "151.21.5"
                "nothing expected.12341234--e-fqwef-qwef"
                "152.214.124124-zeee"
                "152.214.124124-beeee"
                "152.213.5"
                "153.214.124124-beeee"
            |]
        let expected =
            [|
                "153.214.124124-beeee"
                "152.214.124124-zeee"
                "152.214.124124-beeee"
                "152.213.5"
                "151.21.5"
                "7.1.5"
                "6.0.4"
                "6.0.0-beta2"
                "6.0.0-alpha042"
                "5.247.2"
                "5.244.1"
                "5.242.2"
                "5.241.5"
                "nothing expected.12341234--e-fqwef-qwef"
            |]
        let actual =
            dirs
            |> Array.map (fun d -> d, d)
            |> ReferenceLoading.Internals.Logic.paketVersionSortForNugetCacheFolder
            |> Seq.map snd
            |> Seq.concat
            |> Seq.map fst
            |> Seq.toArray
        Assert.AreEqual(expected, actual)

    [<Test>]
    let ``.nuget resolution logic takes a release before its prereleases`` () =
        let actual =
            [| "12.0.0-alpha001"; "12.0.0"; "12.0.0-rc1"; "11.0.0" |]
            |> Array.map (fun d -> d, d)
            |> ReferenceLoading.Internals.Logic.paketVersionSortForNugetCacheFolder
            |> Seq.map snd
            |> Seq.concat
            |> Seq.map fst
            |> Seq.toArray
        Assert.AreEqual([| "12.0.0"; "12.0.0-rc1"; "12.0.0-alpha001"; "11.0.0" |], actual)

    [<Test>]
    let ``a tool manifest gives the version of paket`` () =
        let manifest = """{ "version": 1, "isRoot": true, "tools": { "paket": { "version": "12.0.0", "commands": [ "paket" ] } } }"""
        Assert.AreEqual((Some "12.0.0", true), ReferenceLoading.Internals.Logic.paketOfToolManifest manifest)

    [<Test>]
    let ``a command named paket of another tool isn't paket`` () =
        let manifest = """{ "version": 1, "tools": { "paket-wrapper": { "version": "1.0.0", "commands": [ "paket" ] } } }"""
        Assert.AreEqual((None, false), ReferenceLoading.Internals.Logic.paketOfToolManifest manifest)

    [<Test>]
    let ``a root tool manifest without paket stops the lookup`` () =
        let manifest = """{ "version": 1, "isRoot": true, "tools": { "fantomas": { "version": "7.0.0", "commands": [ "fantomas" ] } } }"""
        Assert.AreEqual((None, true), ReferenceLoading.Internals.Logic.paketOfToolManifest manifest)

    [<Test>]
    let ``a tool manifest that isn't the root lets the lookup go up`` () =
        let manifest = """{ "version": 1, "isRoot": false, "tools": {} }"""
        Assert.AreEqual((None, false), ReferenceLoading.Internals.Logic.paketOfToolManifest manifest)


    let root = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dotnet-lookup"))
    let inRoot (parts: string list) = System.IO.Path.Combine(Array.ofList (root :: parts))
    // a UNC path, without the ':' that splits the PATH on Linux
    let inWindowsRoot (parts: string list) = System.IO.Path.Combine(Array.ofList (@"\\server\dotnet-lookup" :: parts))
    let paths (folders: string list) = String.concat (string System.IO.Path.PathSeparator) folders

    let dotnetCandidates isWindows (variables: (string * string) list) processPath runtimeDirectory =
        let getEnvironmentVariable name =
            variables |> List.tryFind (fst >> (=) name) |> Option.map snd |> Option.toObj
        let inRoot = if isWindows then inWindowsRoot else inRoot
        ReferenceLoading.Internals.Logic.dotnetCandidates
            isWindows getEnvironmentVariable processPath runtimeDirectory (inRoot [ "ProgramFiles" ]) (inRoot [ "home" ])

    let runtimeDirectory (parts: string list) =
        inRoot parts + string System.IO.Path.DirectorySeparatorChar

    let defaultFolders =
        [ System.IO.Path.Combine("/usr/local/share/dotnet", "dotnet")
          System.IO.Path.Combine("/usr/share/dotnet", "dotnet")
          System.IO.Path.Combine("/usr/lib/dotnet", "dotnet")
          System.IO.Path.Combine("/usr/lib64/dotnet", "dotnet")
          inRoot [ "home"; ".dotnet"; "dotnet" ] ]

    [<Test>]
    let ``dotnet is looked for in the host of the script, then DOTNET_ROOT, the PATH and the default folders`` () =
        let actual =
            dotnetCandidates
                false
                [ "DOTNET_HOST_PATH", inRoot [ "host"; "dotnet" ]
                  "DOTNET_ROOT", inRoot [ "dotnet-root" ]
                  "PATH", paths [ inRoot [ "first" ]; inRoot [ "second" ] ] ]
                (Some(inRoot [ "process"; "dotnet" ]))
                (Some(runtimeDirectory [ "runtime"; "shared"; "Microsoft.NETCore.App"; "10.0.0" ]))
        let expected =
            [ inRoot [ "host"; "dotnet" ]
              inRoot [ "process"; "dotnet" ]
              inRoot [ "runtime"; "dotnet" ]
              inRoot [ "dotnet-root"; "dotnet" ]
              inRoot [ "first"; "dotnet" ]
              inRoot [ "second"; "dotnet" ] ]
            @ defaultFolders
        Assert.AreEqual(expected, actual)

    [<Test>]
    let ``a dotnet found twice, as the process and the root of its runtime, is looked at once`` () =
        let actual =
            dotnetCandidates
                false
                []
                (Some(inRoot [ "runtime"; "dotnet" ]))
                (Some(runtimeDirectory [ "runtime"; "shared"; "Microsoft.NETCore.App"; "10.0.0" ]))
        Assert.AreEqual(inRoot [ "runtime"; "dotnet" ] :: defaultFolders, actual)

    [<Test>]
    let ``on Windows dotnet is dotnet.exe, also looked for in Program Files and the folder of dotnet-install.ps1`` () =
        let actual =
            dotnetCandidates
                true
                [ "PATH", inWindowsRoot [ "bin" ]
                  "ProgramW6432", inWindowsRoot [ "ProgramW6432" ]
                  "LOCALAPPDATA", inWindowsRoot [ "LocalAppData" ] ]
                (Some(inWindowsRoot [ "process"; "DOTNET.EXE" ]))
                None
        let expected =
            [ inWindowsRoot [ "process"; "DOTNET.EXE" ]
              inWindowsRoot [ "bin"; "dotnet.exe" ]
              inWindowsRoot [ "ProgramW6432"; "dotnet"; "dotnet.exe" ]
              inWindowsRoot [ "ProgramFiles"; "dotnet"; "dotnet.exe" ]
              inWindowsRoot [ "LocalAppData"; "Microsoft"; "dotnet"; "dotnet.exe" ]
              inWindowsRoot [ "home"; ".dotnet"; "dotnet.exe" ] ]
        Assert.AreEqual(expected, actual)

    [<Test>]
    let ``on Windows a path relative to the current drive or to its current folder is skipped`` () =
        let actual =
            dotnetCandidates
                true
                [ "DOTNET_HOST_PATH", @"C:dotnet.exe"
                  "DOTNET_ROOT", @"C:dotnet"
                  "PATH", paths [ @"\tools"; "/tools"; inWindowsRoot [ "bin" ] ] ]
                None
                None
        let expected =
            [ inWindowsRoot [ "bin"; "dotnet.exe" ]
              inWindowsRoot [ "ProgramFiles"; "dotnet"; "dotnet.exe" ]
              inWindowsRoot [ "home"; ".dotnet"; "dotnet.exe" ] ]
        Assert.AreEqual(expected, actual)

    [<Test>]
    let ``on Windows a drive or a UNC path can use / as separator`` () =
        let actual = dotnetCandidates true [ "DOTNET_ROOT", "C:/dotnet"; "PATH", "//server/bin" ] None None
        let expected =
            [ System.IO.Path.Combine("C:/dotnet", "dotnet.exe")
              System.IO.Path.Combine("//server/bin", "dotnet.exe")
              inWindowsRoot [ "ProgramFiles"; "dotnet"; "dotnet.exe" ]
              inWindowsRoot [ "home"; ".dotnet"; "dotnet.exe" ] ]
        Assert.AreEqual(expected, actual)

    [<Test>]
    let ``on Windows the shared runtime folders are found without case`` () =
        if not (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform System.Runtime.InteropServices.OSPlatform.Windows) then
            Assert.Ignore "The runtime folder must be a full Windows path that DirectoryInfo reads"
        let actual = dotnetCandidates true [] None (Some(runtimeDirectory [ "runtime"; "SHARED"; "microsoft.netcore.app"; "10.0.0" ]))
        Assert.AreEqual(inRoot [ "runtime"; "dotnet.exe" ], List.head actual)

    [<Test>]
    let ``a process that isn't dotnet isn't the dotnet of the script`` () =
        let actual = dotnetCandidates false [] (Some(inRoot [ "tools"; "fsautocomplete" ])) None
        Assert.AreEqual(defaultFolders, actual)

    [<Test>]
    let ``the root of a runtime is only taken from a shared Microsoft.NETCore.App folder`` () =
        let candidatesOf runtime = dotnetCandidates false [] None (Some runtime)
        let fileSystemRoot = System.IO.Path.GetPathRoot root
        // as the folder of the .NET Framework, or of a self-contained app
        Assert.AreEqual(defaultFolders, candidatesOf (runtimeDirectory [ "Windows"; "Microsoft.NET"; "Framework64"; "v4.0.30319" ]))
        Assert.AreEqual(defaultFolders, candidatesOf (runtimeDirectory [ "runtime"; "other"; "Microsoft.NETCore.App"; "10.0.0" ]))
        Assert.AreEqual(defaultFolders, candidatesOf fileSystemRoot)
        Assert.AreEqual(defaultFolders, candidatesOf (System.IO.Path.Combine(fileSystemRoot, "Microsoft.NETCore.App", "10.0.0")))

    [<Test>]
    let ``empty, relative and quoted paths are skipped or unquoted`` () =
        // Path.Combine only throws on quotes on .NET Framework, which the tests don't run on
        let actual =
            dotnetCandidates
                false
                [ "DOTNET_HOST_PATH", "dotnet"
                  "DOTNET_ROOT", ""
                  "PATH", paths [ ""; "bin"; "\"" + inRoot [ "quoted" ] + "\""; "" ] ]
                None
                None
        Assert.AreEqual(inRoot [ "quoted"; "dotnet" ] :: defaultFolders, actual)

    let paketRuntimeConfig = """{
  "runtimeOptions": {
    "tfm": "net10.0",
    "framework": {
      "name": "Microsoft.NETCore.App",
      "version": "10.0.0"
    },
    "rollForwardOnNoCandidateFx": 2
  }
}"""

    /// The Microsoft.NETCore.App that paketRuntimeConfig asks for
    let paketApp : ReferenceLoading.Internals.Logic.NetCoreApp =
        { Major = 10; Minor = 0; Patch = 0; RollsForwardToLaterMajor = true }

    [<Test>]
    let ``a runtimeconfig.json gives the Microsoft.NETCore.App of the app`` () =
        Assert.AreEqual(Some paketApp, ReferenceLoading.Internals.Logic.netCoreAppOfRuntimeConfig paketRuntimeConfig)

    [<Test>]
    let ``an app rolls forward to a later major version only when its runtimeconfig.json says so`` () =
        let rollForwardOf (rollForward: string) =
            ReferenceLoading.Internals.Logic.netCoreAppOfRuntimeConfig (paketRuntimeConfig.Replace("\"rollForwardOnNoCandidateFx\": 2", rollForward))
            |> Option.map (fun app -> app.RollsForwardToLaterMajor)
        Assert.AreEqual(Some true, rollForwardOf "\"rollForwardOnNoCandidateFx\": 2")
        Assert.AreEqual(Some false, rollForwardOf "\"rollForwardOnNoCandidateFx\": 1")
        Assert.AreEqual(Some false, rollForwardOf "\"tfm2\": \"none\"")
        Assert.AreEqual(Some true, rollForwardOf "\"rollForward\": \"Major\"")
        Assert.AreEqual(Some true, rollForwardOf "\"rollForward\": \"LatestMajor\"")
        Assert.AreEqual(Some false, rollForwardOf "\"rollForward\": \"LatestMinor\"")

    [<Test>]
    let ``DOTNET_ROLL_FORWARD comes before the roll forward of the runtimeconfig.json`` () =
        let rollForwardWith (variable: string) app =
            (ReferenceLoading.Internals.Logic.withRollForwardVariable variable app).RollsForwardToLaterMajor
        let app = { paketApp with RollsForwardToLaterMajor = false }
        Assert.IsFalse(rollForwardWith null app)
        Assert.IsFalse(rollForwardWith "" app)
        Assert.IsTrue(rollForwardWith "latestMajor" app)
        Assert.IsFalse(rollForwardWith "Minor" paketApp)

    [<Test>]
    let ``a runtimeconfig.json without Microsoft.NETCore.App asks for none`` () =
        Assert.AreEqual(None, ReferenceLoading.Internals.Logic.netCoreAppOfRuntimeConfig """{ "runtimeOptions": { "tfm": "net10.0" } }""")

    [<Test>]
    let ``a dotnet runs the app with a runtime of its major version, or of a later one when it rolls forward`` () =
        let app : ReferenceLoading.Internals.Logic.NetCoreApp = { Major = 10; Minor = 1; Patch = 2; RollsForwardToLaterMajor = false }
        let runsApp = ReferenceLoading.Internals.Logic.runsApp
        Assert.IsTrue(runsApp app [ "9.0.11"; "10.2.0" ])
        Assert.IsTrue(runsApp app [ "10.1.2" ])
        Assert.IsFalse(runsApp app [ "9.0.11"; "10.0.3"; "10.1.1"; "11.0.0" ])
        Assert.IsTrue(runsApp { app with RollsForwardToLaterMajor = true } [ "11.0.0" ])
        Assert.IsFalse(runsApp app [ "not-a-version"; "10.2" ])

    [<Test>]
    let ``a prerelease runtime runs the app only when it is above the version asked for`` () =
        let runsApp = ReferenceLoading.Internals.Logic.runsApp
        Assert.IsFalse(runsApp paketApp [ "10.0.0-rc.2.25502.107" ])
        Assert.IsTrue(runsApp paketApp [ "10.1.0-preview.1.26101.1" ])
        Assert.IsTrue(runsApp paketApp [ "11.0.0-preview.1.26101.1" ])
        Assert.IsFalse(runsApp { paketApp with RollsForwardToLaterMajor = false } [ "11.0.0-preview.1.26101.1" ])

    [<Test>]
    let ``the dotnet picked is the first that runs the app, or else the first unknown, or else the first`` () =
        let pick runs = ReferenceLoading.Internals.Logic.pickDotnet (fun dotnet -> Map.find dotnet runs)
        let runs = Map [ "old", Some false; "link", None; "new", Some true ]
        Assert.AreEqual(Some "new", pick runs [ "old"; "link"; "new" ])
        Assert.AreEqual(Some "link", pick runs [ "old"; "link" ])
        Assert.AreEqual(Some "old", pick runs [ "old" ])
        Assert.AreEqual(None, pick runs [])

    [<Test>]
    let ``the dotnet picked is the first that runs the app, without looking at the next ones`` () =
        let rated = System.Collections.Generic.List<string>()
        let runsTheApp dotnet =
            rated.Add dotnet
            Some(dotnet = "new")
        let picked = ReferenceLoading.Internals.Logic.pickDotnet runsTheApp [ "old"; "new"; "offline" ]
        Assert.AreEqual(Some "new", picked)
        Assert.AreEqual([ "old"; "new" ], List.ofSeq rated)

    /// A dotnet installed in a new temp folder, with runtimes that have their deps.json or not
    let dotnetInstall (runtimes: (string * bool) list) =
        let folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dotnet-lookup-" + string (System.Guid.NewGuid()))
        for runtime, withDepsJson in runtimes do
            let runtimeFolder = System.IO.Path.Combine(folder, "shared", "Microsoft.NETCore.App", runtime)
            System.IO.Directory.CreateDirectory runtimeFolder |> ignore
            if withDepsJson then
                System.IO.File.WriteAllText(System.IO.Path.Combine(runtimeFolder, "Microsoft.NETCore.App.deps.json"), "{}")
        folder

    [<Test>]
    let ``the runtimes of a dotnet are the versions of its shared Microsoft.NETCore.App that have their deps.json`` () =
        let folder = dotnetInstall [ "9.0.11", true; "10.0.2", false ]
        try
            let runtimesOfDotnet = ReferenceLoading.Internals.Logic.runtimesOfDotnet
            Assert.AreEqual(Some [ "9.0.11" ], runtimesOfDotnet (System.IO.Path.Combine(folder, "dotnet")))
            Assert.AreEqual(None, runtimesOfDotnet (System.IO.Path.Combine(folder, "shared", "dotnet")))
        finally
            System.IO.Directory.Delete(folder, true)
