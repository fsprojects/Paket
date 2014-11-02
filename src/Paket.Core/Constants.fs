module Paket.Constants

open System
open System.IO

[<Literal>]
let DefaultNugetStream = "https://nuget.org/api/v2"

[<Literal>]
let DependenciesFile = "paket.dependencies"

[<Literal>]
let ReferencesFile = "paket.references"

[<Literal>]
let ProjectDefaultNameSpace = "http://schemas.microsoft.com/developer/msbuild/2003"

let AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)

let PaketConfigFolder = Path.Combine(AppDataFolder, "Paket")