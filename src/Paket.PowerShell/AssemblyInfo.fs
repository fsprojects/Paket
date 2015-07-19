namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Paket.PowerShell")>]
[<assembly: AssemblyProductAttribute("Paket")>]
[<assembly: AssemblyCompanyAttribute("Paket team")>]
[<assembly: AssemblyDescriptionAttribute("A package dependency manager for .NET with support for NuGet packages and GitHub repositories.")>]
[<assembly: AssemblyVersionAttribute("1.19.7")>]
[<assembly: AssemblyFileVersionAttribute("1.19.7")>]
[<assembly: AssemblyInformationalVersionAttribute("1.19.7")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.19.7"
