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
        let extract parts = 
            [ for path in parts do
                  match path with
                  | "net" -> yield DotNetFramework(All, Full, None)
                  | "1.0" -> yield DotNetFramework(All, Full, Some "1.0")
                  | "1.1" -> yield DotNetFramework(All, Full, Some "1.1")
                  | "2.0" -> yield DotNetFramework(All, Full, Some "2.0")
                  | "net20" -> yield DotNetFramework(Framework "v2.0", Full, None)
                  | "net35" -> yield DotNetFramework(Framework "v3.5", Full, None)
                  | "net4" -> yield DotNetFramework(Framework "v4.0", Full, None)
                  | "net40" -> yield DotNetFramework(Framework "v4.0", Full, None)
                  | "net40-full" -> yield DotNetFramework(Framework "v4.0", Full, None)
                  | "net40-client" -> yield DotNetFramework(Framework "v4.0", Client, None)
                  | "portable-net4" -> yield DotNetFramework(Framework "v4.0", Full, None)
                  | "net45" -> yield DotNetFramework(Framework "v4.5", Full, None)
                  | "net45-full" -> yield DotNetFramework(Framework "v4.5", Full, None)
                  | "net451" -> yield DotNetFramework(Framework "v4.5.1", Full, None)
                  | "sl3" -> yield Silverlight "v3.0"
                  | "sl4" -> yield Silverlight "v4.0"
                  | "sl5" -> yield Silverlight "v5.0"
                  | "sl4-wp" -> yield WindowsPhoneApp "7.1"
                  | "sl4-wp71" -> yield WindowsPhoneApp "7.1"
                  | _ -> () ]
        
        let path = path.Replace("\\", "/").ToLower()
        let fi = new FileInfo(path)
        if path.Contains("lib/" + fi.Name.ToLower()) then [ DotNetFramework(All, Full, None) ]
        else 
            let startPos = path.IndexOf("lib/")
            let endPos = path.IndexOf(fi.Name.ToLower())
            if startPos < 0 || endPos < 0 then []
            else path.Substring(startPos + 4, endPos - startPos - 5).Split('+') |> extract
