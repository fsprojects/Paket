module Paket.RemotePushUrlSpecs

open Paket.RemoteUpload
open NUnit.Framework
open System.Xml
open FsUnit

let nugetUrl = "https://nuget.org"
let defaultEndpoint = "/api/v2/package" 
let customHost = "http://my.host.com"
let customEndpoint = "/my/feed"
let customHostWithEndpoint = (customHost + customEndpoint)

[<Test>]
let ``default result is nuget host with default endpoint`` () =
    GetUrlWithEndpoint None None 
    |> shouldEqual (nugetUrl + defaultEndpoint)

[<Test>]
let ``no host with custom endpoint yields nuget host with default endpoint`` () =
    GetUrlWithEndpoint None (Some customEndpoint) 
    |> shouldEqual (nugetUrl + defaultEndpoint)

[<Test>]
let ``custom host with no endpoint yields custom host with default endpoint`` () =
    GetUrlWithEndpoint (Some customHost) None 
    |> shouldEqual (customHost + defaultEndpoint) 

[<Test>]
let ``custom host with custom endpoint yields custom host with custom endpoint`` () =
    GetUrlWithEndpoint (Some customHost) (Some customEndpoint) 
    |> shouldEqual (customHost + customEndpoint)

[<Test>]
let ``custom host that includes endpoint and no custom enpoint does not append default endpoint`` () =
    GetUrlWithEndpoint (Some customHostWithEndpoint) None 
    |> shouldEqual customHostWithEndpoint

[<Test>]
let ``custom host that includes endpoint and custom endpoint yields host + customendpoint`` () =
    GetUrlWithEndpoint (Some customHostWithEndpoint) (Some customEndpoint)
    |> shouldEqual (customHostWithEndpoint + customEndpoint)

[<Test>]
let ``can combine host and endpoint with missing leading slash on endpoint`` () =
    let noSlashes = "my/feed"
    GetUrlWithEndpoint (Some customHost) (Some noSlashes) 
    |> shouldEqual (customHost + "/" + noSlashes)

[<Test>]
let ``can combine host and endpoint with leading slash on endpoint and trailing slash on host`` () =
    let slashyHost = customHost + "/"
    GetUrlWithEndpoint (Some slashyHost) (Some customEndpoint) 
    |> shouldEqual (customHost + customEndpoint)

