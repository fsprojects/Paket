System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__
#r "../../../bin/Chessie.dll"
#r "../../../bin/Paket.Core.dll"

open System.IO
open Paket
open Paket.Domain
open Paket.LoadingScripts
open Paket.LoadingScripts.ScriptGeneration

let printSqs sqs = sqs |> Seq.iter (printfn "%A")

let lockPath =  __SOURCE_DIRECTORY__ </> @"..\..\..\paket.lock" |> Path.GetFullPath
let dependenciesPath =  __SOURCE_DIRECTORY__ </> @"..\..\..\paket.dependencies" |> Path.GetFullPath
let packageRoot =  __SOURCE_DIRECTORY__ </> @"..\..\..\packages" |> Path.GetFullPath


;;
let depCache = DependencyCache dependenciesPath
;;
//depCache.InstallModels();;
depCache.Nuspecs();;
let scriptContent = constructScriptsFromData depCache [GroupName "Main";GroupName "Build"] [] ["fsx"]
;;
printSqs scriptContent;;
