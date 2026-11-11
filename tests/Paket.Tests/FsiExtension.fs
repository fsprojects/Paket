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

