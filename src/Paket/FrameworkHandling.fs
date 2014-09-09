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
    | DotNetFramework of FrameworkVersion * FrameworkProfile
    | WindowsPhoneApp of string
    | Silverlight of string
    
    member x.GetGroupCondition() =
        match x with
        | DotNetFramework _ -> "$(TargetFrameworkIdentifier) == '.NETFramework'"
        | WindowsPhoneApp _ -> "$(TargetFrameworkIdentifier) == 'WindowsPhoneApp'"
        | Silverlight _ -> "$(TargetFrameworkIdentifier) == 'Silverlight'"

    member x.GetFrameworkProfile() =        
        match x with 
        | DotNetFramework(_,Client) -> " And $(TargetFrameworkProfile) == 'Client'" 
        | _ -> ""

    member x.GetCondition() =
        match x with
        | DotNetFramework(v,_) ->
            match v with
            | Framework fw -> sprintf "$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == '%s'%s" fw (x.GetFrameworkProfile())
            | All -> "true"
        | WindowsPhoneApp v -> sprintf "$(TargetFrameworkIdentifier) == 'WindowsPhoneApp' And $(TargetPlatformVersion) == '%s'" v
        | Silverlight v -> sprintf "$(TargetFrameworkIdentifier) == 'Silverlight' And $(SilverlightVersion) == '%s'" v

    static member DetectFromPath(path : string) : FrameworkIdentifier list = 
        let extract parts = 
            [ for path in parts do
                  match path with
                  | "net" -> yield DotNetFramework(All, Full)
                  | "1.0" -> yield DotNetFramework(All, Full)
                  | "1.1" -> yield DotNetFramework(All, Full)
                  | "2.0" -> yield DotNetFramework(All, Full)
                  | "net20" -> yield DotNetFramework(Framework "v2.0", Full)
                  | "net35" -> yield DotNetFramework(Framework "v3.5", Full)
                  | "net4" -> yield DotNetFramework(Framework "v4.0", Full)
                  | "net40" -> yield DotNetFramework(Framework "v4.0", Full)
                  | "net40-full" -> yield DotNetFramework(Framework "v4.0", Full)
                  | "net40-client" -> yield DotNetFramework(Framework "v4.0", Client)
                  | "portable-net4" -> yield DotNetFramework(Framework "v4.0", Full)
                  | "net45" -> yield DotNetFramework(Framework "v4.5", Full)
                  | "net45-full" -> yield DotNetFramework(Framework "v4.5", Full)
                  | "net451" -> yield DotNetFramework(Framework "v4.5.1", Full)
                  | "sl3" -> yield Silverlight "v3.0"
                  | "sl4" -> yield Silverlight "v4.0"
                  | "sl5" -> yield Silverlight "v5.0"
                  | "sl4-wp" -> yield WindowsPhoneApp "7.1"
                  | "sl4-wp71" -> yield WindowsPhoneApp "7.1"
                  | _ -> () ]
        
        let path = path.Replace("\\", "/").ToLower()
        let fi = new FileInfo(path)
        if path.Contains("lib/" + fi.Name.ToLower()) then [ DotNetFramework(All, Full) ]
        else 
            let startPos = path.IndexOf("lib/")
            let endPos = path.IndexOf(fi.Name.ToLower())
            if startPos < 0 || endPos < 0 then []
            else path.Substring(startPos + 4, endPos - startPos - 5).Split('+') |> extract
