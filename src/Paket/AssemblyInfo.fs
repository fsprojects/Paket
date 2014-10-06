namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Paket")>]
[<assembly: AssemblyProductAttribute("Paket")>]
[<assembly: AssemblyDescriptionAttribute("A package dependency manager for .NET with support for NuGet packages and GitHub files.")>]
[<assembly: AssemblyVersionAttribute("0.4.25")>]
[<assembly: AssemblyFileVersionAttribute("0.4.25")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.4.25"
