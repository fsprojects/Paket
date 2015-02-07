module Paket.InstallModel.ProcessingSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Domain

let emptymodel = InstallModel.EmptyModel(PackageName "Unknown",SemVer.Parse "0.1")

[<Test>]
let ``should create empty model with net40, net45 ...``() = 
    let model = emptymodel.AddReferences [ @"..\Rx-Main\lib\net40\Rx.dll"; @"..\Rx-Main\lib\net45\Rx.dll" ] 

    let targets =
        model.ReferenceFileFolders
        |> List.map (fun folder -> folder.Targets)
        |> List.concat

    targets |> shouldContain (SinglePlatform (DotNetFramework FrameworkVersion.V4))
    targets |> shouldContain (SinglePlatform (DotNetFramework FrameworkVersion.V4_5))

[<Test>]
let ``should understand net40 and net45``() = 
    let model = emptymodel.AddReferences [ @"..\Rx-Main\lib\net40\Rx.dll"; @"..\Rx-Main\lib\net45\Rx.dll" ] 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_Client)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\Rx-Main\lib\net45\Rx.dll"

[<Test>]
let ``should understand lib in lib.dll``() = 
    let model = emptymodel.AddReferences [ @"..\FunScript.TypeScript\lib\net40\FunScript.TypeScript.Binding.lib.dll"; ] 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_Client)) |> shouldContain @"..\FunScript.TypeScript\lib\net40\FunScript.TypeScript.Binding.lib.dll"

[<Test>]
let ``should add net35 if we have net20 and net40``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\Rx.dll" ])

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5_1)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"

[<Test>]
let ``should put _._ files into right buckets``() = 
    let model = emptymodel.AddReferences [ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ] 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_Client)) |> shouldContain @"..\Rx-Main\lib\net40\_._"

[<Test>]
let ``should inherit _._ files to higher frameworks``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ])

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Rx-Main\lib\net40\_._"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\Rx-Main\lib\net40\_._"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5_1)) |> shouldContain @"..\Rx-Main\lib\net40\_._"


[<Test>]
let ``should skip buckets which contain placeholder while adjusting upper versions``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\_._"; ])

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"

[<Test>]
let ``should filter _._ when processing blacklist``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ])
            .FilterBlackList()

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldNotContain @"..\Rx-Main\lib\net20\_._"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldNotContain @"..\Rx-Main\lib\net40\_._"

[<Test>]
let ``should install single client profile lib for everything``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Castle.Core\lib\net40-client\Castle.Core.dll" ])

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_Client)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"


[<Test>]
let ``should install net40 for client profile``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Newtonsoft.Json\lib\net35\Newtonsoft.Json.dll" 
              @"..\Newtonsoft.Json\lib\net40\Newtonsoft.Json.dll"])

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldContain @"..\Newtonsoft.Json\lib\net35\Newtonsoft.Json.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_Client)) |> shouldContain @"..\Newtonsoft.Json\lib\net40\Newtonsoft.Json.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Newtonsoft.Json\lib\net40\Newtonsoft.Json.dll" 

[<Test>]
let ``should install not use net40-full for client profile``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Newtonsoft.Json\lib\net35\Newtonsoft.Json.dll" 
              @"..\Newtonsoft.Json\lib\net40-full\Newtonsoft.Json.dll"])

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldContain @"..\Newtonsoft.Json\lib\net35\Newtonsoft.Json.dll"     
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Newtonsoft.Json\lib\net40-full\Newtonsoft.Json.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_Client)) |> shouldNotContain @"..\Newtonsoft.Json\lib\net40-full\Newtonsoft.Json.dll" 

[<Test>]
let ``should handle lib install of Microsoft.Net.Http for .NET 4.5``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 
                    
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" ])

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" 

[<Test>]
let ``should add portable lib``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" ])

    model.GetLibReferences(KnownTargetProfiles.FindPortableProfile "Profile147") |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

[<Test>]
let ``should handle lib install of Jint for NET >= 40 and SL >= 50``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" ])
   
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll"

    model.GetLibReferences(SinglePlatform (Silverlight "v5.0")) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll"
    
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldNotContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 
    model.GetLibReferences(KnownTargetProfiles.FindPortableProfile "Profile147") |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

[<Test>]
let ``should handle lib install of Microsoft.BCL for NET >= 40``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

              @"..\Microsoft.Bcl\lib\net45\_._" ])
              .FilterBlackList()

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldNotContain  @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldBeEmpty
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5_1)) |> shouldBeEmpty


[<Test>]
let ``should skip lib install of Microsoft.BCL for monotouch and monoandroid``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 
              @"..\Microsoft.Bcl\lib\monoandroid\_._" 
              @"..\Microsoft.Bcl\lib\monotouch\_._" 
              @"..\Microsoft.Bcl\lib\net45\_._" ])
            .FilterBlackList()

    model.GetLibReferences(SinglePlatform MonoAndroid) |> shouldBeEmpty
    model.GetLibReferences(SinglePlatform MonoTouch) |> shouldBeEmpty

[<Test>]
let ``should not use portable-net40 if we have net40``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

              @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll" 
              @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll" 
              @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll" ])
    
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_Client)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_Client)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_Client)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    let profile41 = KnownTargetProfiles.FindPortableProfile "Profile41"
    model.GetLibReferences(profile41) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll" 
    model.GetLibReferences(profile41) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll" 
    model.GetLibReferences(profile41) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll" 

[<Test>]
let ``should handle lib install of DotNetZip 1.9.3``() = 
    let model = emptymodel.AddReferences([ @"..\DotNetZip\lib\net20\Ionic.Zip.dll" ])

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"

[<Test>]
let ``should handle lib install of NUnit 2.6 for windows 8``() = 
    let model = emptymodel.AddReferences([ @"..\NUnit\lib\nunit.framework.dll" ])

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\NUnit\lib\nunit.framework.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\NUnit\lib\nunit.framework.dll"
    model.GetLibReferences(SinglePlatform (Windows "v4.5")) |> shouldContain @"..\NUnit\lib\nunit.framework.dll"


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
              @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Primitives.dll" ])

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 

    model.GetLibReferences(SinglePlatform MonoAndroid) |> shouldContain @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Extensions.dll" 
    model.GetLibReferences(SinglePlatform MonoAndroid) |> shouldContain @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Primitives.dll" 

    model.GetLibReferences(SinglePlatform MonoTouch) |> shouldContain @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Extensions.dll" 
    model.GetLibReferences(SinglePlatform MonoTouch) |> shouldContain @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Primitives.dll" 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll"  
    
    let profile88 = KnownTargetProfiles.FindPortableProfile "Profile88"
    model.GetLibReferences(profile88) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.dll"
    model.GetLibReferences(profile88) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Extensions.dll" 
    model.GetLibReferences(profile88) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Primitives.dll" 

    let profile7 = KnownTargetProfiles.FindPortableProfile "Profile7"
    model.GetLibReferences(profile7) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Extensions.dll" 
    model.GetLibReferences(profile7) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Primitives.dll" 

    model.GetLibReferences(SinglePlatform (Windows "v4.5")) |> shouldContain @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Extensions.dll" 
    model.GetLibReferences(SinglePlatform (Windows "v4.5")) |> shouldContain @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Primitives.dll" 

    model.GetLibReferences(SinglePlatform (WindowsPhoneApp "v8.1")) |> shouldContain @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Extensions.dll" 
    model.GetLibReferences(SinglePlatform (WindowsPhoneApp "v8.1")) |> shouldContain @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Primitives.dll" 


[<Test>]
let ``should handle lib install of MicrosoftBcl``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Microsoft.Net.Http\lib\monoandroid\_._" 
              
              @"..\Microsoft.Net.Http\lib\monotouch\_._" 

              @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll"
              @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

              @"..\Microsoft.Net.Http\lib\net45\_._"

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
              @"..\Microsoft.Bcl\lib\portable-net451+win81+wpa81\_._"
               ]).FilterBlackList()

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldBeEmpty
    model.GetLibReferences(SinglePlatform MonoAndroid) |> shouldBeEmpty
    model.GetLibReferences(SinglePlatform MonoTouch) |> shouldBeEmpty
    model.GetLibReferences(SinglePlatform (Windows "v4.5")) |> shouldBeEmpty
    model.GetLibReferences(SinglePlatform (WindowsPhoneSilverlight "v8.0")) |> shouldBeEmpty
    model.GetLibReferences(SinglePlatform (WindowsPhoneApp "v8.1")) |> shouldBeEmpty
    model.GetLibReferences(KnownTargetProfiles.FindPortableProfile "Profile44") |> shouldBeEmpty
    model.GetLibReferences(KnownTargetProfiles.FindPortableProfile "Profile151") |> shouldBeEmpty
    
    let profile41 = KnownTargetProfiles.FindPortableProfile "Profile41"
    model.GetLibReferences(profile41) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll"
    model.GetLibReferences(profile41) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll"
    model.GetLibReferences(profile41) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll" 

    model.GetLibReferences(SinglePlatform (Silverlight "v4.0")) |> shouldContain @"..\Microsoft.Bcl\lib\sl4\System.IO.dll"
    model.GetLibReferences(SinglePlatform (Silverlight "v4.0")) |> shouldContain @"..\Microsoft.Bcl\lib\sl4\System.Runtime.dll"
    model.GetLibReferences(SinglePlatform (Silverlight "v4.0")) |> shouldContain @"..\Microsoft.Bcl\lib\sl4\System.Threading.Tasks.dll" 
    
    model.GetLibReferences(SinglePlatform (WindowsPhoneSilverlight "v7.1")) |> shouldContain @"..\Microsoft.Bcl\lib\sl4-windowsphone71\System.IO.dll"
    model.GetLibReferences(SinglePlatform (WindowsPhoneSilverlight "v7.1")) |> shouldContain @"..\Microsoft.Bcl\lib\sl4-windowsphone71\System.Runtime.dll"
    model.GetLibReferences(SinglePlatform (WindowsPhoneSilverlight "v7.1")) |> shouldContain @"..\Microsoft.Bcl\lib\sl4-windowsphone71\System.Threading.Tasks.dll" 

    model.GetLibReferences(SinglePlatform (Silverlight "v5.0")) |> shouldContain @"..\Microsoft.Bcl\lib\sl5\System.IO.dll"
    model.GetLibReferences(SinglePlatform (Silverlight "v5.0")) |> shouldContain @"..\Microsoft.Bcl\lib\sl5\System.Runtime.dll"
    model.GetLibReferences(SinglePlatform (Silverlight "v5.0")) |> shouldContain @"..\Microsoft.Bcl\lib\sl5\System.Threading.Tasks.dll" 


[<Test>]
let ``should handle lib install of Fantomas 1.5``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ])

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

[<Test>]
let ``should handle lib install of Fantomas 1.5.0 with explicit references``() = 
    let model = 
        emptymodel.AddLibReferences(
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
            NuspecReferences.Explicit ["FantomasLib.dll"])
            
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V3_5)) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 


[<Test>]
let ``should only handle dll and exe files``() = 
    let model = 
        emptymodel.AddLibReferences(
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FantomasLib.xml" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
            NuspecReferences.All)
            .FilterBlackList()

    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\Fantomas\lib\Fantomas.exe" 
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldNotContain @"..\Fantomas\lib\FantomasLib.xml" 

[<Test>]
let ``should use portable net40 in net45 when don't have other files``() = 
    let model = 
        emptymodel.AddLibReferences(
            [ @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll" ],
            NuspecReferences.All)
            
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5)) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5_1)) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5_2)) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_5_3)) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"

[<Test>]
let ``should not install tools``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\FAKE\tools\FAKE.exe" 
              @"..\FAKE\tools\FakeLib.dll" 
              @"..\FAKE\tools\Fake.SQL.dll" ])

    model.ReferenceFileFolders
    |> Seq.forall (fun folder -> folder.Files.References.IsEmpty)
    |> shouldEqual true

[<Test>]
let ``should handle props files``() = 
    let model = 
        emptymodel.AddTargetsFiles(
            [ @"..\xunit.runner.visualstudio\build\net20\xunit.runner.visualstudio.props" 
              @"..\xunit.runner.visualstudio\build\portable-net45+aspnetcore50+win+wpa81+wp80+monotouch+monoandroid\xunit.runner.visualstudio.props" ])
            .FilterBlackList()

    model.GetTargetsFiles(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\xunit.runner.visualstudio\build\net20\xunit.runner.visualstudio.props"

[<Test>]
let ``should handle Targets files``() = 
    let model = 
        emptymodel.AddTargetsFiles(
            [ @"..\StyleCop.MSBuild\build\StyleCop.MSBuild.Targets" ])
            .FilterBlackList()

    model.GetTargetsFiles(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\StyleCop.MSBuild\build\StyleCop.MSBuild.Targets"