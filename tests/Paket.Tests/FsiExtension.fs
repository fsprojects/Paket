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

    [<Test>]
    let ``a dotnet has an SDK when a folder of its sdk folder has the dotnet.dll of an SDK`` () =
        let folder = dotnetInstall [ "10.0.2", true ]
        try
            let hasSdk = ReferenceLoading.Internals.Logic.hasSdk
            let dotnet = System.IO.Path.Combine(folder, "dotnet")
            let sdk = System.IO.Path.Combine(folder, "sdk", "10.0.100")
            Assert.AreEqual(Some false, hasSdk dotnet)
            System.IO.Directory.CreateDirectory sdk |> ignore
            Assert.AreEqual(Some false, hasSdk dotnet)
            System.IO.File.WriteAllText(System.IO.Path.Combine(sdk, "dotnet.dll"), "")
            Assert.AreEqual(Some true, hasSdk dotnet)
            // as next to a link like /usr/bin/dotnet
            Assert.AreEqual(None, hasSdk (System.IO.Path.Combine(folder, "shared", "dotnet")))
        finally
            System.IO.Directory.Delete(folder, true)

    [<Test>]
    let ``dotnet nuget locals gives the NuGet packages folder`` () =
        let globalPackages = ReferenceLoading.Internals.Logic.globalPackagesOfNuGetLocals
        let packages = inRoot [ "home"; ".nuget"; "packages" ] + string System.IO.Path.DirectorySeparatorChar
        Assert.AreEqual(Some packages, globalPackages ("global-packages: " + packages + "\n"))
        Assert.AreEqual(Some packages, globalPackages ("Welcome to .NET!\r\nglobal-packages: " + packages + "\r\n"))
        Assert.AreEqual(None, globalPackages "error: The local resource 'global-packages' is unknown.\n")
        Assert.AreEqual(None, globalPackages "global-packages: \n")
        Assert.AreEqual(None, globalPackages "global-packages: packages\n")
        Assert.AreEqual(None, globalPackages "")

    [<Test>]
    let ``dotnet nuget locals gives a folder with spaces, without a final separator, or after the info caption of NuGet before 5.5`` () =
        let globalPackages = ReferenceLoading.Internals.Logic.globalPackagesOfNuGetLocals
        let packages = inRoot [ "John Smith"; "packages" ]
        Assert.AreEqual(Some packages, globalPackages ("global-packages: " + packages + "\r\n"))
        Assert.AreEqual(Some packages, globalPackages ("info : global-packages: " + packages + "\n"))

    [<Test>]
    let ``dotnet nuget locals gives no folder when its output wasn't UTF-8`` () =
        let globalPackages = ReferenceLoading.Internals.Logic.globalPackagesOfNuGetLocals
        Assert.AreEqual(None, globalPackages ("global-packages: " + inRoot [ "J\uFFFDr\uFFFDme"; "packages" ] + "\n"))

    /// What runNuGetLocals tells of a stand-in dotnet, sh with commands and the arguments from $2, and how
    /// long it took. The commands write in the file of $1 the process ID of the sleep they leave running,
    /// which is killed afterwards. On Linux and macOS only, where killsLeftOver true acts as on Windows, but
    /// kills nothing.
    let runNuGetLocalsWith (arguments: string list) killsLeftOver exitTimeout outputTimeout (commands: string) =
        if System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform System.Runtime.InteropServices.OSPlatform.Windows then
            Assert.Ignore "The stand-in dotnet is a shell script"
        let pidFile = System.IO.Path.GetTempFileName()
        try
            let startInfo =
                System.Diagnostics.ProcessStartInfo(
                    "/bin/sh",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false)
            for argument in [ "-c"; commands; "sh"; pidFile ] @ arguments do
                startInfo.ArgumentList.Add argument
            let watch = System.Diagnostics.Stopwatch.StartNew()
            let packages = ReferenceLoading.Internals.Logic.runNuGetLocals killsLeftOver exitTimeout outputTimeout startInfo
            packages, watch.Elapsed
        finally
            // which Kill(true) no longer finds once sh exited; still sh when it didn't run sleep yet
            try
                let sleep = System.Diagnostics.Process.GetProcessById(int ((System.IO.File.ReadAllText pidFile).Trim()))
                match sleep.ProcessName with
                | "sleep" | "sh" -> sleep.Kill()
                | _ -> ()
            with _ -> ()
            System.IO.File.Delete pidFile

    let runNuGetLocals killsLeftOver exitTimeout outputTimeout commands =
        runNuGetLocalsWith [] killsLeftOver exitTimeout outputTimeout commands

    [<Test>]
    let ``dotnet nuget locals gives the folder of its output`` () =
        let packages, _ = runNuGetLocals false 10000 5000 "echo 'Welcome to .NET!'; echo 'global-packages: /packages/'"
        Assert.AreEqual(ReferenceLoading.Internals.Logic.Known(Some "/packages/"), packages)

    [<Test>]
    let ``dotnet nuget locals gives its folder at once when a process it left running keeps its output open`` () =
        let packages, elapsed = runNuGetLocals false 10000 30000 "sleep 30 & echo $! > \"$1\"; echo 'global-packages: /packages/'"
        Assert.AreEqual(ReferenceLoading.Internals.Logic.Known(Some "/packages/"), packages)
        Assert.Less(elapsed.TotalSeconds, 15.)

    [<Test>]
    let ``dotnet nuget locals that fails gives no folder`` () =
        let packages, _ = runNuGetLocals false 10000 5000 "echo 'global-packages: /packages/'; exit 1"
        Assert.AreEqual(ReferenceLoading.Internals.Logic.Known None, packages)

    [<Test>]
    let ``the output of a dotnet nuget locals that failed isn't waited for where what it left running can't be killed`` () =
        let packages, elapsed = runNuGetLocals false 10000 30000 "sleep 30 & echo $! > \"$1\"; exit 1"
        Assert.AreEqual(ReferenceLoading.Internals.Logic.Known None, packages)
        Assert.Less(elapsed.TotalSeconds, 15.)

    [<Test>]
    let ``the output of a dotnet nuget locals that failed is waited for where it kills what keeps it open`` () =
        let packages, elapsed = runNuGetLocals true 10000 1000 "sleep 30 & echo $! > \"$1\"; exit 1"
        Assert.AreEqual(ReferenceLoading.Internals.Logic.Known None, packages)
        Assert.GreaterOrEqual(elapsed.TotalSeconds, 0.9)

    [<Test>]
    let ``dotnet nuget locals times out when dotnet doesn't exit, and kills it with what it started`` () =
        let late = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "paket-nuget-locals-" + System.Guid.NewGuid().ToString "N")
        try
            // what sh starts writes the file after 2 seconds, unless it is killed with sh
            let packages, elapsed = runNuGetLocalsWith [ late ] false 500 5000 "(sleep 2; echo > \"$2\") & wait"
            Assert.AreEqual(ReferenceLoading.Internals.Logic.TimedOut None, packages)
            System.Threading.Thread.Sleep(max System.TimeSpan.Zero (System.TimeSpan.FromSeconds 3. - elapsed))
            Assert.IsFalse(System.IO.File.Exists late)
        finally
            System.IO.File.Delete late

    [<Test>]
    let ``dotnet nuget locals without a folder stops waiting at the end of its output`` () =
        let packages, elapsed = runNuGetLocals false 10000 30000 "echo 'Welcome to .NET!'"
        Assert.AreEqual(ReferenceLoading.Internals.Logic.Known None, packages)
        Assert.Less(elapsed.TotalSeconds, 15.)

    [<Test>]
    let ``dotnet nuget locals stops waiting at the end of its output where it kills what keeps it open`` () =
        let packages, elapsed = runNuGetLocals true 10000 30000 "echo 'global-packages: /packages/'"
        Assert.AreEqual(ReferenceLoading.Internals.Logic.Known(Some "/packages/"), packages)
        Assert.Less(elapsed.TotalSeconds, 15.)

    [<Test>]
    let ``dotnet nuget locals that failed stops waiting at the end of its output where it kills what keeps it open`` () =
        let packages, elapsed = runNuGetLocals true 10000 30000 "exit 1"
        Assert.AreEqual(ReferenceLoading.Internals.Logic.Known None, packages)
        Assert.Less(elapsed.TotalSeconds, 15.)

    [<Test>]
    let ``dotnet nuget locals that times out keeps the folder that it gave`` () =
        let packages, _ = runNuGetLocals false 1000 5000 "echo 'global-packages: /packages/'; sleep 5"
        Assert.AreEqual(ReferenceLoading.Internals.Logic.TimedOut(Some "/packages/"), packages)

    [<Test>]
    let ``dotnet nuget locals doesn't time out when dotnet exits while a process it left running keeps its output open`` () =
        let packages, _ = runNuGetLocals false 10000 500 "sleep 30 & echo $! > \"$1\"; echo 'Welcome to .NET!'"
        Assert.AreEqual(ReferenceLoading.Internals.Logic.Known None, packages)

    [<Test>]
    let ``dotnet nuget locals keeps its folder where it waits for the end of its output to kill what keeps it open`` () =
        let packages, elapsed = runNuGetLocals true 10000 1000 "sleep 30 & echo $! > \"$1\"; echo 'global-packages: /packages/'"
        Assert.AreEqual(ReferenceLoading.Internals.Logic.Known(Some "/packages/"), packages)
        Assert.GreaterOrEqual(elapsed.TotalSeconds, 0.9)

    [<Test>]
    let ``dotnet nuget locals waits for its folder and the end of its output once in all after dotnet exits where it kills what keeps it open`` () =
        // the folder that comes 1 second after sh exits, from what it left running
        let packages, elapsed =
            runNuGetLocals true 10000 2000 "(sleep 1; echo 'global-packages: /packages/'; exec sleep 30) & echo $! > \"$1\""
        Assert.AreEqual(ReferenceLoading.Internals.Logic.Known(Some "/packages/"), packages)
        // and not 2 more seconds after the folder
        Assert.Less(elapsed.TotalSeconds, 2.9)

    [<Test>]
    let ``dotnet nuget locals without a folder waits once for the end of its output to kill what keeps it open`` () =
        let packages, elapsed = runNuGetLocals true 10000 2000 "sleep 30 & echo $! > \"$1\"; echo 'Welcome to .NET!'"
        Assert.AreEqual(ReferenceLoading.Internals.Logic.Known None, packages)
        // and not once more after the 2 seconds without its folder
        Assert.Less(elapsed.TotalSeconds, 3.5)

    /// The function of nugetPackagesFolderOnce, with the folders it ran nugetPackagesFolder for
    let nugetPackagesFolderOnce (nugetPackagesFolder: string -> ReferenceLoading.Internals.Logic.NuGetPackagesFolder) =
        let runs = ResizeArray()
        ReferenceLoading.Internals.Logic.nugetPackagesFolderOnce (fun folder -> runs.Add folder; nugetPackagesFolder folder), runs

    [<Test>]
    let ``the NuGet packages folder of a folder is asked for once per resolution, even when there is none`` () =
        let known = ReferenceLoading.Internals.Logic.Known
        // the folder of the manifest, which gives none, then the one of the script, still asked after it
        let nugetPackages, runs =
            nugetPackagesFolderOnce (function
                | "/repo/scripts" -> known (Some "/packages")
                | _ -> known None)
        Assert.AreEqual(None, nugetPackages "/repo")
        Assert.AreEqual(None, nugetPackages "/repo")
        Assert.AreEqual(Some "/packages", nugetPackages "/repo/scripts")
        Assert.AreEqual(Some "/packages", nugetPackages "/repo/scripts")
        Assert.AreEqual([ "/repo"; "/repo/scripts" ], List.ofSeq runs)

    [<Test>]
    let ``the NuGet packages folder is no longer asked for after dotnet nuget locals timed out`` () =
        let nugetPackages, runs = nugetPackagesFolderOnce (fun _ -> ReferenceLoading.Internals.Logic.TimedOut None)
        Assert.AreEqual(None, nugetPackages "/repo")
        Assert.AreEqual(None, nugetPackages "/repo/scripts")
        Assert.AreEqual(None, nugetPackages "/repo")
        Assert.AreEqual([ "/repo" ], List.ofSeq runs)

    [<Test>]
    let ``a NuGet packages folder told before a timeout is kept after it`` () =
        let nugetPackages, runs =
            nugetPackagesFolderOnce (function
                | "/repo" -> ReferenceLoading.Internals.Logic.Known(Some "/packages")
                | _ -> ReferenceLoading.Internals.Logic.TimedOut None)
        Assert.AreEqual(Some "/packages", nugetPackages "/repo")
        Assert.AreEqual(None, nugetPackages "/repo/scripts")
        Assert.AreEqual(None, nugetPackages "/other")
        Assert.AreEqual(Some "/packages", nugetPackages "/repo")
        Assert.AreEqual([ "/repo"; "/repo/scripts" ], List.ofSeq runs)

    [<Test>]
    let ``the NuGet packages folder of a dotnet nuget locals that timed out is kept, and no other folder is asked for`` () =
        let nugetPackages, runs = nugetPackagesFolderOnce (fun _ -> ReferenceLoading.Internals.Logic.TimedOut(Some "/packages"))
        Assert.AreEqual(Some "/packages", nugetPackages "/repo")
        Assert.AreEqual(None, nugetPackages "/repo/scripts")
        Assert.AreEqual(Some "/packages", nugetPackages "/repo")
        Assert.AreEqual([ "/repo" ], List.ofSeq runs)

    [<Test>]
    let ``the toolResolverCache gives the paket.dll that dotnet tool restore recorded for a version`` () =
        // as the SDK writes it, with \u escapes for some characters
        let cache = """[{"Version":"12.0.0","TargetFramework":"net10.0","RuntimeIdentifier":"any","Name":"paket","Runner":"dotnet","PathToExecutable":"C:\\Users\\J\u00E9r\u00F4me\\.nuget\\packages\\paket\\12.0.0\\tools\\net10.0\\any\\paket.dll"},{"Version":"12.0.0","TargetFramework":"net11.0","RuntimeIdentifier":"any","Name":"paket","Runner":"dotnet","PathToExecutable":"/a {b}/paket/12.0.0/tools/net10.0/any/paket.dll"},{"Version":"12.1.0","TargetFramework":"net10.0","RuntimeIdentifier":"any","Name":"paket","Runner":"dotnet","PathToExecutable":"/other/paket.dll"}]"""
        let paketDlls = ReferenceLoading.Internals.Logic.paketDllsOfToolResolverCache
        Assert.AreEqual(
            [ @"C:\Users\Jérôme\.nuget\packages\paket\12.0.0\tools\net10.0\any\paket.dll"
              "/a {b}/paket/12.0.0/tools/net10.0/any/paket.dll" ],
            paketDlls "12.0.0" cache)
        Assert.AreEqual([ "/other/paket.dll" ], paketDlls "12.1.0" cache)
        Assert.AreEqual(List.empty<string>, paketDlls "12.2.0" cache)
        Assert.AreEqual(List.empty<string>, paketDlls "12.0.0" "")

    [<Test>]
    let ``the toolResolverCache gives only the paket command that dotnet runs`` () =
        let cache = """[{"Version":"1.0.0","TargetFramework":"net10.0","RuntimeIdentifier":"any","Name":"paket-wrapper","Runner":"dotnet","PathToExecutable":"/wrapper/paket.dll"},{"Version":"1.0.0","TargetFramework":"net10.0","RuntimeIdentifier":"linux-x64","Name":"paket","Runner":"executable","PathToExecutable":"/native/paket"}]"""
        Assert.AreEqual(List.empty<string>, ReferenceLoading.Internals.Logic.paketDllsOfToolResolverCache "1.0.0" cache)

    [<Test>]
    let ``the Paket .NET tool is looked for in the NuGet packages folder, then in the one of the user profile, once each`` () =
        let paketFolders = ReferenceLoading.Internals.Logic.paketFolders
        let home = inRoot [ "home" ]
        let homePaket = inRoot [ "home"; ".nuget"; "packages"; "paket" ]
        let configured = inRoot [ "repo"; "packages" ]
        Assert.AreEqual([ inRoot [ "repo"; "packages"; "paket" ]; homePaket ], paketFolders false (Some configured) home)
        // dotnet nuget locals ends the default folder with a separator
        let defaultFolder = inRoot [ "home"; ".nuget"; "packages" ] + string System.IO.Path.DirectorySeparatorChar
        Assert.AreEqual([ homePaket ], paketFolders false (Some defaultFolder) home)
        Assert.AreEqual([ homePaket ], paketFolders false None home)
        Assert.AreEqual([ inRoot [ "repo"; "packages"; "paket" ] ], paketFolders false (Some configured) null)
        Assert.AreEqual(List.empty<string>, paketFolders false None "")

    [<Test>]
    let ``on Windows the NuGet packages folders are the same without case`` () =
        let paketFolders = ReferenceLoading.Internals.Logic.paketFolders
        let upperCase = inRoot [ "HOME"; ".nuget"; "packages" ]
        Assert.AreEqual(1, List.length (paketFolders true (Some upperCase) (inRoot [ "home" ])))
        Assert.AreEqual(2, List.length (paketFolders false (Some upperCase) (inRoot [ "home" ])))

    /// The Paket that choosePaket picks, the warnings it gives and the lookups it makes, with
    /// ~/.paket/paket as the global tool of ~/.paket
    let choosePaket localTool toolPathExe globalTool restoredTool =
        let warnings = ResizeArray()
        let lookups = ResizeArray()
        let paket =
            ReferenceLoading.Internals.Logic.choosePaket
                warnings.Add
                localTool
                toolPathExe
                (fun paketExe -> paketExe = "~/.paket/paket")
                (fun () -> lookups.Add "global tool"; globalTool)
                (fun () -> lookups.Add "restored tool"; restoredTool)
        paket, List.ofSeq warnings, List.ofSeq lookups

    let manifest = "/repo/.config/dotnet-tools.json"
    let notRestored = "Paket 12.0.0, the local tool of '/repo/.config/dotnet-tools.json', is not restored. Please run 'dotnet tool restore' in '/repo'."

    [<Test>]
    let ``without a manifest that declares paket, a paket installed with --tool-path runs, else a global tool, else the most recent one restored`` () =
        let exe = ReferenceLoading.Internals.Logic.PaketExe
        let dll = ReferenceLoading.Internals.Logic.PaketDll
        Assert.AreEqual((Some (exe "/repo/.paket/paket"), List.empty<string>, List.empty<string>), choosePaket None (Some "/repo/.paket/paket") (Some "~/.dotnet/tools/paket") (Some "/restored/paket.dll"))
        Assert.AreEqual((Some (exe "~/.paket/paket"), List.empty<string>, List.empty<string>), choosePaket None (Some "~/.paket/paket") None None)
        Assert.AreEqual((Some (exe "~/.dotnet/tools/paket"), List.empty<string>, [ "global tool" ]), choosePaket None None (Some "~/.dotnet/tools/paket") (Some "/restored/paket.dll"))
        Assert.AreEqual((Some (dll "/restored/paket.dll"), List.empty<string>, [ "global tool"; "restored tool" ]), choosePaket None None None (Some "/restored/paket.dll"))
        Assert.AreEqual((None, List.empty<string>, [ "global tool"; "restored tool" ]), choosePaket None None None None)

    [<Test>]
    let ``the restored local tool runs instead of a paket installed with --tool-path, with a warning unless it is the global tool of ~/.paket`` () =
        let localTool = Some (manifest, Ok "/local/paket.dll")
        let dll = ReferenceLoading.Internals.Logic.PaketDll
        Assert.AreEqual(
            (Some (dll "/local/paket.dll"),
             [ "Ignoring /repo/.paket/paket: '/repo/.config/dotnet-tools.json' declares paket, which runs instead. Delete it if it is an older Paket, such as the paket.exe or bootstrapper of Paket 11." ],
             List.empty<string>),
            choosePaket localTool (Some "/repo/.paket/paket") (Some "~/.dotnet/tools/paket") (Some "/restored/paket.dll"))
        Assert.AreEqual((Some (dll "/local/paket.dll"), List.empty<string>, List.empty<string>), choosePaket localTool (Some "~/.paket/paket") None None)
        Assert.AreEqual((Some (dll "/local/paket.dll"), List.empty<string>, List.empty<string>), choosePaket localTool None None None)

    [<Test>]
    let ``when the local tool isn't restored, a global tool runs, else the most recent one restored, with a warning, and a paket installed with --tool-path is still skipped`` () =
        let localTool = Some (manifest, Error notRestored)
        let skipped = "Ignoring /repo/.paket/paket: '/repo/.config/dotnet-tools.json' declares paket. Delete it if it is an older Paket, such as the paket.exe or bootstrapper of Paket 11."
        Assert.AreEqual(
            (Some (ReferenceLoading.Internals.Logic.PaketExe "~/.dotnet/tools/paket"),
             [ skipped; notRestored + " Running a global tool instead, which may be another version." ],
             [ "global tool" ]),
            choosePaket localTool (Some "/repo/.paket/paket") (Some "~/.dotnet/tools/paket") (Some "/restored/paket.dll"))
        // the global tool of ~/.paket, which the walk up from a script in the home folder finds too
        Assert.AreEqual(
            (Some (ReferenceLoading.Internals.Logic.PaketExe "~/.paket/paket"),
             [ notRestored + " Running a global tool instead, which may be another version." ],
             [ "global tool" ]),
            choosePaket localTool (Some "~/.paket/paket") (Some "~/.paket/paket") (Some "/restored/paket.dll"))
        Assert.AreEqual(
            (Some (ReferenceLoading.Internals.Logic.PaketDll "/restored/paket.dll"),
             [ notRestored + " Running the most recent Paket .NET tool restored instead, which may be another version." ],
             [ "global tool"; "restored tool" ]),
            choosePaket localTool None None (Some "/restored/paket.dll"))
        Assert.AreEqual((None, [ skipped ], [ "global tool"; "restored tool" ]), choosePaket localTool (Some "/repo/.paket/paket") None None)

    [<Test>]
    let ``when no paket is found, the error asks to restore the local tool, or else gives the locations looked in, if any`` () =
        let paketNotFound = ReferenceLoading.Internals.Logic.paketNotFound
        Assert.AreEqual(notRestored, paketNotFound (Some notRestored) "/repo" [ "~/.dotnet/tools/paket" ])
        Assert.AreEqual(
            "Paket was not found in '/repo' or a parent directory, as a local tool, or in those locations:\n\n - ~/.dotnet/tools/paket"
            + System.Environment.NewLine
            + " - ~/.nuget/packages/paket\n\nPlease install the Paket .NET tool: dotnet tool install paket",
            paketNotFound None "/repo" [ "~/.dotnet/tools/paket"; "~/.nuget/packages/paket" ])
        Assert.AreEqual(
            "Paket was not found in '/repo' or a parent directory, as a local tool.\n\nPlease install the Paket .NET tool: dotnet tool install paket",
            paketNotFound None "/repo" [])

    [<Test>]
    let ``a global tool is looked for in ~/.dotnet/tools and then ~/.paket, and not without a user profile`` () =
        let globalToolPaths = ReferenceLoading.Internals.Logic.globalToolPaths
        Assert.AreEqual(
            [ inRoot [ "home"; ".dotnet"; "tools"; "paket" ]; inRoot [ "home"; ".paket"; "paket" ] ],
            globalToolPaths "paket" (inRoot [ "home" ]))
        Assert.AreEqual(List.empty<string>, globalToolPaths "paket" null)
        // not in the current folder
        Assert.AreEqual(List.empty<string>, globalToolPaths "paket" "")
