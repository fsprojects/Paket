module Paket.LockFile

open System

let format (resolved:PackageResolution)  =
    // TODO: implement conflict handling
    let sources =
        resolved
        |> Seq.map (fun x ->
            match x.Value with
            | Resolved d -> 
                match d.Referenced.VersionRange with
                | Exactly v -> d.Referenced.Source,d.Referenced.Name,v
            )
        |> Seq.groupBy (fun (s,n,v) -> s)
   
    let all =
        [yield "NUGET"
         for source,packages in sources do
             yield "  remote: " + source
             yield "  specs:"
             for _,name,version in packages do
                 yield sprintf "    %s (%s)" name version]

    String.Join(Environment.NewLine,all)

let CreateLockFile fileName (resolved:PackageResolution) =    
    IO.File.WriteAllText(fileName, format resolved)