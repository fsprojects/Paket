namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Paket.PowerShell")>]
[<assembly: AssemblyProductAttribute("Paket")>]
[<assembly: AssemblyCompanyAttribute("Paket team")>]
[<assembly: AssemblyDescriptionAttribute("A package dependency manager for .NET with support for NuGet packages and GitHub repositories.")>]
[<assembly: AssemblyVersionAttribute("1.39.8")>]
[<assembly: AssemblyFileVersionAttribute("1.39.8")>]
[<assembly: AssemblyInformationalVersionAttribute("1.39.8")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.39.8"
