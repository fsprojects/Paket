module Paket.PackageSourceSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Domain
open Paket.PackageSources

[<TestCase("https://nuget.org/api/v2")>]
[<TestCase("https://nuget.org/api/v2/")>]
[<TestCase("https://www.myget.org/F/roslyn-tools/")>]
[<TestCase("http://my.domain/artifactory/api/nuget/nugetsource/")>]
[<TestCase("http://my.domain/artifactory/api/nuget/nuget-local/")>]
[<TestCase("http://my.domain/artifactory/api/nuget/nuget_proxy/")>]
let ``should parse known nuget2 source``(feed : string) =
    let line = sprintf "source %s" feed
    match PackageSource.Parse(line) with
    | NuGet { Url = source; Authentication = _; ProtocolVersion = ProtocolVersion2 } ->
        let quoted = sprintf "source  \"%s\"" feed
        match PackageSource.Parse(quoted) with
        | NuGet { Url = qsource; Authentication = _; ProtocolVersion = ProtocolVersion2 } ->
            source |> shouldEqual qsource
        | NuGet { Url = qsource; Authentication = _; ProtocolVersion = ProtocolVersion3 } ->
            failwithf "%s should be parsed as a v2 protocol when quoted" feed
        | _ -> failwith quoted
    | NuGet { Url = qsource; Authentication = _; ProtocolVersion = ProtocolVersion3 } ->
        failwithf "%s should be parsed as a v2 protocol" feed
    | _ -> failwith feed

[<TestCase("https://api.nuget.org/v3/index.json")>]
[<TestCase("https://dotnet.myget.org/F/roslyn-tools/api/v3/index.json")>]
[<TestCase("http://my.domain/artifactory/api/nuget/v3/nugetsource/index.json")>]
[<TestCase("http://my.domain/artifactory/api/nuget/v3/nuget-local/index.json")>]
[<TestCase("http://my.domain/artifactory/api/nuget/v3/nuget_proxy/index.json")>]
let ``should parse known nuget3 source``(feed : string) =
    let line = sprintf "source %s" feed
    match PackageSource.Parse(line) with
    | NuGet { Url = source; Authentication = _; ProtocolVersion = ProtocolVersion3 } ->
        let quoted = sprintf "source  \"%s\"" feed
        match PackageSource.Parse(quoted) with
        | NuGet { Url = qsource; Authentication = _; ProtocolVersion = ProtocolVersion3 } ->
            source |> shouldEqual qsource
        | NuGet { Url = source; Authentication = _; ProtocolVersion = ProtocolVersion2 } ->
            failwithf "%s should be parsed as a v3 protocol when quoted" feed
        | _ -> failwith quoted
    | NuGet { Url = source; Authentication = _; ProtocolVersion = ProtocolVersion2 } ->
        failwithf "%s should be parsed as a v3 protocol" feed
    | _ -> failwith feed

[<TestCase("https://nuget.org/api/v2", 3)>]
[<TestCase("https://nuget.org/api/v2/", 3)>]
[<TestCase("https://www.myget.org/F/roslyn-tools/", 3)>]
[<TestCase("http://my.domain/artifactory/api/nuget/nugetsource/", 3)>]
[<TestCase("http://my.domain/artifactory/api/nuget/nuget-local/", 3)>]
[<TestCase("http://my.domain/artifactory/api/nuget/nuget_proxy/", 3)>]
let ``should parse protocol v3 even on wellknown nuget v2 source when protocolVersion is explicit`` (feed: string, protocolVersion: int) =
    let line = sprintf "source %s protocolVersion: %d" feed protocolVersion

    match PackageSource.Parse(line) with
    | NuGet { Url = source; Authentication = _; ProtocolVersion = ProtocolVersion3 } ->
        let quoted = sprintf "source  \"%s\" protocolVersion: %d" feed protocolVersion
        match PackageSource.Parse(quoted) with
        | NuGet { Url = qsource; Authentication = _; ProtocolVersion = ProtocolVersion3 } ->
            source |> shouldEqual qsource
        | NuGet { Url = _; Authentication = _; ProtocolVersion = ProtocolVersion2 } ->
            failwithf "%s should be parsed as a v3 protocol when quoted and when protocolVersion explicitly specify v" feed
        | _ -> failwith quoted
    | NuGet { Url = _; Authentication = _; ProtocolVersion = ProtocolVersion2 } ->
        failwithf "%s should be parsed as a v3 protocol when protocolVersion explicitly specify v3" feed
    | e -> failwithf "%s %A" feed e

[<TestCase("https://api.nuget.org/v3/index.json", 2)>]
[<TestCase("https://dotnet.myget.org/F/roslyn-tools/api/v3/index.json", 2)>]
[<TestCase("http://my.domain/artifactory/api/nuget/v3/nugetsource/index.json", 2)>]
[<TestCase("http://my.domain/artifactory/api/nuget/v3/nuget-local/index.json", 2)>]
[<TestCase("http://my.domain/artifactory/api/nuget/v3/nuget_proxy/index.json", 2)>]
let ``should parse protocol v2 even on wellknown nuget v3 source when protocolVersion is explicit`` (feed: string, protocolVersion: int) =
    let line = sprintf "source %s protocolVersion: %d" feed protocolVersion

    match PackageSource.Parse(line) with
    | NuGet { Url = source; Authentication = _; ProtocolVersion = ProtocolVersion2 } ->
        let quoted = sprintf "source  \"%s\" protocolVersion: %d" feed protocolVersion
        match PackageSource.Parse(quoted) with
        | NuGet { Url = qsource; Authentication = _; ProtocolVersion = ProtocolVersion2 } ->
            source |> shouldEqual qsource
        | NuGet { Url = _; Authentication = _; ProtocolVersion = ProtocolVersion3 } ->
            failwithf "%s should be parsed as a v2 protocol when quoted and when protocolVersion explicitly specify v2" feed
        | _ -> failwith quoted
    | NuGet { Url = _; Authentication = _; ProtocolVersion = ProtocolVersion3 } ->
        failwithf "%s should be parsed as a v2 protocol when protocolVersion explicitly specify v2" feed
    | e -> failwithf "%s %A" feed e
