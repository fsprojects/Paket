#load "Scripts/load-project-debug.fsx"
open System
open System.Collections.Generic
open System.IO
open System.Linq
open Mono.Cecil
open QuickGraph

let rootFolder = (@"C:\dev\src\g\fiddle3d") |> DirectoryInfo
//let rootFolder = (__SOURCE_DIRECTORY__) |> DirectoryInfo

[
  Paket.LoadingScripts.ScriptGeneratingModule.CSharp
  Paket.LoadingScripts.ScriptGeneratingModule.FSharp
]
|> Seq.iter (fun t -> Paket.LoadingScripts.ScriptGeneratingModule.generateScriptsForRootFolder t (Paket.FrameworkIdentifier.DotNetFramework Paket.FrameworkVersion.V4_5) rootFolder)
