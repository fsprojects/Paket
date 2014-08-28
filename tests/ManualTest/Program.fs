open Paket

let completeConfig = Config.ReadFromFile "myConfig.fsx"

let resolution = completeConfig.Resolve(Nuget.NugetDiscovery())

for x in resolution.ResolvedVersionMap do
    printfn "%A" x.Value

printfn ""

for x in resolution.DirectDependencies do
    let name,version = x.Key
    printfn "%s (%s)" name version
    for d in x.Value do
        printfn "  %s (%s)" d.Name (ConfigHelpers.formatVersionRange d.VersionRange)


System.Console.ReadKey() |> ignore
