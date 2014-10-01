module Paket.ProjectFile.InstallationModelpecs

open Paket
open NUnit.Framework
open FsUnit

let KnownDotNetFrameworks = [ "v1.0"; "v1.1"; "v2.0"; "v3.5"; "v4.0"; "v4.5"; "v4.5.1" ]

type InstallOption = 
    { Files : string list
      Framework : FrameworkIdentifier }

type InstallModell = 
    { Frameworks : Map<FrameworkIdentifier, string list> }
    static member EmptyModel : InstallModell = 
        let frameworks = 
            [ for x in KnownDotNetFrameworks -> DotNetFramework(Framework x, Full) ]
        { Frameworks = List.fold (fun map f -> Map.add f [] map) Map.empty frameworks }
    member this.GetFrameworks() = this.Frameworks |> Seq.map (fun kv -> kv.Key)

let addToModel framework lib (model : InstallModell) : InstallModell = 
    { model with Frameworks = 
                     match Map.tryFind framework model.Frameworks with
                     | Some files -> Map.add framework (lib :: files) model.Frameworks
                     | None -> Map.add framework [ lib ] model.Frameworks }

let extractFrameworksFromPaths (model : InstallModell) libs : InstallModell = 
    libs |> List.fold (fun model lib -> 
                match FrameworkIdentifier.DetectFromPath lib with
                | Some framework -> addToModel framework lib model
                | None -> model) model

let addMissingFrameworks (model : InstallModell) = 
    //    let grouped =
    //        model
    //        |> Seq.map (fun x -> match x.Framework with | DotNetFramework(Framework v,_) -> Some(x.FileName,v,x) | _ -> None)
    //        |> Seq.choose id
    //        |> Seq.groupBy (fun (n,_,_) -> n)
    //
    //    for dll,framweworks in grouped do
    //        let framweworks = framweworks |> Set.ofSeq
    //        for currentVersion in KnownDotNetFrameworks do
    //            framweworks 
    //                |> 
    model

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
    model.GetFrameworks() |> shouldContain (DotNetFramework(Framework "v4.0", Full))
    model.GetFrameworks() |> shouldContain (DotNetFramework(Framework "v4.5", Full))

[<Test>]
let ``should add net35 if we have net20 and net40``() = 
    let model = 
        [ @"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\Rx.dll" ]
        |> extractFrameworksFromPaths InstallModell.EmptyModel
        |> addMissingFrameworks
    model.GetFrameworks() |> shouldContain (DotNetFramework(Framework "v2.0", Full))
    model.GetFrameworks() |> shouldContain (DotNetFramework(Framework "v3.5", Full))
    model.GetFrameworks() |> shouldContain (DotNetFramework(Framework "v4.0", Full))
    model.GetFrameworks() |> shouldContain (DotNetFramework(Framework "v4.5", Full))
    model.GetFrameworks() |> shouldContain (DotNetFramework(Framework "v4.5.1", Full))
