module Paket.NuspecSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can detect explicit references``() = 
    Nuspec.Load("TestFiles/FSharp.Data.nuspec").References
    |> shouldEqual (NuspecReferences.Explicit ["FSharp.Data.dll"])

[<Test>]
let ``can detect all references``() = 
    Nuspec.Load("TestFiles/Octokit.nuspec").References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``if nuspec is not found we assume all references``() = 
    Nuspec.Load("TestFiles/blablub.nuspec").References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``can detect explicit references for Fantomas``() = 
    Nuspec.Load("TestFiles/Fantomas.nuspec").References
    |> shouldEqual (NuspecReferences.Explicit ["FantomasLib.dll"])

[<Test>]
let ``can detect no framework assemblies for Fantomas``() = 
    Nuspec.Load("TestFiles/Fantomas.nuspec").FrameworkAssemblyReferences
    |> shouldEqual []

[<Test>]
let ``if nuspec is not found we assume no framework references``() = 
    Nuspec.Load("TestFiles/blablub.nuspec").FrameworkAssemblyReferences
    |> shouldEqual []

[<Test>]
let ``can detect framework assemblies for Microsoft.Net.Http``() = 
    Nuspec.Load("TestFiles/Microsoft.Net.Http.nuspec").FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName="System.Net.Http"; TargetFramework=".NETFramework4.5" }
         { AssemblyName="System.Net.Http.WebRequest"; TargetFramework=".NETFramework4.5" }
         { AssemblyName="System.Net.Http"; TargetFramework="MonoTouch0.0" }
         { AssemblyName="System.Net.Http"; TargetFramework="MonoAndroid0.0" } ]