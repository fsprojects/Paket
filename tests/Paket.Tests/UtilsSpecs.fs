module Paket.UtilsSpecs

open System.IO
open Paket
open NUnit.Framework
open FsUnit
open System
open System.Net
open Chessie.ErrorHandling
open System.Xml

[<Test>]
let ``createRelativePath should handle spaces``() =
    "C:/some file" 
    |> createRelativePath "C:/a/b" 
    |> shouldEqual "..\\some file"

[<Test>]
let ``createRelativePath should handle hash characters``() =
    "C:/some#file"
    |> createRelativePath "C:/a/b"
    |> shouldEqual "..\\some#file"

[<Test>]
let ``createRelativePath should handle ampersand characters``() =
    "C:/some&file"
    |> createRelativePath "C:/a/b"
    |> shouldEqual "..\\some&file"

[<Test>]
let ``createRelativePath should handle percent characters``() =
    "C:/some%file"
    |> createRelativePath "C:/a/b"
    |> shouldEqual "..\\some%file"
        
[<Test>]
let ``normalize path with home directory``() =
    "~/data" 
    |> Utils.normalizeLocalPath
    |> shouldEqual (AbsolutePath (Path.Combine(GetHomeDirectory(), "data")))
        
[<Test>]
let ``relative local path is returned as is``() =
    "Externals/NugetStore" 
    |> normalizeLocalPath
    |> shouldEqual (RelativePath "Externals/NugetStore")
    
[<Test>]
let ``absolute path with drive letter``() =
    "c:\\Store" 
    |> normalizeLocalPath
    |> match System.Environment.OSVersion.Platform with
        | System.PlatformID.Win32NT -> shouldEqual (AbsolutePath "c:\\Store")
        | _ -> shouldEqual (RelativePath "c:\\Store")
    
[<Test>]
let ``relative path with drive letter``() =
    "..\\Store" 
    |> normalizeLocalPath
    |> shouldEqual (RelativePath "..\\Store")
    
[<Test>]
let ``relative path with local identifier``() =
    ".\\Store" 
    |> normalizeLocalPath
    |> shouldEqual (RelativePath ".\\Store")

[<Test>]
let ``SMB path is returned as absolute path``() =
    "\\\\server\\Store" 
    |> normalizeLocalPath
    |> match System.Environment.OSVersion.Platform with
        | System.PlatformID.Win32NT | System.PlatformID.Win32S -> shouldEqual (AbsolutePath "\\\\server\\Store")
        | _ -> shouldEqual (RelativePath "\\\\server\\Store")
    
[<Test>]
let ``absolute path on unixoid systems``() =
    "/server/Store" 
    |> normalizeLocalPath
    |> shouldEqual (AbsolutePath "/server/Store")
    
[<Test>]
let ``relative path with local identifier on unxoid systems``() =
    "./Store" 
    |> normalizeLocalPath
    |> shouldEqual (RelativePath "./Store")

[<Test>]
#if NO_UNIT_PLATFORMATTRIBUTE
[<Ignore "PlatformAttribute not supported by netstandard NUnit">]
#else
[<Platform "Mono">]
#endif
let ``mono runtime reported on mono platform``() =
    isMonoRuntime |>
    shouldEqual true

[<Test>]
#if NO_UNIT_PLATFORMATTRIBUTE
[<Ignore "PlatformAttribute not supported by netstandard NUnit">]
#else
[<Platform "Net">]
#endif
let ``mono runtime not reported on net platform``() =
    isMonoRuntime |>
    shouldEqual false

type DisposableEnvVar(name, oldValue, newValue) =
    new(name) =
        new DisposableEnvVar(name, null)
    new(name, value) =
        let current = Environment.GetEnvironmentVariable name
        Environment.SetEnvironmentVariable(name, value)
        new DisposableEnvVar(name, current, value)
    interface IDisposable with
        member this.Dispose () =
            Environment.SetEnvironmentVariable(name, oldValue)

/// envProxies reads the upper case variable first: on Linux it would win over the one of the test
let proxyEnvVar (name: string) value =
    // Windows has a single variable for both names: clear it, then set it, and restore in reverse
    let upper = new DisposableEnvVar(name.ToUpperInvariant())
    let lower = new DisposableEnvVar(name.ToLowerInvariant(), value)
    { new IDisposable with
        member __.Dispose() =
            (lower :> IDisposable).Dispose()
            (upper :> IDisposable).Dispose() }

[<Test>]
let ``disposable env var should set value``() =
    let name = Guid.NewGuid().ToString()
    use v = new DisposableEnvVar(name, "new")
    Environment.GetEnvironmentVariable name |>
    shouldEqual "new"

[<Test>]
let ``disposable env var should override value``() =
    let name = Guid.NewGuid().ToString()
    Environment.SetEnvironmentVariable(name, "old")
    use v = new DisposableEnvVar(name, "new")
    Environment.GetEnvironmentVariable name |>
    shouldEqual "new"

[<Test>]
let ``disposable env var should delete value``() =
    let name = Guid.NewGuid().ToString()
    Environment.SetEnvironmentVariable(name, "old")
    use v = new DisposableEnvVar(name)
    Environment.GetEnvironmentVariable name |>
    shouldEqual null

[<Test>]
let ``disposable env var should restore previous value``() =
    let name = Guid.NewGuid().ToString()
    Environment.SetEnvironmentVariable(name, "old")
    let f () =
        use v = new DisposableEnvVar(name, "new")
        ()
    f ()
    Environment.GetEnvironmentVariable name |>
    shouldEqual "old"

[<Test>]
let ``no env proxy without http_proxy env var``() =
    use v = proxyEnvVar "http_proxy" null
    envProxies().TryFind "http" |>
    shouldEqual None

[<Test>]
let ``no env proxy without https_proxy env var``() =
    use v = proxyEnvVar "https_proxy" null
    envProxies().TryFind "https" |>
    shouldEqual None

[<Test>]
let ``get http env proxy no port nor credentials``() =
    use v = proxyEnvVar "http_proxy" "http://proxy.local"
    use w = proxyEnvVar "no_proxy" null
    let pOpt = envProxies().TryFind "http"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 0
    p.Credentials |> shouldEqual null

[<Test>]
let ``get https env proxy no port nor credentials``() =
    use v = proxyEnvVar "https_proxy" "https://proxy.local"
    use w = proxyEnvVar "no_proxy" null
    let pOpt = envProxies().TryFind "https"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local:443"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 0
    p.Credentials |> shouldEqual null

[<Test>]
let ``get http env proxy with port no credentials``() =
    use v = proxyEnvVar "http_proxy" "http://proxy.local:8080"
    use w = proxyEnvVar "no_proxy" null
    let pOpt = envProxies().TryFind "http"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local:8080"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 0
    p.Credentials |> shouldEqual null

[<Test>]
let ``get https env proxy with port no credentials``() =
    use v = proxyEnvVar "https_proxy" "https://proxy.local:8080"
    use w = proxyEnvVar "no_proxy" null
    let pOpt = envProxies().TryFind "https"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local:8080"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 0
    p.Credentials |> shouldEqual null

[<Test>]
let ``get http env proxy with port and credentials``() =
    let password = "p@ssw0rd:"
    use v = proxyEnvVar "http_proxy" (sprintf "http://user:%s@proxy.local:8080" (Uri.EscapeDataString password))
    use w = proxyEnvVar "no_proxy" null
    let pOpt = envProxies().TryFind "http"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local:8080"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 0
    let credentials = p.Credentials :?> NetworkCredential
    credentials.UserName |> shouldEqual "user"
    credentials.Password |> shouldEqual password

[<Test>]
let ``get https env proxy with port and credentials``() =
    let password = "p@ssw0rd:"
    use v = proxyEnvVar "https_proxy" (sprintf "https://user:%s@proxy.local:8080" (Uri.EscapeDataString password))
    use w = proxyEnvVar "no_proxy" null
    let pOpt = envProxies().TryFind "https"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local:8080"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 0
    let credentials = p.Credentials :?> NetworkCredential
    credentials.UserName |> shouldEqual "user"
    credentials.Password |> shouldEqual password

[<Test>]
let ``get http env proxy with bypass list``() =
    use v = proxyEnvVar "http_proxy" "http://proxy.local:8080"
    use w = proxyEnvVar "no_proxy" ".local,localhost"
    let pOpt = envProxies().TryFind "http"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local:8080"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 2
    p.IsBypassed(new Uri("http://feed.local")) |> shouldEqual true
    p.IsBypassed(new Uri("http://feed.example.com")) |> shouldEqual false
    p.Credentials |> shouldEqual null

[<Test>]
let ``get http env proxy with bypass list containing wildcards``() =
    use v = proxyEnvVar "http_proxy" "http://proxy.local:8080"
    use w = proxyEnvVar "no_proxy" ".local,localhost,*.asdf.com"
    let pOpt = envProxies().TryFind "http"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local:8080"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 3
    p.IsBypassed(new Uri("http://feed.asdf.com")) |> shouldEqual true
    p.Credentials |> shouldEqual null

[<Test>]
let ``no_proxy wildcard bypass entry actually bypasses matching subdomain``() =
    use v = proxyEnvVar "http_proxy" "http://proxy.local:8080"
    use w = proxyEnvVar "no_proxy" "*.internal.company.com"
    let pOpt = envProxies().TryFind "http"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    // were escaped incorrectly and therefore never matched any host, even though they should
    // bypass the proxy for matching subdomains.
    p.IsBypassed(new Uri("http://nuget.internal.company.com")) |> shouldEqual true
    p.IsBypassed(new Uri("http://example.com")) |> shouldEqual false

[<Test>]
let ``no_proxy host entry bypasses that host and its subdomains only``() =
    use v = proxyEnvVar "http_proxy" "http://proxy.local:8080"
    use w = proxyEnvVar "no_proxy" "nuget.internal"
    let p = envProxies().TryFind "http" |> Option.get
    p.IsBypassed(new Uri("http://nuget.internal/v3/index.json")) |> shouldEqual true
    p.IsBypassed(new Uri("http://nuget.internal:8080")) |> shouldEqual true
    p.IsBypassed(new Uri("http://feed.nuget.internal")) |> shouldEqual true
    p.IsBypassed(new Uri("http://mynuget.internal")) |> shouldEqual false
    p.IsBypassed(new Uri("http://nuget.internal.example.com")) |> shouldEqual false
    p.GetProxy(new Uri("http://mynuget.internal")) |> shouldEqual (new Uri("http://proxy.local:8080"))

[<Test>]
let ``no_proxy entries are trimmed``() =
    use v = proxyEnvVar "http_proxy" "http://proxy.local:8080"
    use w = proxyEnvVar "no_proxy" "localhost, nuget.internal ,"
    let p = envProxies().TryFind "http" |> Option.get
    p.BypassList.Length |> shouldEqual 2
    p.IsBypassed(new Uri("http://nuget.internal")) |> shouldEqual true

[<Test>]
let ``no_proxy star alone bypasses every host``() =
    use v = proxyEnvVar "http_proxy" "http://proxy.local:8080"
    use w = proxyEnvVar "no_proxy" "*"
    let p = envProxies().TryFind "http" |> Option.get
    p.IsBypassed(new Uri("http://feed.example.com")) |> shouldEqual true

/// A system proxy that sends every url through http://system.proxy:3128
let systemProxy credentials =
    { new IWebProxy with
        member __.Credentials
            with get () = credentials
            and set _ = ()
        member __.GetProxy _ = Uri "http://system.proxy:3128"
        member __.IsBypassed _ = false }

[<Test>]
let ``proxyFor sends a no_proxy host direct, not through the system proxy``() =
    use v = proxyEnvVar "http_proxy" "http://proxy.local:8080"
    use w = proxyEnvVar "no_proxy" "nuget.internal"
    let uri = Uri "http://nuget.internal/v3/index.json"
    let proxy = proxyFor (envProxies()) (systemProxy null) uri
    proxy.IsBypassed uri |> shouldEqual true
    proxy.GetProxy uri |> shouldEqual uri

[<Test>]
let ``proxyFor sends the other hosts through the env proxy``() =
    use v = proxyEnvVar "http_proxy" "http://proxy.local:8080"
    use w = proxyEnvVar "no_proxy" "nuget.internal"
    let uri = Uri "http://feed.example.com/v3/index.json"
    let proxy = proxyFor (envProxies()) (systemProxy null) uri
    proxy.IsBypassed uri |> shouldEqual false
    proxy.GetProxy uri |> shouldEqual (Uri "http://proxy.local:8080")

[<Test>]
let ``proxyFor keeps the credentials of the system proxy``() =
    use v = proxyEnvVar "https_proxy" null
    let credentials = NetworkCredential("user", "password")
    let uri = Uri "https://feed.example.com/v3/index.json"
    let proxy = proxyFor (envProxies()) (systemProxy credentials) uri
    proxy.GetProxy uri |> shouldEqual (Uri "http://system.proxy:3128")
    proxy.Credentials |> shouldEqual (credentials :> ICredentials)

[<Test>]
let ``proxyFor gives the default credentials to a system proxy without any``() =
    use v = proxyEnvVar "https_proxy" null
    let uri = Uri "https://feed.example.com/v3/index.json"
    let proxy = proxyFor (envProxies()) (systemProxy null) uri
    proxy.GetProxy uri |> shouldEqual (Uri "http://system.proxy:3128")
    proxy.Credentials |> shouldEqual CredentialCache.DefaultCredentials

[<Test>]
let ``proxyFor goes direct when the system proxy answers no proxy``() =
    use v = proxyEnvVar "https_proxy" null
    // what the Windows system proxy answers when nothing applies: never bypassed, but no proxy
    let noProxy =
        { new IWebProxy with
            member _.Credentials
                with get () = null
                and set _ = ()
            member _.GetProxy _ = null
            member _.IsBypassed _ = false }
    let uri = Uri "https://feed.example.com/v3/index.json"
    let proxy = proxyFor (envProxies()) noProxy uri
    proxy.IsBypassed uri |> shouldEqual true
    proxy.GetProxy uri |> shouldEqual uri

[<Test>]
let ``should simplify path``() =
    let p0 = "/Users/dna/Downloads/test/aa/src/bb"
    let p1 = "/Users/dna/Downloads/test/aa/src/bb/../cc/D3D.csproj"
    let p2 = "/Users/dna/Downloads/test/aa/src/cc/D3D.csproj"
    let p3 = @"..\cc\D3D.csproj"
    System.IO.Path.IsPathRooted p0 |> shouldEqual true
    System.IO.Path.IsPathRooted p1 |> shouldEqual true
    System.IO.Path.IsPathRooted p2 |> shouldEqual true
    System.IO.Path.IsPathRooted p3 |> shouldEqual false
    System.IO.Path.GetFullPath p1 |> shouldEqual (System.IO.Path.GetFullPath p2)
    System.IO.Path.Combine(normalizePath p0,normalizePath p3) |> Path.GetFullPath |> shouldEqual (System.IO.Path.GetFullPath p2)

[<Test>]
let ``saving new XML file should produce valid XML``() =
    let tempFile = Path.GetTempFileName ()
    try
        let doc = XmlDocument ()
        doc.AppendChild(doc.CreateElement("configuration")) |> ignore
        saveNormalizedXml tempFile doc |> shouldEqual (ok ())
        let newDoc = XmlDocument ()
        use f = File.OpenRead(tempFile)
        newDoc.Load f
        if not (newDoc.FirstChild :? XmlDeclaration) then
            failwith "Generated XML should contain a declaration"
    finally
        if File.Exists(tempFile) then
            File.Delete(tempFile)


[<Test>]
let ``endsWithIgnoreCase handles shorter strings correct``() =
    let actual = Paket.Utils.String.endsWithIgnoreCase "long_long" "short"
    Assert.False(actual)
    
[<Test>]
let ``startsWithIgnoreCase handles shorter strings correct``() =
    let actual = Paket.Utils.String.startsWithIgnoreCase "long_long" "short"
    Assert.False(actual)

[<Test>]
let ``containsIgnoreCase handles shorter strings correct``() =
    let actual = Paket.Utils.String.containsIgnoreCase "long_long" "short"
    Assert.False(actual)

[<Test>]
let ``FindAllFiles should not descend into dot folders``() =
    let root = Path.Combine(Path.GetTempPath(), "paket_findallfiles_" + Guid.NewGuid().ToString("N"))
    let dotFolder = Path.Combine(root, ".localhistory")
    let normalFolder = Path.Combine(root, "src")
    try
        Directory.CreateDirectory(dotFolder) |> ignore
        Directory.CreateDirectory(normalFolder) |> ignore
        File.WriteAllText(Path.Combine(root, "root.sln"), "")
        File.WriteAllText(Path.Combine(dotFolder, "backup.sln"), "")
        File.WriteAllText(Path.Combine(normalFolder, "nested.sln"), "")

        let files = FindAllFiles(root, "*.sln") |> Array.map (fun fi -> fi.Name) |> Array.sort

        files |> shouldEqual [| "nested.sln"; "root.sln" |]
    finally
        if Directory.Exists(root) then
            Directory.Delete(root, true)
