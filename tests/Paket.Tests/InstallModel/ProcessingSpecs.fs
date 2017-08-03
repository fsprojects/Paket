module Paket.InstallModel.ProcessingSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Domain
open Paket.Requirements
open System.IO
open Pri.LongPath

let emptymodel = InstallModel.EmptyModel(PackageName "Unknown",SemVer.Parse "0.1")

let fromLegacyList (prefix:string) l =
    l
    |> List.map (fun (i:string) ->
        if i.StartsWith prefix then
            { Paket.NuGet.UnparsedPackageFile.FullPath = i; Paket.NuGet.UnparsedPackageFile.PathWithinPackage = i.Substring(prefix.Length).Replace("\\", "/") }
        else failwithf "Expected '%s' to start with '%s'" i prefix)

[<Test>]
let ``Library.ofFile should not crash on files without extension``() =
    let frameworkDepFile = {
        Path = { Name = ""; Platforms= [] }
        File =  { FullPath = Path.Combine(Path.GetTempPath(), "filewithoutext"); PathWithinPackage = "lib/net40" }
        Runtime = None
    }
    let lib = Library.ofFile frameworkDepFile
    lib.Name |> shouldEqual "filewithoutext"

[<Test>]
let ``should create empty model with net40, net45 ...``() = 
    let model = emptymodel.AddReferences ([ @"..\Rx-Main\lib\net40\Rx.dll"; @"..\Rx-Main\lib\net45\Rx.dll" ] |> fromLegacyList @"..\Rx-Main\")

    let targets =
        model.CompileLibFolders
        |> List.map (fun folder -> folder.Targets |> Set.toList)
        |> List.concat

    targets |> shouldContain (SinglePlatform (DotNetFramework FrameworkVersion.V4))
    targets |> shouldContain (SinglePlatform (DotNetFramework FrameworkVersion.V4_5))

[<Test>]
let ``should understand net40 and net45``() = 
    let model = emptymodel.AddReferences ([ @"..\Rx-Main\lib\net40\Rx.dll"; @"..\Rx-Main\lib\net45\Rx.dll" ] |> fromLegacyList @"..\Rx-Main\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain (@"..\Rx-Main\lib\net40\Rx.dll")
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) 
        |> Seq.map (fun f -> f.Path)|> shouldContain @"..\Rx-Main\lib\net45\Rx.dll"

[<Test>]
let ``should understand lib in lib.dll``() = 
    let model = emptymodel.AddReferences ([ @"..\FunScript.TypeScript\lib\net40\FunScript.TypeScript.Binding.lib.dll" ] |> fromLegacyList @"..\FunScript.TypeScript\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\FunScript.TypeScript\lib\net40\FunScript.TypeScript.Binding.lib.dll"

[<Test>]
let ``should understand libuv in runtimes``() = 
    let model = emptymodel.AddReferences ([ @"..\Microsoft.AspNetCore.Server.Kestrel\runtimes\win7-x64\native\libuv.dll" ] |> fromLegacyList @"..\Microsoft.AspNetCore.Server.Kestrel\")

    model.GetRuntimeLibraries RuntimeGraph.Empty (Rid.Of "win7-x64") (SinglePlatform (DotNetFramework FrameworkVersion.V4))
       |> Seq.map (fun (r:RuntimeLibrary) -> r.Library.Path) |> shouldContain @"..\Microsoft.AspNetCore.Server.Kestrel\runtimes\win7-x64\native\libuv.dll"

[<Test>]
let ``should understand reference folder``() =
    let model =
      emptymodel.AddReferences
       ([ @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
          @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.6\System.Security.Cryptography.Algorithms.dll"
          @"..\System.Security.Cryptography.Algorithms\lib\net35\System.Security.Cryptography.Algorithms.dll" ]
          |> fromLegacyList @"..\System.Security.Cryptography.Algorithms\")

    let refs =
        model.GetLegacyReferences(SinglePlatform (DotNetStandard DotNetStandardVersion.V1_6))
        |> Seq.map (fun f -> f.Path)
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\lib\net35\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.6\System.Security.Cryptography.Algorithms.dll"

    let refs =
        model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path)
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldContain @"..\System.Security.Cryptography.Algorithms\lib\net35\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.6\System.Security.Cryptography.Algorithms.dll"

    let refs =
        model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path)
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldContain @"..\System.Security.Cryptography.Algorithms\lib\net35\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.6\System.Security.Cryptography.Algorithms.dll"

    let refs =
        model.GetCompileReferences(SinglePlatform (DotNetStandard DotNetStandardVersion.V1_6))
        |> Seq.map (fun f -> f.Path)
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\lib\net35\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.6\System.Security.Cryptography.Algorithms.dll"

    let refs =
        model.GetCompileReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path)
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldContain @"..\System.Security.Cryptography.Algorithms\lib\net35\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.6\System.Security.Cryptography.Algorithms.dll"

    let refs =
        model.GetCompileReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path)
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldContain @"..\System.Security.Cryptography.Algorithms\lib\net35\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.6\System.Security.Cryptography.Algorithms.dll"

    let model =
      emptymodel.AddReferences
       ([ @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
          @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.5\System.Security.Cryptography.Algorithms.dll"
          @"..\System.Security.Cryptography.Algorithms\lib\netstandard1.5\System.Security.Cryptography.Algorithms.dll" ] |> fromLegacyList @"..\System.Security.Cryptography.Algorithms\")

    let refs =
        model.GetLegacyReferences(SinglePlatform (DotNetStandard DotNetStandardVersion.V1_6))
        |> Seq.map (fun f -> f.Path)
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldContain @"..\System.Security.Cryptography.Algorithms\lib\netstandard1.5\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.5\System.Security.Cryptography.Algorithms.dll"

    let refs =
        model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_6_3))
        |> Seq.map (fun f -> f.Path)
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldContain @"..\System.Security.Cryptography.Algorithms\lib\netstandard1.5\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.5\System.Security.Cryptography.Algorithms.dll"

    let refs =
        model.GetCompileReferences(SinglePlatform (DotNetStandard DotNetStandardVersion.V1_6))
        |> Seq.map (fun f -> f.Path)
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\lib\netstandard1.5\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.5\System.Security.Cryptography.Algorithms.dll"

    let refs =
        model.GetCompileReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_6_3))
        |> Seq.map (fun f -> f.Path)
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\lib\netstandard1.5\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.5\System.Security.Cryptography.Algorithms.dll"

    let refs =
        model.GetRuntimeAssemblies RuntimeGraph.Empty (Rid.Of "win") (SinglePlatform (DotNetStandard DotNetStandardVersion.V1_6))
        |> Seq.map (fun f -> f.Library.Path)
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldContain @"..\System.Security.Cryptography.Algorithms\lib\netstandard1.5\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.5\System.Security.Cryptography.Algorithms.dll"

    let refs =
        model.GetRuntimeAssemblies RuntimeGraph.Empty (Rid.Of "win") (SinglePlatform (DotNetFramework FrameworkVersion.V4_6_3))
        |> Seq.map (fun f -> f.Library.Path)
    refs |> shouldContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\lib\netstandard1.5\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.5\System.Security.Cryptography.Algorithms.dll"

    let refs =
        model.GetRuntimeAssemblies RuntimeGraph.Empty (Rid.Of "unix") (SinglePlatform (DotNetFramework FrameworkVersion.V4_6_3))
        |> Seq.map (fun f -> f.Library.Path)
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldContain @"..\System.Security.Cryptography.Algorithms\lib\netstandard1.5\System.Security.Cryptography.Algorithms.dll"
    refs |> shouldNotContain @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.5\System.Security.Cryptography.Algorithms.dll"

[<Test>]
let ``should understand aot in runtimes``() = 
    let model = emptymodel.AddReferences ([ @"..\packages\System.Diagnostics.Contracts\runtimes\aot\lib\netstandard13\System.Diagnostics.Contracts.dll" ] |> fromLegacyList @"..\packages\System.Diagnostics.Contracts\")

    let refs =
        model.GetRuntimeAssemblies RuntimeGraph.Empty (Rid.Of "aot") (SinglePlatform (DotNetStandard DotNetStandardVersion.V1_6))
        |> Seq.map (fun f -> f.Library.Path)
    refs |> shouldContain @"..\packages\System.Diagnostics.Contracts\runtimes\aot\lib\netstandard13\System.Diagnostics.Contracts.dll"


[<Test>]
let ``should understand mylib in mylib.dll``() = 
    let model = emptymodel.AddReferences ([ @"c:/users/username/workspace/myproject/packages/mylib.mylib/lib/net45/mylib.mylib.dll" ] |> fromLegacyList @"c:/users/username/workspace/myproject/packages/mylib.mylib/")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"c:/users/username/workspace/myproject/packages/mylib.mylib/lib/net45/mylib.mylib.dll"

[<Test>]
let ``should add net35 if we have net20 and net40``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\Rx.dll" ] |> fromLegacyList @"..\Rx-Main\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5_1))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"

[<Test>]
let ``should put _._ files into right buckets``() = 
    let model = emptymodel.AddReferences ([ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ] |> fromLegacyList @"..\Rx-Main\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net40\_._"

[<Test>]
let ``should inherit _._ files to higher frameworks``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ] |> fromLegacyList @"..\Rx-Main\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net40\_._"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net40\_._"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5_1))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net40\_._"


[<Test>]
let ``should skip buckets which contain placeholder while adjusting upper versions``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\_._" ] |> fromLegacyList @"..\Rx-Main\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"

[<Test>]
let ``should filter _._ when processing blacklist``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ] |> fromLegacyList @"..\Rx-Main\")
            .FilterBlackList()

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Rx-Main\lib\net20\_._"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Rx-Main\lib\net40\_._"

[<Test>]
let ``should install single client profile lib for everything``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Castle.Core\lib\net40-client\Castle.Core.dll" ] |> fromLegacyList @"..\Castle.Core\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"


[<Test>]
let ``should install net40 for client profile``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Newtonsoft.Json\lib\net35\Newtonsoft.Json.dll"
              @"..\Newtonsoft.Json\lib\net40\Newtonsoft.Json.dll"] |> fromLegacyList @"..\Newtonsoft.Json\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Newtonsoft.Json\lib\net35\Newtonsoft.Json.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Newtonsoft.Json\lib\net40\Newtonsoft.Json.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Newtonsoft.Json\lib\net40\Newtonsoft.Json.dll" 

[<Test>]
let ``should install not use net40-full for client profile``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Newtonsoft.Json\lib\net35\Newtonsoft.Json.dll"
              @"..\Newtonsoft.Json\lib\net40-full\Newtonsoft.Json.dll"] |> fromLegacyList @"..\Newtonsoft.Json\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Newtonsoft.Json\lib\net35\Newtonsoft.Json.dll"     
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Newtonsoft.Json\lib\net40-full\Newtonsoft.Json.dll" 

[<Test>]
let ``should handle lib install of Microsoft.Net.Http for .NET 4.5``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll"
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll"
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll"

              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll"
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" ] |> fromLegacyList @"..\Microsoft.Net.Http\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" 

[<Test>]
let ``should add portable lib``() = 
    let model =
        emptymodel.AddReferences([ @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" ] |> fromLegacyList @"..\Jint\")

    model.GetLegacyReferences(KnownTargetProfiles.FindPortableProfile "Profile147")
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

[<Test>]
let ``should handle lib install of Jint for NET >= 40 and SL >= 50``() = 
    let model =
        emptymodel.AddReferences([ @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" ] |> fromLegacyList @"..\Jint\")
   
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll"

    model.GetLegacyReferences(SinglePlatform (Silverlight SilverlightVersion.V5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll"
    
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 
    model.GetLegacyReferences(KnownTargetProfiles.FindPortableProfile "Profile147")
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

[<Test>]
let ``should handle lib install of Microsoft.BCL for NET >= 40``() = 
    let model =
        emptymodel.AddReferences(
            [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll"
              @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll"
              @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll"

              @"..\Microsoft.Bcl\lib\net45\_._" ] |> fromLegacyList @"..\Microsoft.Bcl\")
              .FilterBlackList()

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain  @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldBeEmpty
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5_1)) |> shouldBeEmpty


[<Test>]
let ``should skip lib install of Microsoft.BCL for monotouch and monoandroid``() =
    let model =
        emptymodel.AddReferences(
            [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll"
              @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll"
              @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll"
              @"..\Microsoft.Bcl\lib\monoandroid\_._"
              @"..\Microsoft.Bcl\lib\monotouch\_._"
              @"..\Microsoft.Bcl\lib\net45\_._" ] |> fromLegacyList @"..\Microsoft.Bcl\")
            .FilterBlackList()

    model.GetLegacyReferences(SinglePlatform (MonoAndroid MonoAndroidVersion.V1)) |> shouldBeEmpty
    model.GetLegacyReferences(SinglePlatform MonoTouch) |> shouldBeEmpty

[<Test>]
let ``should not use portable-net40 if we have net40``() = 
    let model =
        emptymodel.AddReferences(
            [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll"
              @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll"
              @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll"

              @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll"
              @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll"
              @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll" ] |> fromLegacyList @"..\Microsoft.Bcl\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    let profile41 = KnownTargetProfiles.FindPortableProfile "Profile41"
    model.GetLegacyReferences(profile41)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll" 
    model.GetLegacyReferences(profile41)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll" 
    model.GetLegacyReferences(profile41)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll" 

[<Test>]
let ``should handle lib install of DotNetZip 1.9.3``() = 
    let model = emptymodel.AddReferences([ @"..\DotNetZip\lib\net20\Ionic.Zip.dll" ] |> fromLegacyList @"..\DotNetZip\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"

[<Test>]
let ``should handle lib install of NUnit 2.6 for windows 8``() = 
    let model = emptymodel.AddReferences([ @"..\NUnit\lib\nunit.framework.dll" ] |> fromLegacyList @"..\NUnit\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\NUnit\lib\nunit.framework.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\NUnit\lib\nunit.framework.dll"
    model.GetLegacyReferences(SinglePlatform (Windows WindowsVersion.V8))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\NUnit\lib\nunit.framework.dll"


[<Test>]
let ``should handle lib install of Microsoft.Net.Http 2.2.28``() = 
    let model =
        emptymodel.AddReferences(
            [ @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Extensions.dll"
              @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Primitives.dll"

              @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Extensions.dll"
              @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Primitives.dll"

              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll"
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll"
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll"

              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll"
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll"

              @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.dll"
              @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Extensions.dll"
              @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Primitives.dll"

              @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Extensions.dll"
              @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Primitives.dll"

              @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Extensions.dll"
              @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Primitives.dll"

              @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Extensions.dll"
              @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Primitives.dll" ] |> fromLegacyList @"..\Microsoft.Net.Http\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 

    model.GetLegacyReferences(SinglePlatform (MonoAndroid MonoAndroidVersion.V1))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Extensions.dll" 
    model.GetLegacyReferences(SinglePlatform (MonoAndroid MonoAndroidVersion.V1))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Primitives.dll" 

    model.GetLegacyReferences(SinglePlatform MonoTouch)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Extensions.dll" 
    model.GetLegacyReferences(SinglePlatform MonoTouch)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Primitives.dll" 

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" 

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll"  
    
    let profile88 = KnownTargetProfiles.FindPortableProfile "Profile88"
    model.GetLegacyReferences(profile88)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.dll"
    model.GetLegacyReferences(profile88)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Extensions.dll" 
    model.GetLegacyReferences(profile88)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Primitives.dll" 

    let profile7 = KnownTargetProfiles.FindPortableProfile "Profile7"
    model.GetLegacyReferences(profile7)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Extensions.dll" 
    model.GetLegacyReferences(profile7)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Primitives.dll" 

    model.GetLegacyReferences(SinglePlatform (Windows WindowsVersion.V8))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Extensions.dll" 
    model.GetLegacyReferences(SinglePlatform (Windows WindowsVersion.V8))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Primitives.dll" 

    model.GetLegacyReferences(SinglePlatform (WindowsPhoneApp WindowsPhoneAppVersion.V8_1))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Extensions.dll" 
    model.GetLegacyReferences(SinglePlatform (WindowsPhoneApp WindowsPhoneAppVersion.V8_1))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Primitives.dll" 


[<Test>]
let ``should handle lib install of MicrosoftBcl``() = 
    let model =
        emptymodel.AddReferences(
            ([ @"..\Microsoft.Net.Http\lib\monoandroid\_._"

               @"..\Microsoft.Net.Http\lib\monotouch\_._"
               @"..\Microsoft.Net.Http\lib\net45\_._"
             ] |> fromLegacyList @"..\Microsoft.Net.Http\") @
            ([
              @"..\Microsoft.Bcl\lib\net40\System.IO.dll"
              @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll"
              @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll"


              @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll"
              @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll"
              @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll"

              @"..\Microsoft.Bcl\lib\sl4\System.IO.dll"
              @"..\Microsoft.Bcl\lib\sl4\System.Runtime.dll"
              @"..\Microsoft.Bcl\lib\sl4\System.Threading.Tasks.dll"

              @"..\Microsoft.Bcl\lib\sl4-windowsphone71\System.IO.dll"
              @"..\Microsoft.Bcl\lib\sl4-windowsphone71\System.Runtime.dll"
              @"..\Microsoft.Bcl\lib\sl4-windowsphone71\System.Threading.Tasks.dll"

              @"..\Microsoft.Bcl\lib\sl5\System.IO.dll"
              @"..\Microsoft.Bcl\lib\sl5\System.Runtime.dll"
              @"..\Microsoft.Bcl\lib\sl5\System.Threading.Tasks.dll"

              @"..\Microsoft.Bcl\lib\win8\_._"
              @"..\Microsoft.Bcl\lib\wp8\_._"
              @"..\Microsoft.Bcl\lib\wpa81\_._"
              @"..\Microsoft.Bcl\lib\portable-net451+win81\_._"
              @"..\Microsoft.Bcl\lib\portable-net451+win81+wpa81\_._"]
             |> fromLegacyList @"..\Microsoft.Bcl\")).FilterBlackList()

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldBeEmpty
    model.GetLegacyReferences(SinglePlatform (MonoAndroid MonoAndroidVersion.V1)) |> shouldBeEmpty
    model.GetLegacyReferences(SinglePlatform MonoTouch) |> shouldBeEmpty
    model.GetLegacyReferences(SinglePlatform (Windows WindowsVersion.V8)) |> shouldBeEmpty
    model.GetLegacyReferences(SinglePlatform (WindowsPhone WindowsPhoneVersion.V8)) |> shouldBeEmpty
    model.GetLegacyReferences(SinglePlatform (WindowsPhoneApp WindowsPhoneAppVersion.V8_1)) |> shouldBeEmpty
    model.GetLegacyReferences(KnownTargetProfiles.FindPortableProfile "Profile44") |> shouldBeEmpty
    model.GetLegacyReferences(KnownTargetProfiles.FindPortableProfile "Profile151") |> shouldBeEmpty
    
    let profile41 = KnownTargetProfiles.FindPortableProfile "Profile41"
    model.GetLegacyReferences(profile41)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll"
    model.GetLegacyReferences(profile41)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll"
    model.GetLegacyReferences(profile41)
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll" 

    model.GetLegacyReferences(SinglePlatform (Silverlight SilverlightVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\sl4\System.IO.dll"
    model.GetLegacyReferences(SinglePlatform (Silverlight SilverlightVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\sl4\System.Runtime.dll"
    model.GetLegacyReferences(SinglePlatform (Silverlight SilverlightVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\sl4\System.Threading.Tasks.dll" 
    
    model.GetLegacyReferences(SinglePlatform (WindowsPhone WindowsPhoneVersion.V7_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\sl4-windowsphone71\System.IO.dll"
    model.GetLegacyReferences(SinglePlatform (WindowsPhone WindowsPhoneVersion.V7_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\sl4-windowsphone71\System.Runtime.dll"
    model.GetLegacyReferences(SinglePlatform (WindowsPhone WindowsPhoneVersion.V7_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\sl4-windowsphone71\System.Threading.Tasks.dll" 

    model.GetLegacyReferences(SinglePlatform (Silverlight SilverlightVersion.V5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\sl5\System.IO.dll"
    model.GetLegacyReferences(SinglePlatform (Silverlight SilverlightVersion.V5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\sl5\System.Runtime.dll"
    model.GetLegacyReferences(SinglePlatform (Silverlight SilverlightVersion.V5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl\lib\sl5\System.Threading.Tasks.dll" 


[<Test>]
let ``should handle lib install of Fantomas 1.5``() = 
    let model =
        emptymodel.AddReferences(
            [ @"..\Fantomas\lib\FantomasLib.dll"
              @"..\Fantomas\lib\FSharp.Core.dll"
              @"..\Fantomas\lib\Fantomas.exe" ] |> fromLegacyList @"..\Fantomas\")

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

[<Test>]
let ``should handle lib install of Fantomas 1.5.0 with explicit references``() = 
    let model =
        emptymodel.AddLibReferences(
            [ @"..\Fantomas\lib\FantomasLib.dll"
              @"..\Fantomas\lib\FSharp.Core.dll"
              @"..\Fantomas\lib\Fantomas.exe" ] |> fromLegacyList @"..\Fantomas\",
            NuspecReferences.Explicit ["FantomasLib.dll"])

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 


[<Test>]
let ``should only handle dll and exe files``() = 
    let model =
        emptymodel.AddLibReferences(
            [ @"..\Fantomas\lib\FantomasLib.dll"
              @"..\Fantomas\lib\FantomasLib.xml"
              @"..\Fantomas\lib\FSharp.Core.dll"
              @"..\Fantomas\lib\Fantomas.exe" ] |> fromLegacyList @"..\Fantomas\",
            NuspecReferences.All)
            .FilterBlackList()

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Fantomas\lib\Fantomas.exe" 
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldNotContain @"..\Fantomas\lib\FantomasLib.xml" 

[<Test>]
let ``should use portable net40 in net45 when don't have other files``() = 
    let model =
        emptymodel.AddLibReferences(
            [ @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll" ] |> fromLegacyList @"..\Google.Apis.Core\",
            NuspecReferences.All)

    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5_1))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5_2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"
    model.GetLegacyReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5_3))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"

[<Test>]
let ``should not install tools``() = 
    let model =
        emptymodel.AddReferences(
            [ @"..\FAKE\tools\FAKE.exe"
              @"..\FAKE\tools\FakeLib.dll"
              @"..\FAKE\tools\Fake.SQL.dll" ] |> fromLegacyList @"..\FAKE\")

    model.CompileLibFolders
    |> Seq.forall (fun folder -> folder.FolderContents.Libraries.IsEmpty && folder.FolderContents.FrameworkReferences.IsEmpty)
    |> shouldEqual true

[<Test>]
let ``should handle props files``() = 
    let model =
        InstallModel.EmptyModel(PackageName "xunit.runner.visualstudio",SemVer.Parse "0.1").AddTargetsFiles(
            [ @"..\xunit.runner.visualstudio\build\net20\xunit.runner.visualstudio.props"
              @"..\xunit.runner.visualstudio\build\portable-net45+aspnetcore50+win+wpa81+wp80+monotouch+monoandroid\xunit.runner.visualstudio.props" ] |> fromLegacyList @"..\xunit.runner.visualstudio\")
            .FilterBlackList()

    model.GetTargetsFiles(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\xunit.runner.visualstudio\build\net20\xunit.runner.visualstudio.props"

[<Test>]
let ``should handle Targets files``() = 
    let model =
        InstallModel.EmptyModel(PackageName "StyleCop.MSBuild",SemVer.Parse "0.1").AddTargetsFiles(
            [ @"..\StyleCop.MSBuild\build\StyleCop.MSBuild.Targets" ] |> fromLegacyList @"..\StyleCop.MSBuild\")
            .FilterBlackList()

    model.GetTargetsFiles(SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\StyleCop.MSBuild\build\StyleCop.MSBuild.Targets"

[<Test>]
let ``should filter .NET 4.0 dlls for System.Net.Http 2.2.8``() = 
    let expected =
        [ @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
          @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll"
          @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll"
          @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" ]
        |> Seq.ofList

    let model =
        InstallModel.CreateFromLibs
            (PackageName "System.Net.Http", SemVer.Parse "2.2.8",
             FrameworkRestriction.NoRestriction,
             [ @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
               @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll"
               @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.dll"
               @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Primitives.dll" ] |> fromLegacyList @"..\Microsoft.Net.Http\", [], [], Nuspec.All)

    model.GetLegacyReferences(SinglePlatform(DotNetFramework(FrameworkVersion.V4)))
    |> Seq.map (fun f -> f.Path)
    |> shouldEqual expected

[<Test>]
let ``should filter .NET 4.5 dlls for System.Net.Http 2.2.8``() = 
    let expected = 
        [ @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll" 
          @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll"]
        |> Seq.ofList

    let model =
        InstallModel.CreateFromLibs
            (PackageName "System.Net.Http", SemVer.Parse "2.2.8",
             FrameworkRestriction.NoRestriction,
             [ @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
               @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll"
               @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.dll"
               @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Primitives.dll"
               @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Extensions.dll"
               @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Primitives.dll" ] |> fromLegacyList @"..\Microsoft.Net.Http\", [], [], Nuspec.All)

    model.GetLegacyReferences(SinglePlatform(DotNetFramework(FrameworkVersion.V4_5)))
    |> Seq.map (fun f -> f.Path)
    |> shouldEqual expected

    model.GetLegacyReferences(SinglePlatform(DotNetFramework(FrameworkVersion.V4_5_2)))
    |> Seq.map (fun f -> f.Path)
    |> shouldEqual expected

[<Test>]
let ``should filter properly when portables are available``() = 
    let model =
        InstallModel.CreateFromLibs
            (PackageName "Newtonsoft.Json", SemVer.Parse "8.0.3",
             FrameworkRestriction.NoRestriction,
             [ @"..\Newtonsoft.Json\lib\net20\Newtonsoft.Json.dll"
               @"..\Newtonsoft.Json\lib\net35\Newtonsoft.Json.dll"
               @"..\Newtonsoft.Json\lib\net40\Newtonsoft.Json.dll"
               @"..\Newtonsoft.Json\lib\net45\Newtonsoft.Json.dll"
               @"..\Newtonsoft.Json\lib\portable-net40+sl5+wp80+win8+wpa81\Newtonsoft.Json.dll"
               @"..\Newtonsoft.Json\lib\portable-net45+wp80+win8+wpa81+dnxcore50\Newtonsoft.Json.dll" ] |> fromLegacyList @"..\Newtonsoft.Json\", [], [], Nuspec.All)

    let filteredModel =
      model.ApplyFrameworkRestrictions (FrameworkRestriction.Exactly (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_5))

    filteredModel.GetLegacyReferences(SinglePlatform(DotNetFramework(FrameworkVersion.V4_5)))
    |> Seq.map (fun f -> f.Path)
    |> Seq.toList
    |> shouldEqual [ @"..\Newtonsoft.Json\lib\net45\Newtonsoft.Json.dll" ]

    filteredModel.GetLegacyReferences(SinglePlatform(DotNetFramework(FrameworkVersion.V4)))
    |> Seq.toList
    |> shouldEqual [ ]

[<Test>]
let ``should keep net20 if nothing better is available``() = 
    let model =
        InstallModel.CreateFromLibs
            (PackageName "EPPlus", SemVer.Parse "4.0.5",
             FrameworkRestriction.NoRestriction,
             [ @"..\EPPlus\lib\net20\EPPlus.dll" ] |> fromLegacyList @"..\EPPlus\", [], [], Nuspec.All)

    let filteredModel =
      model.ApplyFrameworkRestrictions (FrameworkRestriction.Exactly (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_6_1))

    filteredModel.GetLegacyReferences(SinglePlatform(DotNetFramework(FrameworkVersion.V4_6_1)))
    |> Seq.map (fun f -> f.Path)
    |> Seq.toList
    |> shouldEqual [ @"..\EPPlus\lib\net20\EPPlus.dll" ]

    filteredModel.GetLegacyReferences(SinglePlatform(DotNetFramework(FrameworkVersion.V4)))
    |> Seq.map (fun f -> f.Path)
    |> Seq.toList
    |> shouldEqual [ ]

[<Test>]
let ``prefer net20 over empty folder``() =
    let model =
        InstallModel.CreateFromLibs
            (PackageName "EPPlus", SemVer.Parse "4.0.5",
             FrameworkRestriction.NoRestriction,
             [ @"..\EPPlus\lib\readme.txt"
               @"..\EPPlus\lib\net20\EPPlus.dll" ] |> fromLegacyList @"..\EPPlus\", [], [], Nuspec.All)

    let filteredModel =
      model.ApplyFrameworkRestrictions (FrameworkRestriction.Exactly (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_6_1))

    filteredModel.GetLegacyReferences(SinglePlatform(DotNetFramework(FrameworkVersion.V4_6_1)))
    |> Seq.map (fun f -> f.Path)
    |> Seq.toList
    |> shouldEqual [ @"..\EPPlus\lib\net20\EPPlus.dll" ]

    filteredModel.GetLegacyReferences(SinglePlatform(DotNetFramework(FrameworkVersion.V4)))
    |> Seq.toList
    |> shouldEqual [ ]

[<Test>]
let ``should understand xamarinios``() =
    let model = emptymodel.ApplyFrameworkRestrictions (FrameworkRestriction.Exactly (XamariniOS))
    let model = model.AddReferences ([ @"..\FSharp.Core\lib\portable-net45+monoandroid10+monotouch10+xamarinios10\FSharp.Core.dll" ] |> fromLegacyList @"..\FSharp.Core\")

    model.GetLegacyReferences(SinglePlatform (XamariniOS))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\FSharp.Core\lib\portable-net45+monoandroid10+monotouch10+xamarinios10\FSharp.Core.dll"

[<Test>]
let ``should prefer net40-full over net40-client``() = 
    let model =
        emptymodel.AddReferences
            ([@"..\packages\MyPackage\lib\net40-client\MyPackage.dll"
              @"..\packages\MyPackage\lib\net40-full\MyPackage.dll"] 
             |> fromLegacyList @"..\packages\MyPackage\")

    let refs =
        model.GetLegacyReferences (SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.PathWithinPackage)
    refs |> shouldContain @"lib/net40-full/MyPackage.dll"
    refs |> shouldNotContain @"lib/net40-client/MyPackage.dll"
    
[<Test>]
let ``should prefer net40-full over net40-client (reversed)``() = 
    // Should not depend on any order.
    let model =
        emptymodel.AddReferences
            ([@"..\packages\MyPackage\lib\net40-full\MyPackage.dll"
              @"..\packages\MyPackage\lib\net40-client\MyPackage.dll"] 
             |> fromLegacyList @"..\packages\MyPackage\")

    let refs =
        model.GetLegacyReferences (SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
        |> Seq.map (fun f -> f.PathWithinPackage)
    refs |> shouldContain @"lib/net40-full/MyPackage.dll"
    refs |> shouldNotContain @"lib/net40-client/MyPackage.dll"
