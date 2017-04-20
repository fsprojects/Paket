System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__
#r "../../../bin/Chessie.dll"
#r "../../../bin/Paket.Core.dll"

open System.IO
open Paket
open Paket.Domain
open Paket.LoadingScripts
open Paket.LoadingScripts.ScriptGeneration

let printSqs sqs = sqs |> Seq.iter (printfn "%A")


let paketRoot =
    @"C:\Users\jared\Github\Forks\Paket\integrationtests\scenarios\loading-scripts\fsharpcore\temp\"

let rootInfo = DirectoryInfo paketRoot

let dependenciesPath =  paketRoot </> "paket.dependencies"
let packageRoot =  paketRoot </> "packages"
let loadRoot = paketRoot </> ".paket"</>"load"

let targetFramework = (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_5)

let depCache = DependencyCache dependenciesPath


let refs = depCache.GetOrderedReferences Constants.MainDependencyGroup targetFramework :> seq<_>


;;

let gens = ScriptGeneration.constructScriptsFromData depCache [] ["net46"] ["fsx"] |> List.ofSeq
;;
gens |> Seq.map (fun x -> 
    (Path.Combine(rootInfo.FullName,x.PartialPath)) + "\n\n" +
    (x.Render rootInfo) + "\n\n ---------------- \n\n")
|> printSqs
;;

;;
//printSqs gens
//;;

