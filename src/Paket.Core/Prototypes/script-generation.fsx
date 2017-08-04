System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__
#r "../../../bin/Chessie.dll"
#r "../../../bin/Paket.Core.dll"
#r "../../../packages/build/FAKE/tools/FakeLib.dll"
#r "../../../packages/build/Pri.LongPath/lib/net45/Pri.LongPath.dll"
open System.IO
open Pri.LongPath
open Fake
open Paket
open Paket.Domain
open Paket.LoadingScripts
open Paket.LoadingScripts.ScriptGeneration


let _ = Logging.event.Publish |> Observable.subscribe Logging.traceToConsole

let printSqs sqs = sqs |> Seq.iter (printfn "%A")

let scenarios = System.Collections.Generic.List<_>()

let integrationTestPath = Path.GetFullPath(__SOURCE_DIRECTORY__ + "../../../../integrationtests/scenarios/loading-scripts")
let scenarioTempPath scenario = Path.Combine(integrationTestPath,scenario,"temp")
let originalScenarioPath scenario = Path.Combine(integrationTestPath,scenario,"before")


let prepare scenario =
    
    scenarios.Add scenario
    let originalScenarioPath = originalScenarioPath scenario
    let scenarioPath = scenarioTempPath scenario
    CleanDir scenarioPath
    CopyDir scenarioPath originalScenarioPath (fun _ -> true)
    Directory.GetFiles(scenarioPath, "*.fsprojtemplate", SearchOption.AllDirectories)
    |> Seq.iter (fun f -> File.Move(f, Path.ChangeExtension(f, "fsproj")))
    Directory.GetFiles(scenarioPath, "*.csprojtemplate", SearchOption.AllDirectories)
    |> Seq.iter (fun f -> File.Move(f, Path.ChangeExtension(f, "csproj")))
    Directory.GetFiles(scenarioPath, "*.vcxprojtemplate", SearchOption.AllDirectories)
    |> Seq.iter (fun f -> File.Move(f, Path.ChangeExtension(f, "vcxproj")))
    Directory.GetFiles(scenarioPath, "*.templatetemplate", SearchOption.AllDirectories)
    |> Seq.iter (fun f -> File.Move(f, Path.ChangeExtension(f, "template")))
    Directory.GetFiles(scenarioPath, "*.jsontemplate", SearchOption.AllDirectories)
    |> Seq.iter (fun f -> File.Move(f, Path.ChangeExtension(f, "json")))




let targetFramework = (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_5)

let execBase = false

if execBase then
    let projectRoot = __SOURCE_DIRECTORY__ +  "../../../../"
    let projectDependencies = projectRoot </> "paket.dependencies"
    Logging.verbose <- true
    let projectCache = DependencyCache projectDependencies

    let projectRefs = projectCache.GetOrderedReferences Constants.MainDependencyGroup targetFramework
    let projectGens = ScriptGeneration.constructScriptsFromData projectCache [] [] ["fsx"] |> List.ofSeq
    ()
;;
let scenario = "fsharpcore"

let paketRoot =
    Path.Combine(__SOURCE_DIRECTORY__,sprintf "../../../integrationtests/scenarios/loading-scripts/%s/temp" scenario)
    |> Path.GetFullPath

printfn  "%s" paketRoot
let paketDependencies = paketRoot </> "paket.dependencies"

prepare scenario
;;
let text = File.ReadAllText paketDependencies
Dependencies.Install (text, paketRoot)
;;
let rootInfo = DirectoryInfo paketRoot

let packageRoot =  paketRoot </> "packages"
let loadRoot = paketRoot </> ".paket"</>"load"
let loadRootInfo = DirectoryInfo loadRoot

let depCache = DependencyCache  paketDependencies 

//let refs = depCache.GetOrderedReferences Constants.MainDependencyGroup targetFramework :> seq<_>

let depUtil = Dependencies.Locate paketDependencies
;;
Logging.verbose <- true
let rawgens = ScriptGeneration.constructScriptsFromData depCache [] ["net46"] ["fsx"] |> List.ofSeq
;;
//let gens = depUtil.GenerateLoadScriptData paketDependencies [] ["net46"] ["fsx"] |> List.ofSeq
;;


