namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Paket.Core")>]
[<assembly: AssemblyProductAttribute("Paket")>]
[<assembly: AssemblyCompanyAttribute("Paket team")>]
[<assembly: AssemblyDescriptionAttribute("A package dependency manager for .NET with support for NuGet packages and GitHub repositories.")>]
[<assembly: AssemblyVersionAttribute("2.49.4")>]
[<assembly: AssemblyFileVersionAttribute("2.49.4")>]
[<assembly: AssemblyInformationalVersionAttribute("2.49.4")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "2.49.4"
