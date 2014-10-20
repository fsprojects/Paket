module Paket.InstallModel.ProcessingSpecs

open Paket
open NUnit.Framework
open FsUnit

let emptymodel = InstallModel.EmptyModel("Unknown",SemVer.Parse "0.1")

[<Test>]
let ``should create empty model with net40, net45 ...``() = 
    let model = emptymodel.AddReferences [ @"..\Rx-Main\lib\net40\Rx.dll"; @"..\Rx-Main\lib\net45\Rx.dll" ] 

    model.GetFrameworks() |> shouldContain (DotNetFramework(FrameworkVersion.V4))
    model.GetFrameworks() |> shouldContain (DotNetFramework(FrameworkVersion.V4_5))

[<Test>]
let ``should understand net40 and net45``() = 
    let model = emptymodel.AddReferences [ @"..\Rx-Main\lib\net40\Rx.dll"; @"..\Rx-Main\lib\net45\Rx.dll" ] 

    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\Rx-Main\lib\net45\Rx.dll"

[<Test>]
let ``should add net35 if we have net20 and net40``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\Rx.dll" ])
            .UseLowerVersionLibIfEmpty()

    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5_1)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"

[<Test>]
let ``should put _._ files into right buckets``() = 
    let model = emptymodel.AddReferences [ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ] 

    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Rx-Main\lib\net40\_._"

[<Test>]
let ``should inherit _._ files to higher frameworks``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ])
            .UseLowerVersionLibIfEmpty()

    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Rx-Main\lib\net40\_._"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\Rx-Main\lib\net40\_._"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5_1)) |> shouldContain @"..\Rx-Main\lib\net40\_._"


[<Test>]
let ``should skip buckets which contain placeholder while adjusting upper versions``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\_._"; ])
            .UseLowerVersionLibIfEmpty()

    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"

[<Test>]
let ``should filter _._ when processing blacklist``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ])
            .FilterBlackList()

    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldNotContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldNotContain @"..\Rx-Main\lib\net40\_._"

[<Test>]
let ``should install single client profile lib for everything``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Castle.Core\lib\net40-client\Castle.Core.dll" ])
            .UseLowerVersionLibIfEmpty()

    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldNotContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_Client)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"

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
            .UseLowerVersionLibIfEmpty()

    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"

    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" 

[<Test>]
let ``should handle lib install of Jint for NET >= 40 and SL >= 50``() = 
    let model = 
        emptymodel.AddReferences([ @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" ])
            .UsePortableVersionLibIfEmpty()

    model.GetFiles(PortableFramework("7.0", "net40+sl50+win+wp80")) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldNotContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll"

    model.GetFiles(Silverlight("v5.0")) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

[<Test>]
let ``should handle lib install of Microsoft.BCL for NET >= 40``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

              @"..\Microsoft.Bcl\lib\net45\_._" ])
            .Process()

    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldNotContain  @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldBeEmpty
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5_1)) |> shouldBeEmpty


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
            .Process()

    model.GetFiles(MonoAndroid) |> shouldBeEmpty
    model.GetFiles(MonoTouch) |> shouldBeEmpty

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
    
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8")) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll" 
    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8")) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll" 
    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8")) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll" 

[<Test>]
let ``should handle lib install of DotNetZip 1.9.3``() = 
    let model = emptymodel.AddReferences([ @"..\DotNetZip\lib\net20\Ionic.Zip.dll" ]).Process()

    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"

[<Test>]
let ``should reduce lib install of DotNetZip 1.9.3``() = 
    let model = emptymodel.AddReferences([ @"..\DotNetZip\lib\net20\Ionic.Zip.dll" ]).ProcessAndReduce()

    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldNotContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldNotContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"

[<Test>]
let ``should handle lib install of NUnit 2.6 for windows 8``() = 
    let model = emptymodel.AddReferences([ @"..\NUnit\lib\nunit.framework.dll" ]).Process()

    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldContain @"..\NUnit\lib\nunit.framework.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\NUnit\lib\nunit.framework.dll"
    model.GetFiles(Windows("v8.0")) |> shouldContain @"..\NUnit\lib\nunit.framework.dll"


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
            .Process()

    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"

    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 

    model.GetFiles(MonoAndroid) |> shouldContain @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Extensions.dll" 
    model.GetFiles(MonoAndroid) |> shouldContain @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Primitives.dll" 

    model.GetFiles(MonoTouch) |> shouldContain @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Extensions.dll" 
    model.GetFiles(MonoTouch) |> shouldContain @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Primitives.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll"  
    
    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8+wp71+wpa81")) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.dll"
    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8+wp71+wpa81")) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Extensions.dll" 
    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8+wp71+wpa81")) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Primitives.dll" 

    model.GetFiles(PortableFramework("7.0", "net45+win8")) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Extensions.dll" 
    model.GetFiles(PortableFramework("7.0", "net45+win8")) |> shouldContain @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Primitives.dll" 

    model.GetFiles(Windows("v8.0")) |> shouldContain @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Extensions.dll" 
    model.GetFiles(Windows("v8.0")) |> shouldContain @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Primitives.dll" 

    model.GetFiles(WindowsPhoneApp("v8.1")) |> shouldContain @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Extensions.dll" 
    model.GetFiles(WindowsPhoneApp("v8.1")) |> shouldContain @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Primitives.dll" 


[<Test>]
let ``should handle lib install of Microsoft.Bcl 1.1.9``() = 
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
               ])
            .Process()

    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldBeEmpty
    model.GetFiles(MonoAndroid) |> shouldBeEmpty
    model.GetFiles(MonoTouch) |> shouldBeEmpty
    model.GetFiles(Windows "v8.0") |> shouldBeEmpty
    model.GetFiles(WindowsPhoneApp "v8.0") |> shouldBeEmpty
    model.GetFiles(WindowsPhoneApp "v8.1") |> shouldBeEmpty
    model.GetFiles(PortableFramework("7.0", "net451+win81")) |> shouldBeEmpty
    model.GetFiles(PortableFramework("7.0", "net451+win81+wpa81")) |> shouldBeEmpty
    
    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8")) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll"
    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8")) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll"
    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8")) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll" 

    model.GetFiles(Silverlight("v4.0")) |> shouldContain @"..\Microsoft.Bcl\lib\sl4\System.IO.dll"
    model.GetFiles(Silverlight("v4.0")) |> shouldContain @"..\Microsoft.Bcl\lib\sl4\System.Runtime.dll"
    model.GetFiles(Silverlight("v4.0")) |> shouldContain @"..\Microsoft.Bcl\lib\sl4\System.Threading.Tasks.dll" 
    
    model.GetFiles(WindowsPhoneApp "7.1") |> shouldContain @"..\Microsoft.Bcl\lib\sl4-windowsphone71\System.IO.dll"
    model.GetFiles(WindowsPhoneApp "7.1") |> shouldContain @"..\Microsoft.Bcl\lib\sl4-windowsphone71\System.Runtime.dll"
    model.GetFiles(WindowsPhoneApp "7.1") |> shouldContain @"..\Microsoft.Bcl\lib\sl4-windowsphone71\System.Threading.Tasks.dll" 

    model.GetFiles(Silverlight("v5.0")) |> shouldContain @"..\Microsoft.Bcl\lib\sl5\System.IO.dll"
    model.GetFiles(Silverlight("v5.0")) |> shouldContain @"..\Microsoft.Bcl\lib\sl5\System.Runtime.dll"
    model.GetFiles(Silverlight("v5.0")) |> shouldContain @"..\Microsoft.Bcl\lib\sl5\System.Threading.Tasks.dll" 


[<Test>]
let ``should handle lib install of Fantomas 1.5``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ])
          .Process()

    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

[<Test>]
let ``should handle lib install of Fantomas 1.5.0 with explicit references``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ], NuspecReferences.Explicit ["FantomasLib.dll"])
            .Process()
            
    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V3_5)) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 


[<Test>]
let ``should only handle dll and exe files``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FantomasLib.xml" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ], NuspecReferences.All)
            .Process()
            
    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldContain @"..\Fantomas\lib\Fantomas.exe" 
    model.GetFiles(DotNetFramework(FrameworkVersion.V2)) |> shouldNotContain @"..\Fantomas\lib\FantomasLib.xml" 

[<Test>]
let ``should use portable net40 in net45 when don't have other files``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll" ], NuspecReferences.All)
            .Process()
            
    model.GetFiles(DotNetFramework(FrameworkVersion.V4)) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5)) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5_1)) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5_2)) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"
    model.GetFiles(DotNetFramework(FrameworkVersion.V4_5_3)) |> shouldContain @"..\Google.Apis.Core\lib\portable-net40+sl50+win+wpa81+wp80\Google.Apis.Core.dll"

[<Test>]
let ``should not install tools``() = 
    let model = 
        emptymodel.AddReferences(
            [ @"..\FAKE\tools\FAKE.exe" 
              @"..\FAKE\tools\FakeLib.dll" 
              @"..\FAKE\tools\Fake.SQL.dll" ])
            .Process()

    model.Frameworks
    |> Seq.forall (fun kv -> kv.Value.References.IsEmpty)
    |> shouldEqual true
