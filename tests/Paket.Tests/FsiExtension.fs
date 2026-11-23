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

    /// What a tool manifest says of paket, which fails when the manifest is refused
    let paketOf manifest =
        match ReferenceLoading.Internals.Logic.paketOfToolManifest manifest with
        | Ok paket -> paket
        | Error reason -> failwith reason

    /// Why a tool manifest is refused, if it is
    let refusalOf manifest =
        match ReferenceLoading.Internals.Logic.paketOfToolManifest manifest with
        | Ok _ -> None
        | Error reason -> Some reason

    /// The paket tool of a manifest, with a version that NuGet doesn't change
    let manifestPaket version rollForward : ReferenceLoading.Internals.Logic.ManifestPaket =
        { Version = version; NormalizedVersion = version; RollForward = rollForward }

    [<Test>]
    let ``a tool manifest gives the version of paket`` () =
        let manifest = """{ "version": 1, "isRoot": true, "tools": { "paket": { "version": "12.0.0", "commands": [ "paket" ] } } }"""
        Assert.AreEqual((Some (manifestPaket "12.0.0" false), true), paketOf manifest)

    [<Test>]
    let ``a tool manifest gives the version of paket as NuGet normalizes it`` () =
        // the version that `dotnet tool restore` records in the toolResolverCache and restores the tool under
        let manifest = """{ "version": 1, "isRoot": true, "tools": { "paket": { "version": "12.0-alpha003", "commands": [ "paket" ] } } }"""
        // and the version as written, for the messages
        let paket: ReferenceLoading.Internals.Logic.ManifestPaket =
            { Version = "12.0-alpha003"; NormalizedVersion = "12.0.0-alpha003"; RollForward = false }
        Assert.AreEqual((Some paket, true), paketOf manifest)

    let normalizedNuGetVersion = ReferenceLoading.Internals.Logic.normalizedNuGetVersion

    [<Test>]
    let ``a version is normalized as NuGet does`` () =
        Assert.AreEqual(Some "12.0.0-alpha003", normalizedNuGetVersion "12.0-alpha003")
        Assert.AreEqual(Some "12.0.0", normalizedNuGetVersion "12.0.0.0")
        Assert.AreEqual(Some "12.0.1", normalizedNuGetVersion "012.0.1")
        Assert.AreEqual(Some "12.0.0", normalizedNuGetVersion "12.0.0+sha")
        Assert.AreEqual(Some "12.0.0", normalizedNuGetVersion "12")
        Assert.AreEqual(Some "1.2.3.4", normalizedNuGetVersion "1.2.3.4")
        // the labels keep their case, a label can start with 0 when it isn't a number, and the metadata can have
        // leading zeros
        Assert.AreEqual(Some "12.0.0-Beta.0a.0", normalizedNuGetVersion " 12.0.0-Beta.0a.0+sha.01 ")
        Assert.AreEqual(Some "12.0.0-rc-1", normalizedNuGetVersion "12.0.0-rc-1+build-2")
        // the white space around the version and its numbers, as String.Trim removes it
        Assert.AreEqual(Some "1.0.0", normalizedNuGetVersion "1. 0")
        Assert.AreEqual(Some "12.0.0-beta", normalizedNuGetVersion "12 .0\t.0 -beta")
        Assert.AreEqual(Some "12.0.0", normalizedNuGetVersion "\u00a012.0.0\u3000")
        Assert.AreEqual(Some "12.0.0-alpha003", normalizedNuGetVersion "12.0\u00a0-alpha003")
        Assert.AreEqual(Some "12.0.0", normalizedNuGetVersion "12\u2028.\u00850.0")

    [<Test>]
    let ``a version that NuGet refuses has no normalized form`` () =
        for version in [ ""; "abc"; "1.2.3.4.5"; "1..0"; "12.0.0-"; "12.0.0-a..b"; "12.0.0-01"; "12.0.0-a_b"; "12.0.0+"; "12.0.0+a_b"; "2147483648.0.0"; "1.-0"; "1 0"; "12.0.0- beta"; "12\u0000.0.0"; "12.0.0\u0000"; "12\u200b.0.0"; "12\ufeff.0.0"; "+1.0" ] do
            Assert.AreEqual(None, normalizedNuGetVersion version, version)

    [<Test>]
    let ``a tool manifest gives the rollForward of paket`` () =
        // as `dotnet tool install --allow-roll-forward` writes it
        let manifest = """{
  "version": 1,
  "isRoot": true,
  "tools": {
    "paket": {
      "version": "12.0.0",
      "commands": [
        "paket"
      ],
      "rollForward": true
    }
  }
}"""
        Assert.AreEqual((Some (manifestPaket "12.0.0" true), true), paketOf manifest)
        Assert.AreEqual((Some (manifestPaket "12.0.0" false), true), paketOf (manifest.Replace("\"rollForward\": true", "\"rollForward\": false")))
        // dotnet reads the property names with their case
        Assert.AreEqual((Some (manifestPaket "12.0.0" false), true), paketOf (manifest.Replace("\"rollForward\"", "\"RollForward\"")))

    [<Test>]
    let ``the rollForward of another tool isn't the one of paket`` () =
        let manifest = """{ "version": 1, "isRoot": false, "tools": { "paket": { "version": "12.0.0", "commands": [ "paket" ] }, "fantomas": { "version": "7.0.0", "commands": [ "fantomas" ], "rollForward": true } } }"""
        Assert.AreEqual((Some (manifestPaket "12.0.0" false), false), paketOf manifest)

    [<Test>]
    let ``a paket property of another tool isn't the paket tool`` () =
        let manifest = """{ "version": 1, "isRoot": false, "tools": { "fantomas": { "version": "7.0.0", "commands": [ "fantomas" ], "paket": { "enabled": true } }, "paket": { "version": "12.0.0", "commands": [ "paket" ], "rollForward": true } } }"""
        Assert.AreEqual((Some (manifestPaket "12.0.0" true), false), paketOf manifest)

    [<Test>]
    let ``a command named paket of another tool isn't paket`` () =
        let manifest = """{ "version": 1, "isRoot": false, "tools": { "paket-wrapper": { "version": "1.0.0", "commands": [ "paket" ] } } }"""
        Assert.AreEqual((None, false), paketOf manifest)

    [<Test>]
    let ``a root tool manifest without paket stops the lookup`` () =
        let manifest = """{ "version": 1, "isRoot": true, "tools": { "fantomas": { "version": "7.0.0", "commands": [ "fantomas" ] } } }"""
        Assert.AreEqual((None, true), paketOf manifest)

    [<Test>]
    let ``a tool manifest that isn't the root lets the lookup go up`` () =
        let manifest = """{ "version": 1, "isRoot": false, "tools": {} }"""
        Assert.AreEqual((None, false), paketOf manifest)

    [<Test>]
    let ``only the tools of "tools" count`` () =
        // a "paket" object with a version in another tool, or out of "tools"
        let manifest = """{ "version": 1, "isRoot": false, "paket": { "version": "8.0.0" }, "tools": { "fantomas": { "version": "7.0.0", "commands": [ "fantomas" ], "paket": { "version": "9.0.0" } }, "paket": { "version": "12.0.0", "commands": [ "paket" ] } } }"""
        Assert.AreEqual((Some (manifestPaket "12.0.0" false), false), paketOf manifest)
        // an object in the paket tool doesn't hide the properties after it
        let manifest = """{ "version": 1, "isRoot": false, "tools": { "paket": { "version": "12.0.0", "commands": [ "paket" ], "settings": { "a": [ {} ] }, "rollForward": true } } }"""
        Assert.AreEqual((Some (manifestPaket "12.0.0" true), false), paketOf manifest)

    [<Test>]
    let ``the property names are case-sensitive, the tool ID isn't`` () =
        // as dotnet, which refuses a manifest without "isRoot" or a paket tool without "version"
        Assert.AreEqual(Some "it has no \"isRoot\" true or false", refusalOf """{ "version": 1, "IsRoot": true, "tools": {} }""")
        Assert.AreEqual(
            Some "the paket tool has no \"version\"",
            refusalOf """{ "version": 1, "isRoot": true, "tools": { "paket": { "Version": "12.0.0", "commands": [ "paket" ] } } }""")
        Assert.AreEqual((None, false), paketOf """{ "version": 1, "isRoot": false, "Tools": { "paket": { "version": "12.0.0", "commands": [ "paket" ] } } }""")
        Assert.AreEqual(
            (Some (manifestPaket "12.0.0" false), false),
            paketOf """{ "version": 1, "isRoot": false, "tools": { "Paket": { "version": "12.0.0", "commands": [ "paket" ] } } }""")
        // of two IDs that differ only by case, the first counts; the first, "pa\u006bet", is paket written with an escape
        Assert.AreEqual(
            (Some (manifestPaket "12.0.0" false), false),
            paketOf """{ "version": 1, "isRoot": false, "tools": { "pa\u006bet": { "version": "12.0.0", "commands": [ "paket" ] }, "Paket": { "version": "12.1.0", "commands": [ "paket" ] } } }""")

    [<Test>]
    let ``the last of a property written twice counts`` () =
        let manifest = """{ "isRoot": true, "version": 1, "isRoot": false, "tools": { "paket": { "version": "12.0.0", "rollForward": true, "commands": [ "paket" ], "version": "12.1.0", "rollForward": false } } }"""
        Assert.AreEqual((Some (manifestPaket "12.1.0" false), false), paketOf manifest)

    [<Test>]
    let ``a tool manifest with comments or trailing commas is refused`` () =
        // as dotnet, which reads it as strict JSON
        for manifest in
            [ "{ // the tools of the repository\n  \"version\": 1, \"isRoot\": true, \"tools\": {} }"
              """{ /* the tools of the repository */ "version": 1, "isRoot": true, "tools": {} }"""
              """{ "version": 1, "isRoot": true, "tools": {}, }"""
              """{ "version": 1, "isRoot": true, "tools": { "paket": { "version": "12.0.0", "commands": [ "paket", ] } } }""" ] do
            Assert.AreEqual(Some "it isn't JSON, which allows no comment or trailing comma", refusalOf manifest, manifest)

    [<Test>]
    let ``a tool manifest that isn't a JSON object is refused`` () =
        // not JSON, as a manifest cut in a string or after a backslash, or an object followed by more text, such as
        // an extra }
        for manifest in [ ""; "{"; """{ "tools": { "paket": { "version": "12.0"""; """{ "tools": { "paket": { "version": "12.0\"""; """{ "tools": { "paket": { "version": "12.0.0" } } } }""" ] do
            Assert.AreEqual(Some "it isn't JSON, which allows no comment or trailing comma", refusalOf manifest, manifest)
        // JSON that isn't an object
        for manifest in [ "[]"; "\"paket\"" ] do
            Assert.AreEqual(Some "it isn't a JSON object", refusalOf manifest, manifest)

    [<Test>]
    let ``a tool manifest without "isRoot" true or false, or with a paket tool without a version, is refused`` () =
        // as dotnet, which doesn't know then whether to look above it
        for manifest in [ """{ "version": 1, "tools": {} }"""; """{ "version": 1, "isRoot": "true", "tools": {} }"""; """{ "version": 1, "isRoot": null, "tools": {} }""" ] do
            Assert.AreEqual(Some "it has no \"isRoot\" true or false", refusalOf manifest, manifest)
        // the first paket tool counts, even when another one, with an ID in another case, has a version
        for manifest in [ """{ "version": 1, "isRoot": false, "tools": { "paket": { "commands": [ "paket" ] }, "Paket": { "version": "12.0.0", "commands": [ "paket" ] } } }"""; """{ "version": 1, "isRoot": false, "tools": { "paket": { "version": 12, "commands": [ "paket" ] } } }"""; """{ "version": 1, "isRoot": false, "tools": { "paket": "12.0.0" } }""" ] do
            Assert.AreEqual(Some "the paket tool has no \"version\"", refusalOf manifest, manifest)

    [<Test>]
    let ``a tool manifest with "tools" that isn't an object is refused`` () =
        // as dotnet, where it would look above it for paket
        for manifest in [ """{ "version": 1, "isRoot": false, "tools": [] }"""; """{ "version": 1, "isRoot": false, "tools": null }""" ] do
            Assert.AreEqual(Some "its \"tools\" isn't an object", refusalOf manifest, manifest)
        // without "tools", as without tools
        Assert.AreEqual((None, true), paketOf """{ "version": 1, "isRoot": true }""")

    [<Test>]
    let ``a tool manifest that gives paket a version that NuGet refuses is refused`` () =
        // the folder of the tool would be looked for under it: an absolute path or a .. would leave the NuGet packages
        // folder, and .NET Framework refuses a " in a path; the version is the JSON one, then the one read
        for json, version in [ "/tmp/x", "/tmp/x"; "../../x", "../../x"; "", ""; "12.0.0\\\"x", "12.0.0\"x" ] do
            let manifest = """{ "version": 1, "isRoot": false, "tools": { "paket": { "version": "VERSION", "commands": [ "paket" ] } } }""".Replace("VERSION", json)
            Assert.AreEqual(Some $"the version '%s{version}' of paket isn't a NuGet version", refusalOf manifest, manifest)

    /// The tool manifest that declares paket, with its paket tool and its folder, or why the lookup stopped at an
    /// invalid manifest
    type FoundLocalTool = Result<(string * ReferenceLoading.Internals.Logic.ManifestPaket * string) option, string>

    /// What localToolOfManifests finds in manifests given with their folder and their content, in order, and the
    /// manifests that it reads
    let localToolOf (manifests: (string * string * string) list) : FoundLocalTool * string list =
        let read = ResizeArray()
        let contents = manifests |> List.map (fun (_, manifest, content) -> manifest, content) |> Map.ofList
        let localTool =
            ReferenceLoading.Internals.Logic.localToolOfManifests
                (fun manifest -> read.Add manifest; Map.find manifest contents)
                (manifests |> List.map (fun (folder, manifest, _) -> folder, manifest))
        localTool, List.ofSeq read

    let paketManifest = """{ "version": 1, "isRoot": false, "tools": { "paket": { "version": "12.0.0", "commands": [ "paket" ] } } }"""
    let rootManifest = """{ "version": 1, "isRoot": true, "tools": {} }"""
    let notRootManifest = """{ "version": 1, "isRoot": false, "tools": {} }"""

    [<Test>]
    let ``the local tool is the one of the first manifest up from the script that declares paket`` () =
        let found: FoundLocalTool = Ok (Some ("/repo/.config/dotnet-tools.json", manifestPaket "12.0.0" false, "/repo"))
        // the manifests above it aren't read
        Assert.AreEqual(
            (found, [ "/repo/src/dotnet-tools.json"; "/repo/.config/dotnet-tools.json" ]),
            localToolOf
                [ "/repo/src", "/repo/src/dotnet-tools.json", notRootManifest
                  "/repo", "/repo/.config/dotnet-tools.json", paketManifest
                  "/", "/dotnet-tools.json", paketManifest ])
        let notFound: FoundLocalTool = Ok None
        Assert.AreEqual((notFound, [ "/repo/dotnet-tools.json" ]), localToolOf [ "/repo", "/repo/dotnet-tools.json", notRootManifest ])
        Assert.AreEqual((notFound, List.empty<string>), localToolOf [])

    [<Test>]
    let ``the lookup of the local tool stops at a root manifest, and at an invalid one with the reason`` () =
        let notFound: FoundLocalTool = Ok None
        Assert.AreEqual(
            (notFound, [ "/repo/dotnet-tools.json" ]),
            localToolOf [ "/repo", "/repo/dotnet-tools.json", rootManifest; "/", "/dotnet-tools.json", paketManifest ])
        // as dotnet, which fails on it
        let invalid: FoundLocalTool = Error "The tool manifest '/repo/dotnet-tools.json' is invalid: it has no \"isRoot\" true or false."
        Assert.AreEqual(
            (invalid, [ "/repo/dotnet-tools.json" ]),
            localToolOf [ "/repo", "/repo/dotnet-tools.json", """{ "version": 1, "tools": {} }"""; "/", "/dotnet-tools.json", paketManifest ])

    let parseJson = ReferenceLoading.Internals.Logic.parseJson

    [<Test>]
    let ``JSON is read with its values and escapes`` () =
        let expected =
            ReferenceLoading.Internals.Logic.JsonObject
                [ "a", ReferenceLoading.Internals.Logic.JsonArray [ ReferenceLoading.Internals.Logic.JsonNumber "-0.5e+10"; ReferenceLoading.Internals.Logic.JsonBool true; ReferenceLoading.Internals.Logic.JsonBool false; ReferenceLoading.Internals.Logic.JsonNull ]
                  "a", ReferenceLoading.Internals.Logic.JsonString "\u00e9\"\\/\b\f\n\r\t" ]
        Assert.AreEqual(Some expected, parseJson " \r\n\t{ \"a\" : [ -0.5e+10, true, false, null ], \"a\": \"\\u00e9\\\"\\\\\\/\\b\\f\\n\\r\\t\" } ")
        // an escape that isn't JSON
        Assert.AreEqual(None, parseJson "\"\\x\"")
        Assert.AreEqual(None, parseJson "\"\\u12\"")
        Assert.AreEqual(None, parseJson "\"\\u12zz\"")

    [<Test>]
    let ``JSON is read as System.Text.Json reads it by default`` () =
        // the numbers of JSON, and a control character with an escape
        for json, value in [ "-0", "-0"; "10.5e-3", "10.5e-3"; "1E+2", "1E+2" ] do
            Assert.AreEqual(Some (ReferenceLoading.Internals.Logic.JsonNumber value), parseJson json, json)
        Assert.AreEqual(Some (ReferenceLoading.Internals.Logic.JsonString "\u0001"), parseJson "\"\\u0001\"")
        // other numbers, a control character in a string, comments, trailing commas, and white space that JSON doesn't
        // have
        for json in [ "01"; "-01"; "1."; ".5"; "+1"; "-"; "1e"; "1e+"; "\"\u0001\""; "\"a\tb\""; "// a\n1"; "/* a */ 1"; "1 // a"; "[ 1, ]"; "{ \"a\": 1, }"; "[ , ]"; "\u00a01"; "\f1" ] do
            Assert.AreEqual(None, parseJson json, json)

    [<Test>]
    let ``JSON is read 64 objects and arrays deep at most`` () =
        let nested depth = String.replicate depth "[" + String.replicate depth "]"
        Assert.IsTrue(Option.isSome (parseJson (nested 64)))
        Assert.AreEqual(None, parseJson (nested 65))
        Assert.AreEqual(None, parseJson (nested 100000))
        let nestedObjects depth = String.replicate depth "{\"a\":" + "1" + String.replicate depth "}"
        Assert.IsTrue(Option.isSome (parseJson (nestedObjects 64)))
        Assert.AreEqual(None, parseJson (nestedObjects 65))
        Assert.AreEqual(None, parseJson (nestedObjects 100000))


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
    let ``--roll-forward Major comes before DOTNET_ROLL_FORWARD and the runtimeconfig.json`` () =
        let rollForwardWith (rollForward: bool) (variable: string) app =
            (ReferenceLoading.Internals.Logic.withRollForward rollForward variable app).RollsForwardToLaterMajor
        let app = { paketApp with RollsForwardToLaterMajor = false }
        Assert.IsTrue(rollForwardWith true null app)
        Assert.IsTrue(rollForwardWith true "Minor" app)
        // without it, the roll forward of the runtimeconfig.json, unless DOTNET_ROLL_FORWARD is set
        Assert.IsTrue(rollForwardWith false null paketApp)
        Assert.IsTrue(rollForwardWith false "" paketApp)
        Assert.IsFalse(rollForwardWith false "Minor" paketApp)
        Assert.IsTrue(rollForwardWith false "LatestMajor" app)

    [<Test>]
    let ``the arguments of dotnet have --roll-forward Major before the paket.dll when it rolls forward`` () =
        let dotnetArguments = ReferenceLoading.Internals.Logic.dotnetArguments
        Assert.AreEqual("\"/tools/paket.dll\" ", dotnetArguments false "/tools/paket.dll")
        Assert.AreEqual("--roll-forward Major \"/tools/paket.dll\" ", dotnetArguments true "/tools/paket.dll")

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
    let ``with --roll-forward Major, the dotnet of a later major runtime is picked whatever DOTNET_ROLL_FORWARD says`` () =
        let older = dotnetInstall [ "9.0.11", true ]
        let later = dotnetInstall [ "11.0.0", true ]
        let tool = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "paket-tool-" + string (System.Guid.NewGuid()))
        System.IO.Directory.CreateDirectory tool |> ignore
        try
            let paketDll = System.IO.Path.Combine(tool, "paket.dll")
            System.IO.File.WriteAllText(System.IO.Path.Combine(tool, "paket.runtimeconfig.json"), paketRuntimeConfig)
            let olderDotnet = System.IO.Path.Combine(older, "dotnet")
            let laterDotnet = System.IO.Path.Combine(later, "dotnet")
            let findDotnet rollForward variable =
                ReferenceLoading.Internals.Logic.findDotnet variable [ olderDotnet; laterDotnet ] rollForward paketDll
            Assert.AreEqual(Some laterDotnet, findDotnet true "Minor")
            Assert.AreEqual(Some laterDotnet, findDotnet true null)
            // without it, DOTNET_ROLL_FORWARD comes before the roll forward of the runtimeconfig.json: no dotnet
            // runs paket, and the first one says what runtime is missing
            Assert.AreEqual(Some olderDotnet, findDotnet false "Minor")
            Assert.AreEqual(Some laterDotnet, findDotnet false null)
        finally
            for folder in [ older; later; tool ] do
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

    /// The Paket that choosePaket picks after the lookup of the local tool reached an invalid manifest, the
    /// warnings it gives and the lookups it makes, with ~/.paket/paket as the global tool of ~/.paket
    let choosePaketAfter invalidManifest localTool toolPathExe globalTool restoredTool =
        let warnings = ResizeArray()
        let lookups = ResizeArray()
        let paket =
            ReferenceLoading.Internals.Logic.choosePaket
                warnings.Add
                invalidManifest
                localTool
                toolPathExe
                (fun paketExe -> paketExe = "~/.paket/paket")
                (fun () -> lookups.Add "global tool"; globalTool)
                (fun () -> lookups.Add "restored tool"; restoredTool)
        paket, List.ofSeq warnings, List.ofSeq lookups

    /// The Paket that choosePaket picks, the warnings it gives and the lookups it makes
    let choosePaket localTool toolPathExe globalTool restoredTool = choosePaketAfter None localTool toolPathExe globalTool restoredTool

    let manifest = "/repo/.config/dotnet-tools.json"
    let notRestored = "Paket 12.0.0, the local tool of '/repo/.config/dotnet-tools.json', is not restored. Please run 'dotnet tool restore' in '/repo'."

    [<Test>]
    let ``without a manifest that declares paket, a paket installed with --tool-path runs, else a global tool, else the most recent one restored`` () =
        let exe = ReferenceLoading.Internals.Logic.PaketExe
        let dll paketDll = ReferenceLoading.Internals.Logic.PaketDll(paketDll, false)
        Assert.AreEqual((Some (exe "/repo/.paket/paket"), List.empty<string>, List.empty<string>), choosePaket None (Some "/repo/.paket/paket") (Some "~/.dotnet/tools/paket") (Some "/restored/paket.dll"))
        Assert.AreEqual((Some (exe "~/.paket/paket"), List.empty<string>, List.empty<string>), choosePaket None (Some "~/.paket/paket") None None)
        Assert.AreEqual((Some (exe "~/.dotnet/tools/paket"), List.empty<string>, [ "global tool" ]), choosePaket None None (Some "~/.dotnet/tools/paket") (Some "/restored/paket.dll"))
        Assert.AreEqual((Some (dll "/restored/paket.dll"), List.empty<string>, [ "global tool"; "restored tool" ]), choosePaket None None None (Some "/restored/paket.dll"))
        Assert.AreEqual((None, List.empty<string>, [ "global tool"; "restored tool" ]), choosePaket None None None None)

    [<Test>]
    let ``the restored local tool runs instead of a paket installed with --tool-path, with a warning unless it is the global tool of ~/.paket`` () =
        let localTool = Some (manifest, Ok ("/local/paket.dll", false))
        let dll paketDll = ReferenceLoading.Internals.Logic.PaketDll(paketDll, false)
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
            (Some (ReferenceLoading.Internals.Logic.PaketDll("/restored/paket.dll", false)),
             [ notRestored + " Running the most recent Paket .NET tool restored instead, which may be another version." ],
             [ "global tool"; "restored tool" ]),
            choosePaket localTool None None (Some "/restored/paket.dll"))
        Assert.AreEqual((None, [ skipped ], [ "global tool"; "restored tool" ]), choosePaket localTool (Some "/repo/.paket/paket") None None)

    [<Test>]
    let ``after an invalid manifest, the Paket that runs without a manifest that declares paket runs, with a warning`` () =
        let invalidManifest = "The tool manifest '/repo/.config/dotnet-tools.json' is invalid: it has no \"isRoot\" true or false."
        let instead paket = $"%s{invalidManifest} Running %s{paket} instead, which may be another version."
        let choosePaket = choosePaketAfter (Some invalidManifest) None
        // a paket installed with --tool-path isn't skipped, as the manifest may not declare paket
        Assert.AreEqual(
            (Some (ReferenceLoading.Internals.Logic.PaketExe "/repo/.paket/paket"), [ instead "/repo/.paket/paket" ], List.empty<string>),
            choosePaket (Some "/repo/.paket/paket") (Some "~/.dotnet/tools/paket") (Some "/restored/paket.dll"))
        Assert.AreEqual(
            (Some (ReferenceLoading.Internals.Logic.PaketExe "~/.dotnet/tools/paket"), [ instead "a global tool" ], [ "global tool" ]),
            choosePaket None (Some "~/.dotnet/tools/paket") (Some "/restored/paket.dll"))
        Assert.AreEqual(
            (Some (ReferenceLoading.Internals.Logic.PaketDll("/restored/paket.dll", false)),
             [ instead "the most recent Paket .NET tool restored" ],
             [ "global tool"; "restored tool" ]),
            choosePaket None None (Some "/restored/paket.dll"))
        Assert.AreEqual((None, List.empty<string>, [ "global tool"; "restored tool" ]), choosePaket None None None)

    [<Test>]
    let ``the local tool runs with the rollForward of its manifest`` () =
        Assert.AreEqual(
            (Some (ReferenceLoading.Internals.Logic.PaketDll("/local/paket.dll", true)), List.empty<string>, List.empty<string>),
            choosePaket (Some (manifest, Ok ("/local/paket.dll", true))) None None (Some "/restored/paket.dll"))

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
