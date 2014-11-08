module Paket.FindReferences

open System
open System.IO
open Logging

let For (packages : list<string>) =
    let refFiles =
        Directory.GetFiles(".", "paket.references", SearchOption.AllDirectories)
        |> Seq.map ReferencesFile.FromFile

    let packagesAndTheirRefFiles =
        packages
        |> Seq.collect (fun p ->            
                refFiles
                |> Seq.filter (fun r ->
                    r.NugetPackages
                    |> Seq.filter (fun np -> np.Equals(p))
                    |> Seq.isEmpty |> not)
                |> Seq.map (fun r -> (p, r.FileName)))
        |> Seq.groupBy fst
        
    packagesAndTheirRefFiles
    |> Seq.iter (fun (k, vs) ->
        tracefn "%s" k
        vs |> Seq.map snd |> Seq.iter (fun v -> tracefn "%s" v)
        
        tracefn "")

