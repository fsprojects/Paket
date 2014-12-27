module Paket.DependencyChangeDetection

open Paket.Domain

let findChanges(dependenciesFile:DependenciesFile,lockFile:LockFile) =
    let added =
        [for d in dependenciesFile.DirectDependencies do
            if lockFile.ResolvedPackages.ContainsKey(NormalizedPackageName d.Key) |> not then
                yield d.Key]

    let removed = 
        let direct =
            dependenciesFile.DirectDependencies
            |> Seq.map (fun d -> NormalizedPackageName d.Key)
            |> Set.ofSeq

        [for d in lockFile.GetTopLevelDependencies() do
            if direct.Contains d |> not then
                yield d]

    added,removed