module Paket.EmitFsx

open Paket
open System
open System.IO
open Paket.Logging

let PrintFsx projectFile properties =
    let p = ProjectParser.FSharpProjectFileInfo.Parse(projectFile, properties)

    let assemblies =
      [| match p.OutputFile with Some f -> yield f | None -> ()
         for ref in p.References do yield ref |]

    printfn "References: %A" assemblies

    printfn "Dir: %s" p.Directory

    let dir = Environment.CurrentDirectory
    Environment.CurrentDirectory <- p.Directory

    let faw = DependencyOrdering.ForeignAidWorker()


    printfn "Script includes:\n%s" (faw.Work(assemblies))
    Environment.CurrentDirectory <- dir
