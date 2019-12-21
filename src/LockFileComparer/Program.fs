open System
open Newtonsoft.Json
open System.IO
open Newtonsoft.Json.Linq
open Paket
open Paket.Domain

let nugetPath = @"D:\temp\saturnblabla\nugety\packages.lock.json"
let paketPath = @"D:\temp\saturnblabla\paket.lock"

let printNuGet () =
    let nugetText = File.ReadAllText nugetPath
    let nugetLock = JObject.Parse nugetText
    let deps = nugetLock.["dependencies"] :?> JObject
    let netcore3 = deps.[".NETCoreApp,Version=v3.0"] :?> JObject
    netcore3.Properties()
    |> Seq.map (fun p ->
        let prop = p.Value :?> JObject
        let v = prop.["resolved"].ToString()
        let v = if v.EndsWith (".0") then v.Substring(0,v.Length-2) else v
        p.Name,v)
    |> Seq.sortBy fst
    |> Seq.iter (fun (n,v) -> printfn "%s, %s" n v)


let printPaket() =
    let paketLock = LockFile.LoadFrom paketPath
    paketLock.Groups.[GroupName "Main"].Resolution
    |> Seq.map (fun r -> r.Key.Name,r.Value.Version.ToString())
    |> Seq.sortBy fst
    |> Seq.iter (fun (n,v) -> printfn "%s, %s" n v)

[<EntryPoint>]
let main argv =
    printNuGet ()
    0
