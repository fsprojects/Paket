module Paket.Constants

open System
open System.IO

[<Literal>]
let DefaultNugetStream = "https://nuget.org/api/v2"

[<Literal>]
let LockFileName = "paket.lock"

[<Literal>]
let DependenciesFileName = "paket.dependencies"

[<Literal>]
let ReferencesFile = "paket.references"

[<Literal>]
let AccessLockFileName = "paket.locked"

[<Literal>]
let PaketFilesFolderName = "paket-files"

[<Literal>]
let PackagesFolderName = "packages"

[<Literal>] 
let SolutionFolderProjectGuid = "2150E333-8FDC-42A3-9474-1A3956D46DE8"

[<Literal>]
let PaketVersionFileName = "paket.version"

[<Literal>]
let TemplateFile = "paket.template"

[<Literal>]
let FullProjectSourceFileName = "FULLPROJECT"

[<Literal>]
let ProjectDefaultNameSpace = "http://schemas.microsoft.com/developer/msbuild/2003"

let AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)

let PaketConfigFolder = Path.Combine(AppDataFolder, "Paket")

let PaketConfigFile = Path.Combine(PaketConfigFolder, "paket.config")

/// The magic unpublished date is 1900-01-01T00:00:00
let MagicUnlistingDate = DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.FromHours(-8.)).DateTime