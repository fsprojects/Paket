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
                match FrameworkIdentifier.DetectFromPath lib with
                | Some framework -> addToModel framework lib model
                | None -> model) model

let useLowerVersionLibIfEmpty (model : InstallModell) = 
    KnownDotNetFrameworks
    |> List.rev
    |> List.fold (fun (model : InstallModell) (lowerVersion,lowerProfile) -> 
           let newFiles = model.GetFiles(DotNetFramework(Framework lowerVersion, lowerProfile))
           let containsPlaceHolder = newFiles |> Set.exists (fun x -> x.Contains(placeHolder))
           if Set.isEmpty newFiles || containsPlaceHolder then model
           else 
               KnownDotNetFrameworks
               |> List.filter (fun (version,profile) -> (version,profile) > (lowerVersion,lowerProfile))
               |> List.fold (fun (model : InstallModell) (upperVersion,upperProfile) -> 
                      let framework = DotNetFramework(Framework upperVersion, upperProfile)
                      match Map.tryFind framework model.Frameworks with
                      | Some files when Set.isEmpty files -> 
                          { model with Frameworks = Map.add framework newFiles model.Frameworks }
                      | _ -> model) model) model


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
let ``should skip buckets which contain placeholder while adjusting upper versions``() = 
    let model = 
        [ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\_._"; ]
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> useLowerVersionLibIfEmpty

    model.GetFiles(DotNetFramework(Framework "v2.0", Full)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework "v3.5", Full)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldNotContain @"..\Rx-Main\lib\net20\Rx.dll"
    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldContain @"..\Rx-Main\lib\net20\Rx.dll"

[<Test>]
let ``should filter _._ when processing blacklist``() = 
    let model = 
        [ @"..\Rx-Main\lib\net40\_._"; @"..\Rx-Main\lib\net20\_._" ] 
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> filterBlackList

    model.GetFiles(DotNetFramework(Framework "v2.0", Full)) |> shouldNotContain @"..\Rx-Main\lib\net20\_._"
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldNotContain @"..\Rx-Main\lib\net40\_._"

[<Test>]
let ``should install single client profile lib for everything ``() = 
    let model = 
        [ @"..\Castle.Core\lib\net40-client\Castle.Core.dll" ] 
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> useLowerVersionLibIfEmpty

    model.GetFiles(DotNetFramework(Framework "v3.5", Full)) |> shouldNotContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"
    model.GetFiles(DotNetFramework(Framework "v4.0", Client)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll" 
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"
    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldContain @"..\Castle.Core\lib\net40-client\Castle.Core.dll"