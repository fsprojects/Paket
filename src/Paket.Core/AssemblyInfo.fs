namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Paket.Core")>]
[<assembly: AssemblyProductAttribute("Paket")>]
[<assembly: AssemblyDescriptionAttribute("A package dependency manager for .NET with support for NuGet packages and GitHub files.")>]
[<assembly: AssemblyVersionAttribute("0.5.13")>]
[<assembly: AssemblyFileVersionAttribute("0.5.13")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.5.13"
