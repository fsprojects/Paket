module Paket.InstallModelSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Literal>]
let placeHolder = "_._"
let blackList =
    [fun (f:string) -> f.Contains placeHolder]



let KnownDotNetFrameworks = 
    [ FrameworkVersionNo.V1, Full
      FrameworkVersionNo.V1_1, Full
      FrameworkVersionNo.V2, Full
      FrameworkVersionNo.V3_5, Full
      FrameworkVersionNo.V4, Client
      FrameworkVersionNo.V4, Full
      FrameworkVersionNo.V4_5, Full
      FrameworkVersionNo.V4_5_1, Full ]

type InstallModell = 
    {   PackageName: string
        PackageVersion: SemVerInfo
        Frameworks : Map<FrameworkIdentifier, string Set> }
    
    static member EmptyModel(packageName,packageVersion) : InstallModell = 
        let frameworks = 
            [ for x,p in KnownDotNetFrameworks -> DotNetFramework(Framework x, p) ]
        {   PackageName = packageName
            PackageVersion = packageVersion
            Frameworks = List.fold (fun map f -> Map.add f Set.empty map) Map.empty frameworks }
    
    member this.GetFrameworks() = this.Frameworks |> Seq.map (fun kv -> kv.Key)
    member this.GetFiles(framework) = 
        match this.Frameworks.TryFind framework with
        | Some libs -> libs
        | None -> Set.empty

    member this.Add(framework,lib:string,references) : InstallModell = 
        let install =
            match references with
            | Nuspec.References.All -> true
            | Nuspec.References.Explicit list -> list |> List.exists lib.EndsWith

        if not install then this else
        { this with Frameworks = 
                     match Map.tryFind framework this.Frameworks with
                     | Some files -> Map.add framework (Set.add lib files) this.Frameworks
                     | None -> Map.add framework (Set.singleton lib) this.Frameworks }

    member this.Add (libs,references) : InstallModell =         
        libs |> List.fold (fun model lib -> 
                    match FrameworkIdentifier.DetectFromPathNew lib with
                    | Some framework -> model.Add(framework,lib,references)
                    | _ -> model) this

    member this.Add libs = this.Add(libs, Nuspec.References.All)

    member this.FilterBlackList() =
        { this with Frameworks = 
                     blackList 
                     |> List.fold 
                            (fun frameworks f -> Map.map (fun _ files -> files |> Set.filter (f >> not)) frameworks) 
                        this.Frameworks }

    member this.UseGenericFrameworkVersionIfEmpty() =
        let genericFramework = DotNetFramework(All, Full)
        let newFiles = this.GetFiles genericFramework
               
        let model =
            if Set.isEmpty newFiles then this else

            let target = DotNetFramework(Framework FrameworkVersionNo.V1,Full)
            match Map.tryFind target this.Frameworks with
            | Some files when Set.isEmpty files |> not -> this
            | _ -> { this with Frameworks = Map.add target newFiles this.Frameworks }

        { model with Frameworks = model.Frameworks |> Map.remove genericFramework } 

    member this.UseLowerVersionLibIfEmpty() = 
        KnownDotNetFrameworks
        |> List.rev
        |> List.fold (fun (model : InstallModell) (lowerVersion,lowerProfile) -> 
               let newFiles = model.GetFiles(DotNetFramework(Framework lowerVersion, lowerProfile))
               if Set.isEmpty newFiles then model
               else 
                   KnownDotNetFrameworks
                   |> List.filter (fun (version,profile) -> (version,profile) > (lowerVersion,lowerProfile))
                   |> List.fold (fun (model : InstallModell) (upperVersion,upperProfile) -> 
                          let framework = DotNetFramework(Framework upperVersion, upperProfile)
                          match Map.tryFind framework model.Frameworks with
                          | Some files when Set.isEmpty files -> 
                              { model with Frameworks = Map.add framework newFiles model.Frameworks }
                          | _ -> model) model) this

    member this.UsePortableVersionLibIfEmpty() = 
        this.Frameworks 
        |> Seq.fold 
               (fun (model : InstallModell) kv -> 
               let newFiles = kv.Value
           
               if Set.isEmpty newFiles then model else

               let otherProfiles = 
                   match kv.Key with
                   | PortableFramework(_, f) -> 
                       f.Split([| '+' |], System.StringSplitOptions.RemoveEmptyEntries)
                       |> Array.map (FrameworkIdentifier.Extract false)
                       |> Array.choose id
                   | _ -> [||]

               if Array.isEmpty otherProfiles then model else 
                otherProfiles 
                |> Array.fold (fun (model : InstallModell) framework -> 
                        match Map.tryFind framework model.Frameworks with
                        | Some files when Set.isEmpty files |> not -> model
                        | _ -> { model with Frameworks = Map.add framework newFiles model.Frameworks }) model) this

    member this.Process() =
        this
            .UseGenericFrameworkVersionIfEmpty()
            .UsePortableVersionLibIfEmpty()
            .UseLowerVersionLibIfEmpty()
            .FilterBlackList()

    static member CreateFromLibs(packageName,packageVersions,libs) = 
        InstallModell.EmptyModel(packageName,packageVersions)
            .Add(libs)
            .Process()


let emptymodel = InstallModell.EmptyModel("Unknown",SemVer.parse "0.1")

[<Test>]
let ``should create empty model with net40, net45 ...``() = 
    let model = emptymodel.Add [ @"..\Rx-Main\lib\net40\Rx.dll"; @"..\Rx-Main\lib\net45\Rx.dll" ] 

    model.GetFrameworks() |> shouldContain (DotNetFramework(Framework FrameworkVersionNo.V4, Full))
    model.GetFrameworks() |> shouldContain (DotNetFramework(Framework FrameworkVersionNo.V4_5, Full))

[<Test>]
let ``should understand net40 and net45``() = 
    let model = emptymodel.Add [ @"..\Rx-Main\lib\net40\Rx.dll"; @"..\Rx-Main\lib\net45\Rx.dll" ] 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldContain @"..\Rx-Main\lib\net45\Rx.dll"

[<Test>]
let ``should add net35 if we have net20 and net40``() = 
    let model = 
        emptymodel.Add([ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\Rx.dll" ])
            .UseLowerVersionLibIfEmpty()

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V2, Full)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5_1, Full)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"

[<Test>]
let ``should put _._ files into right buckets``() = 
    let model = emptymodel.Add [ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ] 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V2, Full)) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Rx-Main\lib\net40\_._"

[<Test>]
let ``should inherit _._ files to higher frameworks``() = 
    let model = 
        emptymodel.Add([ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ])
            .UseLowerVersionLibIfEmpty()

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V2, Full)) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Rx-Main\lib\net40\_._"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldContain @"..\Rx-Main\lib\net40\_._"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5_1, Full)) |> shouldContain @"..\Rx-Main\lib\net40\_._"


[<Test>]
let ``should skip buckets which contain placeholder while adjusting upper versions``() = 
    let model = 
        emptymodel.Add([ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\_._"; ])
            .UseLowerVersionLibIfEmpty()

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V2, Full)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"

[<Test>]
let ``should filter _._ when processing blacklist``() = 
    let model = 
        emptymodel.Add([ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ])
            .FilterBlackList()

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V2, Full)) |> shouldNotContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldNotContain @"..\Rx-Main\lib\net40\_._"

[<Test>]
let ``should install single client profile lib for everything``() = 
    let model = 
        emptymodel.Add([ @"..\Castle.Core\lib\net40-client\Castle.Core.dll" ])
            .UseLowerVersionLibIfEmpty()

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldNotContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Client)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"

[<Test>]
let ``should handle lib install of Microsoft.Net.Http for .NET 4.5``() = 
    let model = 
        emptymodel.Add(
            [ @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 
                    
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" ])
            .UseLowerVersionLibIfEmpty()

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" 

[<Test>]
let ``should handle lib install of Jint for NET >= 40 and SL >= 50``() = 
    let model = 
        emptymodel.Add([ @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" ])
            .UsePortableVersionLibIfEmpty()

    model.GetFiles(PortableFramework("7.0", "net40+sl50+win+wp80")) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldNotContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll"

    model.GetFiles(Silverlight("v5.0")) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

[<Test>]
let ``should handle lib install of Microsoft.BCL for NET >= 40``() = 
    let model = 
        emptymodel.Add(
            [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

              @"..\Microsoft.Bcl\lib\net45\_._" ])
            .UseLowerVersionLibIfEmpty()
            .FilterBlackList()

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldNotContain  @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldBeEmpty
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5_1, Full)) |> shouldBeEmpty


[<Test>]
let ``should skip lib install of Microsoft.BCL for monotouch and monoandroid``() = 
    let model = 
        emptymodel.Add(
            [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 
              @"..\Microsoft.Bcl\lib\monoandroid\_._" 
              @"..\Microsoft.Bcl\lib\monotouch\_._" 
              @"..\Microsoft.Bcl\lib\net45\_._" ])
            .UseLowerVersionLibIfEmpty()
            .FilterBlackList()

    model.GetFiles(MonoAndroid) |> shouldBeEmpty
    model.GetFiles(MonoTouch) |> shouldBeEmpty

[<Test>]
let ``should not use portable-net40 if we have net40``() = 
    let model = 
        emptymodel.Add(
            [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
              @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

              @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll" 
              @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll" 
              @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll" ])
    
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8")) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll" 
    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8")) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll" 
    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8")) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll" 

[<Test>]
let ``should handle lib install of DotNetZip 1.9.3``() = 
    let model = emptymodel.Add([ @"..\DotNetZip\lib\net20\Ionic.Zip.dll" ]).Process()

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V2, Full)) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldContain @"..\DotNetZip\lib\net20\Ionic.Zip.dll"


[<Test>]
let ``should handle lib install of Microsoft.Net.Http 2.2.28``() = 
    let model = 
        emptymodel.Add(
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

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 

    model.GetFiles(MonoAndroid) |> shouldContain @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Extensions.dll" 
    model.GetFiles(MonoAndroid) |> shouldContain @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Primitives.dll" 

    model.GetFiles(MonoTouch) |> shouldContain @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Extensions.dll" 
    model.GetFiles(MonoTouch) |> shouldContain @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Primitives.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll"  
    
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
        emptymodel.Add(
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

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll"
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldBeEmpty
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
        emptymodel.Add(
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ])
          .UseGenericFrameworkVersionIfEmpty()
          .UseLowerVersionLibIfEmpty()

    model.GetFiles(DotNetFramework(All, Full)) |> shouldBeEmpty
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V2, Full)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V2, Full)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldContain @"..\Fantomas\lib\FSharp.Core.dll" 

[<Test>]
let ``should handle lib install of Fantomas 1.5.0 with explicit references``() = 
    let model = 
        emptymodel.Add(
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ], Nuspec.Explicit ["FantomasLib.dll"])
            .Process()
            
    model.GetFiles(DotNetFramework(All, Full)) |> shouldBeEmpty
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V2, Full)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V2, Full)) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldNotContain @"..\Fantomas\lib\FSharp.Core.dll" 

[<Test>]
let ``should not install tools``() = 
    let model = 
        emptymodel.Add(
            [ @"..\FAKE\tools\FAKE.exe" 
              @"..\FAKE\tools\FakeLib.dll" 
              @"..\FAKE\tools\Fake.SQL.dll" ])
            .Process()

    model.Frameworks
    |> Seq.forall (fun kv -> kv.Value.IsEmpty)
    |> shouldEqual true