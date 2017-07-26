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
[<Platform "Mono">]
let ``mono runtime reported on mono platform``() =
    isMonoRuntime |>
    shouldEqual true

[<Test>]
[<Platform "Net">]
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
    use v = new DisposableEnvVar("http_proxy")
    envProxies().TryFind "http" |>
    shouldEqual None

[<Test>]
let ``no env proxy without https_proxy env var``() =
    use v = new DisposableEnvVar("https_proxy")
    envProxies().TryFind "https" |>
    shouldEqual None

[<Test>]
let ``get http env proxy no port nor credentials``() =
    use v = new DisposableEnvVar("http_proxy", "http://proxy.local")
    use w = new DisposableEnvVar("no_proxy")
    let pOpt = envProxies().TryFind "http"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 0
    p.Credentials |> shouldEqual null

[<Test>]
let ``get https env proxy no port nor credentials``() =
    use v = new DisposableEnvVar("https_proxy", "https://proxy.local")
    use w = new DisposableEnvVar("no_proxy")
    let pOpt = envProxies().TryFind "https"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local:443"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 0
    p.Credentials |> shouldEqual null

[<Test>]
let ``get http env proxy with port no credentials``() =
    use v = new DisposableEnvVar("http_proxy", "http://proxy.local:8080")
    use w = new DisposableEnvVar("no_proxy")
    let pOpt = envProxies().TryFind "http"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local:8080"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 0
    p.Credentials |> shouldEqual null

[<Test>]
let ``get https env proxy with port no credentials``() =
    use v = new DisposableEnvVar("https_proxy", "https://proxy.local:8080")
    use w = new DisposableEnvVar("no_proxy")
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
    use v = new DisposableEnvVar("http_proxy", sprintf "http://user:%s@proxy.local:8080" (Uri.EscapeDataString password))
    use w = new DisposableEnvVar("no_proxy")
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
    use v = new DisposableEnvVar("https_proxy", sprintf "https://user:%s@proxy.local:8080" (Uri.EscapeDataString password))
    use w = new DisposableEnvVar("no_proxy")
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
    use v = new DisposableEnvVar("http_proxy", "http://proxy.local:8080")
    use w = new DisposableEnvVar("no_proxy", ".local,localhost")
    let pOpt = envProxies().TryFind "http"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local:8080"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 2
    p.BypassList.[0] |> shouldEqual "\\.local"
    p.BypassList.[1] |> shouldEqual "localhost"
    p.Credentials |> shouldEqual null

[<Test>]
let ``get http env proxy with bypass list containing wildcards``() =
    use v = new DisposableEnvVar("http_proxy", "http://proxy.local:8080")
    use w = new DisposableEnvVar("no_proxy", ".local,localhost,*.asdf.com")
    let pOpt = envProxies().TryFind "http"
    Option.isSome pOpt |> shouldEqual true
    let p = Option.get pOpt
    p.Address |> shouldEqual (new Uri("http://proxy.local:8080"))
    p.BypassProxyOnLocal |> shouldEqual true
    p.BypassList.Length |> shouldEqual 3
    p.BypassList.[0] |> shouldEqual "\\.local"
    p.BypassList.[1] |> shouldEqual "localhost"
    p.BypassList.[2] |> shouldEqual "\\.*\\.asdf\\.com"
    p.Credentials |> shouldEqual null

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
    
let net40 = SinglePlatform (DotNetFramework FrameworkVersion.V4)
    
[<Test>]
let ``superset behaves like I expect it to 1``() =
    // see \src\Paket.Core\PaketConfigFiles\ProjectFile.fs:807
    set [ net40 ]
    |> Set.isSuperset KnownTargetProfiles.AllProfiles
    |> Assert.True

[<Test>]
let ``superset behaves like I expect it to 2``() =
    // see \src\Paket.Core\PaketConfigFiles\ProjectFile.fs:807
    KnownTargetProfiles.AllProfiles
    |> Set.isSuperset (set [ net40 ])
    |> Assert.False
    
[<Test>]
let ``superset behaves like I expect it to 3``() =
    // see \src\Paket.Core\PaketConfigFiles\ProjectFile.fs:807
    set [ net40 ]
    |> Set.isSuperset (set [ net40 ])
    |> Assert.True
    
[<Test>]
let ``superset behaves like I expect it to 4``() =
    // see \src\Paket.Core\PaketConfigFiles\ProjectFile.fs:807
    KnownTargetProfiles.AllProfiles
    |> Set.isSuperset KnownTargetProfiles.AllProfiles
    |> Assert.True