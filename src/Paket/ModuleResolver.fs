/// Contains logic which helps to resolve the dependency graph for modules
module Paket.ModuleResolver

open System.IO

// Represents details on a dependent source file.
//TODO: As new sources e.g. fssnip etc. are added, this should probably become a DU or perhaps have an enum marker.
type UnresolvedSourceFile =
    { Owner : string
      Project : string
      Name : string      
      Commit : string option }
    member this.FilePath =
        let path = this.Name
                    .TrimStart('/')
                    .Replace("/", Path.DirectorySeparatorChar.ToString())
                    .Replace("\\", Path.DirectorySeparatorChar.ToString())

        let di = DirectoryInfo(Path.Combine("paket-files", this.Owner, this.Project, path))
        di.FullName

    override this.ToString() = 
        match this.Commit with
        | Some commit -> sprintf "%s/%s:%s %s" this.Owner this.Project commit this.Name
        | None -> sprintf "%s/%s %s" this.Owner this.Project this.Name

type ResolvedSourceFile =
    { Owner : string
      Project : string
      Name : string      
      Commit : string
      Dependencies : PackageRequirement list }
    member this.FilePath = this.ComputeFilePath(this.Name)

    member this.ComputeFilePath(name:string) =
        let path = name
                    .TrimStart('/')
                    .Replace("/", Path.DirectorySeparatorChar.ToString())
                    .Replace("\\", Path.DirectorySeparatorChar.ToString())

        let di = DirectoryInfo(Path.Combine("paket-files", this.Owner, this.Project, path))
        di.FullName

    override this.ToString() =  sprintf "%s/%s:%s %s" this.Owner this.Project this.Commit this.Name

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
                       let packages:PackageRequirement list = getPackages naked

                       {naked with Dependencies = packages })