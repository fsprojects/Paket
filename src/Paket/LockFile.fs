module Paket.LockFile

open System

let formatPackages (dependencies: Map<string*string,Package list>) =
    [for x in dependencies do        
        let name,version = x.Key
        yield sprintf "    %s (%s)" name version
        for d in x.Value do
            yield sprintf "      %s (%s)" d.Name (ConfigHelpers.formatVersionRange d.VersionRange)]

let format dependencies =
    let all =
        ["NUGET"
         "  remote: http://nuget.org/api/v2"
         "  specs:"] @ formatPackages dependencies

    String.Join(Environment.NewLine,all)

let CreateLockFile fileName dependencies =
    IO.File.WriteAllText(fileName, format dependencies)