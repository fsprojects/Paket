/// Contains logic which helps to resolve the dependency graph for modules
module Paket.ModuleResolver


// TODO: github has a rate limit - try to convince them to whitelist Paket


let Resolve(getPackages, getSha1, remoteFiles : UnresolvedSourceFile list) : ResolvedSourceFile list = 
    remoteFiles |> List.map (fun file -> 
                       let sha = 
                           match file.Commit with
                           | None -> getSha1 file.Owner file.Project "master"
                           | Some sha -> sha
                       let naked =
                           { Commit = sha
                             Owner = file.Owner
                             Project = file.Project
                             Dependencies = []
                             Name = file.Name }
                       let packages:UnresolvedPackage list = getPackages naked

                       {naked with Dependencies = packages |> List.map (fun p -> p.Name, p.VersionRequirement) })