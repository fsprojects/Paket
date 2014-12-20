module Paket.DependencyChangeDetection

open Paket.Domain

let findChanges(dependenciesFile:DependenciesFile,lockFile:LockFile) =
    let added =
        [for d in dependenciesFile.DirectDependencies do
            if lockFile.ResolvedPackages.ContainsKey(NormalizedPackageName d.Key) |> not then
                yield d.Key]

    added,[]