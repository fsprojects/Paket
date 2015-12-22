namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Paket.PowerShell")>]
[<assembly: AssemblyProductAttribute("Paket")>]
[<assembly: AssemblyCompanyAttribute("Paket team")>]
[<assembly: AssemblyDescriptionAttribute("A package dependency manager for .NET with support for NuGet packages and GitHub repositories.")>]
[<assembly: AssemblyVersionAttribute("2.37.4")>]
[<assembly: AssemblyFileVersionAttribute("2.37.4")>]
[<assembly: AssemblyInformationalVersionAttribute("2.37.4")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "2.37.4"
