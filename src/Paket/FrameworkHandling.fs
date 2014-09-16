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

type PlatformVersion = string

type PortableFrameworkProfile = string

/// Framework Identifier type.
type FrameworkIdentifier = 
    | DotNetFramework of FrameworkVersion * FrameworkProfile
    | PortableFramework of PlatformVersion * PortableFrameworkProfile
    | WindowsPhoneApp of string
    | Silverlight of string
    
    member x.GetFrameworkIdentifier() =
        match x with
        | DotNetFramework _ -> "$(TargetFrameworkIdentifier) == '.NETFramework'"
        | PortableFramework _ -> "$(TargetFrameworkIdentifier) == '.NETPortable'"
        | WindowsPhoneApp _ -> "$(TargetFrameworkIdentifier) == 'WindowsPhoneApp'"
        | Silverlight _ -> "$(TargetFrameworkIdentifier) == 'Silverlight'"

    member x.GetFrameworkProfile() =        
        match x with 
        | DotNetFramework(_,Client) -> " And $(TargetFrameworkProfile) == 'Client'" 
        | PortableFramework(_,profile) -> sprintf " And $(TargetFrameworkProfile) == '%s'"  profile
        | _ -> ""

    member x.GetPlatformIdentifier() =        
        match x with 
        | PortableFramework(_,_) -> sprintf " And $(TargetPlatformIdentifier) == Portable'"
        | _ -> ""

    member x.GetPlatformVersion() =        
        match x with 
        | PortableFramework(v,_) -> sprintf " And $(TargetPlatformVersion) == '%s'"  v
        | WindowsPhoneApp v -> sprintf " And $(TargetPlatformVersion) == '%s'"  v
        | _ -> ""

    member x.GetCondition() =
        match x with
        | DotNetFramework(v,_) ->
            match v with
            | Framework fw -> sprintf "%s And $(TargetFrameworkVersion) == '%s'%s" (x.GetFrameworkIdentifier()) fw (x.GetFrameworkProfile())
            | All -> "true"
        | PortableFramework _ -> sprintf "%s%s%s%s" (x.GetFrameworkIdentifier()) (x.GetFrameworkProfile()) (x.GetPlatformIdentifier()) (x.GetPlatformVersion())
        | WindowsPhoneApp _ -> sprintf "%s%s" (x.GetFrameworkIdentifier()) (x.GetPlatformVersion())
        | Silverlight v -> sprintf "%s And $(SilverlightVersion) == '%s'" (x.GetFrameworkIdentifier()) v

    static member DetectFromPath(path : string) : FrameworkIdentifier option = 
        let extract path = 
            match path with
            | "net" -> Some(DotNetFramework(All, Full))
            | "1.0" -> Some(DotNetFramework(All, Full))
            | "1.1" -> Some(DotNetFramework(All, Full))
            | "2.0" -> Some(DotNetFramework(All, Full))
            | "net20" -> Some(DotNetFramework(Framework "v2.0", Full))
            | "net35" -> Some(DotNetFramework(Framework "v3.5", Full))
            | "net4" -> Some(DotNetFramework(Framework "v4.0", Full))
            | "net40" -> Some(DotNetFramework(Framework "v4.0", Full))
            | "net40-full" -> Some(DotNetFramework(Framework "v4.0", Full))
            | "net40-client" -> Some(DotNetFramework(Framework "v4.0", Client))
            | "portable-net4" -> Some(DotNetFramework(Framework "v4.0", Full))
            | "net45" -> Some(DotNetFramework(Framework "v4.5", Full))
            | "net45-full" -> Some(DotNetFramework(Framework "v4.5", Full))
            | "net451" -> Some(DotNetFramework(Framework "v4.5.1", Full))
            | "35" -> Some(DotNetFramework(Framework "v3.5", Full))
            | "40" -> Some(DotNetFramework(Framework "v4.0", Full))
            | "45" -> Some(DotNetFramework(Framework "v4.5", Full))
            | "sl3" -> Some(Silverlight "v3.0")
            | "sl4" -> Some(Silverlight "v4.0")
            | "sl5" -> Some(Silverlight "v5.0")
            | "sl4-wp" -> Some(WindowsPhoneApp "7.1")
            | "sl4-wp71" -> Some(WindowsPhoneApp "7.1")
            | "portable-net4+sl5+wp8+win8+wpa81+monoandroid16+monotouch40" -> Some(PortableFramework("7.0","Profile328"))
            | _ -> None
        
        let path = path.Replace("\\", "/").ToLower()
        let fi = new FileInfo(path)
        
        if path.Contains("lib/" + fi.Name.ToLower()) then Some(DotNetFramework(All, Full))
        else 
            let startPos = path.IndexOf("lib/")
            let endPos = path.IndexOf(fi.Name.ToLower())
            if startPos < 0 || endPos < 0 then None
            else path.Substring(startPos + 4, endPos - startPos - 5) |> extract
