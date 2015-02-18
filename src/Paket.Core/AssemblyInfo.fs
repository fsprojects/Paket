namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Paket.Core")>]
[<assembly: AssemblyProductAttribute("Paket")>]
[<assembly: AssemblyDescriptionAttribute("A package dependency manager for .NET with support for NuGet packages and GitHub repositories.")>]
[<assembly: AssemblyVersionAttribute("0.28.11")>]
[<assembly: AssemblyFileVersionAttribute("0.28.11")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.28.11"
