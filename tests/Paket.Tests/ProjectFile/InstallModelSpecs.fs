module Paket.ProjectFile.InstallationModelpecs

open Paket
open NUnit.Framework
open FsUnit

let KnownDotNetFrameworks = [ "v1.0"; "v1.1"; "v2.0"; "v3.5"; "v4.0"; "v4.5"; "v4.5.1" ]

type InstallModell = 
    { Frameworks : Map<FrameworkIdentifier, string Set> }
    static member EmptyModel : InstallModell = 
        let frameworks = 
            [ for x in KnownDotNetFrameworks -> DotNetFramework(Framework x, Full) ]
        { Frameworks = List.fold (fun map f -> Map.add f Set.empty map) Map.empty frameworks }
    member this.GetFrameworks() = this.Frameworks |> Seq.map (fun kv -> kv.Key)
    member this.GetFiles(framework) = this.Frameworks.[framework]

let addToModel framework lib (model : InstallModell) : InstallModell = 
    { model with Frameworks = 
                     match Map.tryFind framework model.Frameworks with
                     | Some files -> Map.add framework (Set.add lib files) model.Frameworks
                     | None -> Map.add framework (Set.singleton lib) model.Frameworks }

let addToModelIfEmpty framework lib (model : InstallModell) : InstallModell = 
    { model with Frameworks = 
                     match Map.tryFind framework model.Frameworks with
                     | Some files ->
                        if Set.isEmpty files then Map.add framework (Set.singleton lib) model.Frameworks else model.Frameworks
                     | None -> Map.add framework (Set.singleton lib) model.Frameworks }

let extractFrameworksFromPaths (model : InstallModell) libs : InstallModell = 
    libs |> List.fold (fun model lib -> 
                match FrameworkIdentifier.DetectFromPath lib with
                | Some framework -> addToModel framework lib model
                | None -> model) model

let useLowerVersionLibIfEmpty (model : InstallModell) =
    let addToUpper upperVersion files model =
        files
        |> Seq.fold (fun model file -> addToModelIfEmpty (DotNetFramework(Framework upperVersion, Full)) file model) model

    KnownDotNetFrameworks
    |> List.rev
    |> List.fold (fun (model:InstallModell) lowerVersion ->
            let newFiles = model.GetFiles(DotNetFramework(Framework lowerVersion, Full)) 
            let upperVersions = KnownDotNetFrameworks |> List.filter (fun version -> version > lowerVersion)
            upperVersions
            |> List.fold (fun (model:InstallModell) upperVersion -> addToUpper upperVersion newFiles model) model
        ) model


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
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain (@"..\Rx-Main\lib\net40\Rx.dll")
    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldContain (@"..\Rx-Main\lib\net45\Rx.dll")

[<Test>]
let ``should add net35 if we have net20 and net40``() = 
    let model = 
        [ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\Rx.dll" ]
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> useLowerVersionLibIfEmpty

    model.GetFiles(DotNetFramework(Framework "v2.0", Full)) |> shouldContain (@"..\Rx-Main\lib\net20\Rx.dll")
    model.GetFiles(DotNetFramework(Framework "v3.5", Full)) |> shouldContain (@"..\Rx-Main\lib\net20\Rx.dll")
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldContain (@"..\Rx-Main\lib\net40\Rx.dll")
    model.GetFiles(DotNetFramework(Framework "v4.0", Full)) |> shouldNotContain (@"..\Rx-Main\lib\net20\Rx.dll")
    model.GetFiles(DotNetFramework(Framework "v4.5", Full)) |> shouldContain (@"..\Rx-Main\lib\net40\Rx.dll")
    model.GetFiles(DotNetFramework(Framework "v4.5.1", Full)) |> shouldContain (@"..\Rx-Main\lib\net40\Rx.dll")