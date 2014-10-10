namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Paket.Core")>]
[<assembly: AssemblyProductAttribute("Paket")>]
[<assembly: AssemblyDescriptionAttribute("A package dependency manager for .NET with support for NuGet packages and GitHub files.")>]
[<assembly: AssemblyVersionAttribute("0.5.2")>]
[<assembly: AssemblyFileVersionAttribute("0.5.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.5.2"
