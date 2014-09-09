namespace Paket

open System.IO

/// The Framework profile.
type FrameworkProfile =
| Client
| Full

/// The Framework version.
type FrameworkVersion =
| All
| Framework of string

/// Framework Identifier type.
type FrameworkIdentifier =
| DotNetFramework of FrameworkVersion * FrameworkProfile * string option
| WindowsPhoneApp of string
| Silverlight of string
    member x.GetGroup() =
        match x with
        | DotNetFramework _ -> ".NET"  
        | WindowsPhoneApp _ -> "WindowsPhoneApp"
        | Silverlight _ -> "Silverlight"

    static member DetectFromPath(path : string) : FrameworkIdentifier list = 

        let rec mapPath acc parts =
            match parts with
            | [] -> acc
            | path::rest ->
                match path with
                | "net" -> mapPath (DotNetFramework(All,Full,None) :: acc) rest
                | "1.0" -> mapPath (DotNetFramework(All,Full,Some "1.0") :: acc) rest
                | "1.1" -> mapPath (DotNetFramework(All,Full,Some "1.1") :: acc) rest
                | "2.0" -> mapPath (DotNetFramework(All,Full,Some "2.0") :: acc) rest
                | "net20" -> mapPath (DotNetFramework(Framework "v2.0",Full,None) :: acc) rest
                | "net35" -> mapPath (DotNetFramework(Framework "v3.5",Full,None) :: acc) rest
                | "net4" -> mapPath (DotNetFramework(Framework "v4.0",Full,None) :: acc) rest
                | "net40" -> mapPath (DotNetFramework(Framework "v4.0",Full,None) :: acc) rest                
                | "net40-full" -> mapPath (DotNetFramework(Framework "v4.0",Full,None) :: acc) rest
                | "net40-client" -> mapPath (DotNetFramework(Framework "v4.0",Client,None) :: acc) rest
                | "portable-net4" -> mapPath (DotNetFramework(Framework "v4.0",Full,None) :: acc) rest
                | "net45" -> mapPath (DotNetFramework(Framework "v4.5",Full,None) :: acc) rest
                | "net45-full" -> mapPath (DotNetFramework(Framework "v4.5",Full,None) :: acc) rest
                | "net451" -> mapPath (DotNetFramework(Framework "v4.5.1",Full,None) :: acc) rest
                | "sl3" -> mapPath (Silverlight("v3.0") :: acc) rest
                | "sl4" -> mapPath (Silverlight("v4.0") :: acc) rest
                | "sl5" -> mapPath (Silverlight("v5.0") :: acc) rest
                | "sl4-wp" -> mapPath (WindowsPhoneApp("7.1") :: acc) rest
                | "sl4-wp71" -> mapPath (WindowsPhoneApp("7.1") :: acc) rest
                | _ -> mapPath acc rest
               
        let path = path.Replace("\\", "/").ToLower()
        let fi = new FileInfo(path)

        if path.Contains("lib/" + fi.Name.ToLower()) then [DotNetFramework(All,Full,None)] else
        let startPos = path.IndexOf("lib/")
        let endPos = path.IndexOf(fi.Name.ToLower())
        if startPos < 0 || endPos < 0 then [] else
        path.Substring(startPos+4,endPos-startPos-5).Split('+')
        |> Seq.toList
        |> mapPath []
        |> List.rev
        