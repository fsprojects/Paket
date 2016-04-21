#load "Scripts/load-project-debug.fsx"
open System
open System.Collections.Generic
open System.IO
open System.Linq
open Mono.Cecil
open QuickGraph

let rootFolder = (@"C:\dev\src\projects\github.com\fsprojects\Paket.VisualStudio") |> DirectoryInfo
//let rootFolder = (__SOURCE_DIRECTORY__) |> DirectoryInfo

[
  Paket.LoadingScripts.ScriptGeneration.CSharp
  Paket.LoadingScripts.ScriptGeneration.FSharp
]
|> Seq.iter (fun t -> Paket.LoadingScripts.ScriptGeneration.generateScriptsForRootFolder t (Paket.FrameworkIdentifier.DotNetFramework Paket.FrameworkVersion.V4_5) rootFolder)
