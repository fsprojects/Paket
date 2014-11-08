module Paket.FindReferences

open System
open System.IO
open Logging

let FindReferencesForPackage (dependenciesFileName, package:string) =
    let root = Path.GetDirectoryName dependenciesFileName
    let refFiles =
        Directory.GetFiles(root, "paket.references", SearchOption.AllDirectories)
        |> Seq.map ReferencesFile.FromFile
            
    refFiles
    |> Seq.filter (fun r ->
        r.NugetPackages
        |> Seq.exists (fun np -> np.ToLower() = package.ToLower()))
    |> Seq.map (fun r -> r.FileName)
    |> Seq.toList

let FindReferencesFor (dependenciesFileName, packages : string list) =
    let root = Path.GetDirectoryName dependenciesFileName
    let refFiles =
        Directory.GetFiles(root, "paket.references", SearchOption.AllDirectories)
        |> Seq.map ReferencesFile.FromFile
    
    packages
    |> Seq.collect (fun p ->            
            refFiles
            |> Seq.filter (fun r ->
                r.NugetPackages
                |> Seq.exists (fun np -> np.ToLower() = p.ToLower()))
            |> Seq.map (fun r -> (p, r.FileName)))            
    |> Seq.groupBy fst
    |> Seq.toList
    |> List.map (fun (g,values) -> g, Seq.toList values)


let ShowReferencesFor (dependenciesFileName, packages : string list) =
    FindReferencesFor(dependenciesFileName,packages)
    |> Seq.iter (fun (k, vs) ->
        tracefn "%s" k
        vs |> Seq.map snd |> Seq.iter (tracefn "%s")
        
        tracefn "")