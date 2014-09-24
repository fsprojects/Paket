module Paket.Constants

[<Literal>]
let internal DefaultNugetStream = "http://nuget.org/api/v2"

let DefaultNugetSource = PackageSource.NugetSource DefaultNugetStream

[<Literal>]
let DependenciesFile = "paket.dependencies"

[<Literal>]
let ReferencesFile = "paket.references"