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
    | NuGetV2 { Url = source; Authentication = _ } ->
        let quoted = sprintf "source  \"%s\"" feed
        match PackageSource.Parse(quoted) with
        | NuGetV2 { Url = qsource; Authentication = _ } -> 
            source |> shouldEqual qsource
        | _ -> failwith quoted
    | _ -> failwith feed  

[<TestCase("https://api.nuget.org/v3/index.json")>]
[<TestCase("https://dotnet.myget.org/F/roslyn-tools/api/v3/index.json")>]
[<TestCase("http://my.domain/artifactory/api/nuget/v3/nugetsource/index.json")>]
[<TestCase("http://my.domain/artifactory/api/nuget/v3/nuget-local/index.json")>]
[<TestCase("http://my.domain/artifactory/api/nuget/v3/nuget_proxy/index.json")>]
let ``should parse known nuget3 source``(feed : string) =
    let line = sprintf "source %s" feed
    match PackageSource.Parse(line) with
    | NuGetV3 { Url = source; Authentication = _ } ->
        let quoted = sprintf "source  \"%s\"" feed
        match PackageSource.Parse(quoted) with
        | NuGetV3 { Url = qsource; Authentication = _ } -> 
            source |> shouldEqual qsource
        | _ -> failwith quoted
    | _ -> failwith feed  

[<Test>]
let ``should parse unquoted local source path containing spaces``() =
    let path = @"C:\Program Files\dotnet\sdk\NuGetFallbackFolder"
    let line = sprintf "source %s" path
    match PackageSource.Parse(line) with
    | LocalNuGet(source, _) -> source |> shouldEqual path
    | other -> failwithf "expected LocalNuGet but got %A" other

[<Test>]
let ``should parse unquoted local source path containing spaces with trailing slash``() =
    let path = @"C:\My directory\NuGet"
    let line = sprintf "source %s/" path
    match PackageSource.Parse(line) with
    | LocalNuGet(source, _) -> source |> shouldEqual path
    | other -> failwithf "expected LocalNuGet but got %A" other

[<Test>]
let ``should parse unquoted source with space in path and credentials on same line``() =
    let path = @"C:\My directory\NuGet"
    let line = sprintf "source %s username: \"user\" password: \"pass\"" path
    match PackageSource.Parse(line) with
    | LocalNuGet(source, _) -> source |> shouldEqual path
    | other -> failwithf "expected LocalNuGet but got %A" other