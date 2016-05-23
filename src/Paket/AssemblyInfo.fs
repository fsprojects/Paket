namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Paket")>]
[<assembly: AssemblyProductAttribute("Paket")>]
[<assembly: AssemblyCompanyAttribute("Paket team")>]
[<assembly: AssemblyDescriptionAttribute("A dependency manager for .NET with support for NuGet packages and git repositories.")>]
[<assembly: AssemblyVersionAttribute("2.66.2")>]
[<assembly: AssemblyFileVersionAttribute("2.66.2")>]
[<assembly: AssemblyInformationalVersionAttribute("2.66.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "2.66.2"
    let [<Literal>] InformationalVersion = "2.66.2"
