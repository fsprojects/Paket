module Paket.ProjectFile.InstallationModelpecs

open Paket
open NUnit.Framework
open FsUnit

let KnownDotNetFrameworks = ["1.0";"1.1";"2.0";"3.5";"4.5";"4.5.1"]

type InstallOption = {
    FileName: string
    Framework: FrameworkIdentifier }

type InstallModell = InstallOption Set

let getFrameworks model = model |> Set.map (fun x -> x.Framework)


let extractFrameworksFromPaths libs : InstallModell =
    libs
    |> List.map (fun lib ->     
            match FrameworkIdentifier.DetectFromPath lib with
            | Some framework -> Some {FileName = lib; Framework = framework}
            | None -> None)            
    |> List.choose id
    |> Set.ofList

let addMissingFrameworks (model:InstallModell) =
    let frameworks =
        model
        |> Seq.map (fun x -> match x.Framework with | DotNetFramework(Framework v,_) -> Some(v,x.FileName) | _ -> None)
        |> Seq.choose id
        |> Set.ofSeq

    model
    |> Set.add { FileName = ""; Framework = DotNetFramework(Framework "v3.5",Full)}
    |> Set.add { FileName = ""; Framework = DotNetFramework(Framework "v4.5",Full)}
    |> Set.add { FileName = ""; Framework = DotNetFramework(Framework "v4.5.1",Full)}          
    
[<Test>]
let ``should understand net40 and net45``() =
    let model =
        [@"..\Rx-Main\lib\net40\Rx.dll"; @"..\Rx-Main\lib\net45\Rx.dll"]
        |> extractFrameworksFromPaths
    
    getFrameworks model |> shouldContain (DotNetFramework(Framework "v4.0",Full))
    getFrameworks model |> shouldContain (DotNetFramework(Framework "v4.5",Full))

[<Test>]
let ``should add net35 if we have net20 and net40``() =
    let model =
        [@"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\Rx.dll"]
        |> extractFrameworksFromPaths 
        |> addMissingFrameworks

    getFrameworks model |> shouldContain (DotNetFramework(Framework "v2.0",Full))
    getFrameworks model |> shouldContain (DotNetFramework(Framework "v3.5",Full))
    getFrameworks model |> shouldContain (DotNetFramework(Framework "v4.0",Full))
    getFrameworks model |> shouldContain (DotNetFramework(Framework "v4.5",Full))
    getFrameworks model |> shouldContain (DotNetFramework(Framework "v4.5.1",Full))