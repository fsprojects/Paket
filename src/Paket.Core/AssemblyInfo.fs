namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Paket.Core")>]
[<assembly: AssemblyProductAttribute("Paket")>]
[<assembly: AssemblyCompanyAttribute("Paket team")>]
[<assembly: AssemblyDescriptionAttribute("A dependency manager for .NET with support for NuGet packages and git repositories.")>]
[<assembly: AssemblyVersionAttribute("3.1.6")>]
[<assembly: AssemblyFileVersionAttribute("3.1.6")>]
[<assembly: AssemblyInformationalVersionAttribute("3.1.6")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "3.1.6"
    let [<Literal>] InformationalVersion = "3.1.6"
