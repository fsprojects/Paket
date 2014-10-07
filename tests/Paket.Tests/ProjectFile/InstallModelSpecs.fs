module Paket.ProjectFile.InstallationModelpecs

open Paket
open NUnit.Framework
open FsUnit

[<Literal>]
let placeHolder = "_._"
let blackList =
    [fun (f:string) -> f.Contains placeHolder]

let KnownDotNetFrameworks = [ "v1.0",Full; "v1.1",Full; "v2.0",Full; "v3.5",Full; "v4.0",Client; "v4.0",Full; "v4.5",Full; "v4.5.1",Full ]

type InstallModell = 
    { Frameworks : Map<FrameworkIdentifier, string Set> }
    
    static member EmptyModel : InstallModell = 
        let frameworks = 
            [ for x,p in KnownDotNetFrameworks -> DotNetFramework(Framework x, p) ]
        { Frameworks = List.fold (fun map f -> Map.add f Set.empty map) Map.empty frameworks }
    
    member this.GetFrameworks() = this.Frameworks |> Seq.map (fun kv -> kv.Key)
    member this.GetFiles(framework) = this.Frameworks.[framework]

let addToModel framework lib (model : InstallModell) : InstallModell = 
    { model with Frameworks = 
                     match Map.tryFind framework model.Frameworks with
                     | Some files -> Map.add framework (Set.add lib files) model.Frameworks
                     | None -> Map.add framework (Set.singleton lib) model.Frameworks }

let extractFrameworksFromPaths (model : InstallModell) libs : InstallModell = 
    libs |> List.fold (fun model lib -> 
                match FrameworkIdentifier.DetectFromPathNew lib with
                | Some framework -> addToModel framework lib model
                | _ -> model) model

let useLowerVersionLibIfEmpty (model : InstallModell) = 
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
                      | _ -> model) model) model

let usePortableVersionLibIfEmpty (model : InstallModell) = 
    model.Frameworks 
    |> Seq.fold 
           (fun (model : InstallModell) kv -> 
           let newFiles = kv.Value
           
           let otherProfiles = 
               match kv.Key with
               | PortableFramework(_, f) -> 
                   f.Split([| '+' |], System.StringSplitOptions.RemoveEmptyEntries)
                   |> Array.map (FrameworkIdentifier.Extract false)
                   |> Array.choose id
               | _ -> [||]
           if Set.isEmpty newFiles || Array.isEmpty otherProfiles then model
           else 
               otherProfiles 
               |> Array.fold (fun (model : InstallModell) framework -> 
                      match Map.tryFind framework model.Frameworks with
                      | Some files when Set.isEmpty files |> not -> model
                      | _ -> { model with Frameworks = Map.add framework newFiles model.Frameworks }) model) model


let filterBlackList (model : InstallModell) = 
    { model with Frameworks = 
                     blackList 
                     |> List.fold 
                            (fun frameworks f -> 
                                frameworks 
                                |> Map.map (fun _ files -> files |> Set.filter (f >> not))) 
                        model.Frameworks }

[<Test>]
let ``should create empty model with net40, net45 ...``() = 
    let model = 
        [ @"..\Rx-Main\lib\net40\Rx.dll"; @"..\Rx-Main\lib\net45\Rx.dll" ] 
        |> extractFrameworksFromPaths InstallModell.EmptyModel

    model.GetFrameworks() |> shouldContain (DotNetFramework(Framework "v4.0", Full))
    model.GetFrameworks() |> shouldContain (DotNetFramework(Framework "v4.5", Full))

[<Test>]
let ``should understand net40 and net45``() = 
    let model = 
        [ @"..\Rx-Main\lib\net40\Rx.dll"; @"..\Rx-Main\lib\net45\Rx.dll" ] 
        |> extractFrameworksFromPaths InstallModell.EmptyModel
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldContain @"..\Rx-Main\lib\net45\Rx.dll"

[<Test>]
let ``should add net35 if we have net20 and net40``() = 
    let model = 
        [ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\Rx.dll" ]
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> useLowerVersionLibIfEmpty

    model.GetFiles(DotNetFramework(Framework "v2.0", Full)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework "v3.5", Full)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"
    model.GetFiles(DotNetFramework(Framework "v4.5.1", Full)) |> shouldContain @"..\Rx-Main\lib\net40\Rx.dll"

[<Test>]
let ``should put _._ files into right buckets``() = 
    let model = 
        [ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ] 
        |> extractFrameworksFromPaths InstallModell.EmptyModel
    model.GetFiles(DotNetFramework(Framework "v2.0", Full)) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Rx-Main\lib\net40\_._"

[<Test>]
let ``should inherit _._ files to higher frameworks``() = 
    let model = 
        [ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ] 
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> useLowerVersionLibIfEmpty

    model.GetFiles(DotNetFramework(Framework "v2.0", Full)) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(Framework "v3.5", Full)) |> shouldContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Rx-Main\lib\net40\_._"
    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldContain @"..\Rx-Main\lib\net40\_._"
    model.GetFiles(DotNetFramework(Framework "v4.5.1", Full)) |> shouldContain @"..\Rx-Main\lib\net40\_._"


[<Test>]
let ``should skip buckets which contain placeholder while adjusting upper versions``() = 
    let model = 
        [ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\_._"; ]
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> useLowerVersionLibIfEmpty

    model.GetFiles(DotNetFramework(Framework "v2.0", Full)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework "v3.5", Full)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"

[<Test>]
let ``should filter _._ when processing blacklist``() = 
    let model = 
        [ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ] 
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> filterBlackList

    model.GetFiles(DotNetFramework(Framework "v2.0", Full)) |> shouldNotContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldNotContain @"..\Rx-Main\lib\net40\_._"

[<Test>]
let ``should install single client profile lib for everything``() = 
    let model = 
        [ @"..\Castle.Core\lib\net40-client\Castle.Core.dll" ] 
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> useLowerVersionLibIfEmpty

    model.GetFiles(DotNetFramework(Framework "v3.5", Full)) |> shouldNotContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"
    model.GetFiles(DotNetFramework(Framework "v4.0", Client)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll" 
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"
    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"

[<Test>]
let ``should handle lib install of Microsoft.Net.Http for .NET 4.5``() = 
    let model = 
        [ @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
          @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll" 
          @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
          @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 
                    
          @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll" 
          @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll"           
        ] 
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> useLowerVersionLibIfEmpty

    model.GetFiles(DotNetFramework(Framework "v3.5", Full)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"

    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 

    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldNotContain @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" 
    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldContain @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" 

[<Test>]
let ``should handle lib install of Jint for NET >= 40 and SL >= 50``() = 
    let model = 
        [ @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" ]
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> usePortableVersionLibIfEmpty

    model.GetFiles(PortableFramework("7.0", "net40+sl50+win+wp80")) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

    model.GetFiles(DotNetFramework(Framework "v3.5", Full)) |> shouldNotContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll"

    model.GetFiles(Silverlight("v5.0")) |> shouldContain @"..\Jint\lib\portable-net40+sl50+win+wp80\Jint.dll" 

[<Test>]
let ``should handle lib install of Microsoft.BCL for NET >= 40``() = 
    let model = 
        [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
          @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
          @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

          @"..\Microsoft.Bcl\lib\net45\_._" 
        ]
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> useLowerVersionLibIfEmpty
        |> filterBlackList

    model.GetFiles(DotNetFramework(Framework "v3.5", Full)) |> shouldNotContain  @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 

    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldBeEmpty
    model.GetFiles(DotNetFramework(Framework "v4.5.1", Full)) |> shouldBeEmpty


[<Test>]
let ``should skip lib install of Microsoft.BCL for monotouch and monoandroid``() = 
    let model = 
        [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
          @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
          @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 
          @"..\Microsoft.Bcl\lib\monoandroid\_._" 
          @"..\Microsoft.Bcl\lib\monotouch\_._" 
          @"..\Microsoft.Bcl\lib\net45\_._" 
        ]
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> useLowerVersionLibIfEmpty
        |> filterBlackList

    model.GetFiles(MonoAndroid) |> shouldBeEmpty
    model.GetFiles(MonoTouch) |> shouldBeEmpty

[<Test>]
let ``should not use portable-net40 if we have net40``() = 
    let model = 
        [ @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
          @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
          @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

          @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll" 
          @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll" 
          @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll" ]
        |> extractFrameworksFromPaths InstallModell.EmptyModel
    
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.IO.dll" 
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Runtime.dll" 
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Microsoft.Bcl\lib\net40\System.Threading.Tasks.dll" 

    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8")) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.IO.dll" 
    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8")) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Runtime.dll" 
    model.GetFiles(PortableFramework("7.0", "net40+sl4+win8")) |> shouldContain @"..\Microsoft.Bcl\lib\portable-net40+sl4+win8\System.Threading.Tasks.dll" 