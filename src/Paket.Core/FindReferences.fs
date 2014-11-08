module Paket.FindReferences

open System
open System.IO
open Logging

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
                |> Seq.filter (fun np -> np.Equals(p))
                |> Seq.isEmpty |> not)
            |> Seq.map (fun r -> (p, r.FileName)))
    |> Seq.groupBy fst


let ShowReferencesFor (dependenciesFileName, packages : string list) =
    FindReferencesFor(dependenciesFileName,packages)
    |> Seq.iter (fun (k, vs) ->
        tracefn "%s" k
        vs |> Seq.map snd |> Seq.iter (tracefn "%s")
        
        tracefn "")