module Paket.LockFile

open System

let formatPackages (packages: (string*string) seq) =
    [for name,version in packages do
        yield sprintf "    %s (%s)" name version]

let format (resolved:PackageResolution)  =
    // TODO: implement conflict handling
    let packages =
        resolved
        |> Seq.map (fun x ->
            match x.Value with
            | Resolved d -> 
                match d.Referenced.VersionRange with
                | Exactly v -> d.Referenced.Name,v
            )
   
    let all =
        ["NUGET"
         "  remote: http://nuget.org/api/v2"
         "  specs:"] @ formatPackages packages

    String.Join(Environment.NewLine,all)

let CreateLockFile fileName (resolved:PackageResolution) =    
    IO.File.WriteAllText(fileName, format resolved)