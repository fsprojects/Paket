namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Paket")>]
[<assembly: AssemblyProductAttribute("Paket")>]
[<assembly: AssemblyCompanyAttribute("Paket team")>]
[<assembly: AssemblyDescriptionAttribute("A dependency manager for .NET with support for NuGet packages and git repositories.")>]
[<assembly: AssemblyVersionAttribute("3.19.3")>]
[<assembly: AssemblyFileVersionAttribute("3.19.3")>]
[<assembly: AssemblyInformationalVersionAttribute("3.19.3")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "3.19.3"
    let [<Literal>] InformationalVersion = "3.19.3"
