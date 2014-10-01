module Paket.ProjectFile.InstallationModelpecs

open Paket
open NUnit.Framework
open FsUnit

let KnownDotNetFrameworks = ["1.0";"1.1";"2.0";"3.5";"4.5";"4.5.1"]

type InstallOption = {
    FileName: string
    Framework: FrameworkIdentifier }

let extractFrameworksFromPaths libs =
    libs
    |> List.map (fun lib ->     
            match FrameworkIdentifier.DetectFromPath lib with
            | Some framework -> Some {FileName = lib; Framework = framework}
            | None -> None)            
    |> List.choose id    

let addMissingFrameworks libs =
    let frameworks =
        libs
        |> List.map (fun x -> match x.Framework with | DotNetFramework(Framework v,_) -> Some(v,x.FileName) | _ -> None)
        |> List.choose id

    libs
    |> List.append [
        { FileName = ""; Framework = DotNetFramework(Framework "v3.5",Full)}
        { FileName = ""; Framework = DotNetFramework(Framework "v4.5",Full)}
        { FileName = ""; Framework = DotNetFramework(Framework "v4.5.1",Full)}
          ]
    
[<Test>]
let ``should understand net40 and net45``() =
    let options =
        [@"..\Rx-Main\lib\net40\Rx.dll"; @"..\Rx-Main\lib\net45\Rx.dll"]
        |> extractFrameworksFromPaths
    
    options
    |> List.map (fun x -> x.Framework)
    |> shouldEqual [DotNetFramework(Framework "v4.0",Full); DotNetFramework(Framework "v4.5",Full)]

[<Test>]
let ``should add net35 if we have net20 and net40``() =
    let options =
        [@"..\Rx-Main\lib\net20\Rx.dll"; @"..\Rx-Main\lib\net40\Rx.dll"]
        |> extractFrameworksFromPaths 
        |> addMissingFrameworks

    let frameworks = options|> List.map (fun x -> x.Framework)

    frameworks |> shouldContain (DotNetFramework(Framework "v2.0",Full))
    frameworks |> shouldContain (DotNetFramework(Framework "v3.5",Full))
    frameworks |> shouldContain (DotNetFramework(Framework "v4.0",Full))
    frameworks |> shouldContain (DotNetFramework(Framework "v4.5",Full))
    frameworks |> shouldContain (DotNetFramework(Framework "v4.5.1",Full))