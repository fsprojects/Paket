namespace Paket

open System.IO
open System

[<RequireQualifiedAccess>]
/// The Framework version.
type FrameworkVersion = 
    | V1
    | V1_1
    | V2
    | V3_5
    | V4_Client
    | V4
    | V4_5
    | V4_5_1
    | V4_5_2
    | V4_5_3
    override this.ToString() =
        match this with
        | V1 -> "v1.0"
        | V1_1 -> "v1.1"
        | V2 -> "v2.0"
        | V3_5 -> "v3.5"
        | V4_Client -> "v4.0"
        | V4 -> "v4.0"
        | V4_5 -> "v4.5"
        | V4_5_1 -> "v4.5.1"
        | V4_5_2 -> "v4.5.2"
        | V4_5_3 -> "v4.5.3"

    static member KnownDotNetFrameworks = 
        [ FrameworkVersion.V1
          FrameworkVersion.V1_1
          FrameworkVersion.V2
          FrameworkVersion.V3_5
          FrameworkVersion.V4_Client
          FrameworkVersion.V4
          FrameworkVersion.V4_5
          FrameworkVersion.V4_5_1
          FrameworkVersion.V4_5_2
          FrameworkVersion.V4_5_3 ]

type PlatformVersion = string

type PortableFrameworkProfile = string

/// Framework Identifier type.
type FrameworkIdentifier = 
    | DotNetFramework of FrameworkVersion
    | PortableFramework of PlatformVersion * PortableFrameworkProfile
    | MonoAndroid
    | MonoTouch
    | Windows of string
    | WindowsPhoneApp of string
    | Silverlight of string

    static member KnownAliases =
        ["NET", "net"
         ".NET", "net"
         "NETFramework", "net"
         ".NETFramework", "net"
         "NETCore", "netcore"
         ".NETCore", "netcore"
         "WinRT", "netcore"
         ".NETMicroFramework", "netmf"
         "SL", "sl"
         "Silverlight", "sl"
         ".NETPortable", "portable-"
         "NETPortable", "portable-"
         "WindowsPhone", "wp"
         "Windows", "win"
         "WindowsPhoneApp", "wpa"
         
         "1.0", "10" 
         "1.1", "11" 
         "2.0", "20" 
         "3.5", "35" 
         "4.0", "40" 
         "4.5", "45" 
         "5.0", "50" 
         "0.0", "" ]

    static member Extract(path:string) = 
        let path = 
            FrameworkIdentifier.KnownAliases
            |> List.fold (fun (path:string) (pattern,replacement) -> path.Replace(pattern.ToLower(),replacement.ToLower())) (path.ToLower())

        match path with
        | "net" -> Some(DotNetFramework(FrameworkVersion.V2)) // not sure here
        | "10" -> Some(DotNetFramework(FrameworkVersion.V1))
        | "11" -> Some(DotNetFramework(FrameworkVersion.V1_1))
        | "20" -> Some(DotNetFramework(FrameworkVersion.V2))
        | "net11" -> Some(DotNetFramework(FrameworkVersion.V1_1))
        | "net20" -> Some(DotNetFramework(FrameworkVersion.V2))
        | "net20-full" -> Some(DotNetFramework(FrameworkVersion.V2))
        | "net35" -> Some(DotNetFramework(FrameworkVersion.V3_5))
        | "net35-full" -> Some(DotNetFramework(FrameworkVersion.V3_5))
        | "net4" -> Some(DotNetFramework(FrameworkVersion.V4_Client))
        | "net40" -> Some(DotNetFramework(FrameworkVersion.V4_Client))
        | "net40-full" -> Some(DotNetFramework(FrameworkVersion.V4))
        | "net40-client" -> Some(DotNetFramework(FrameworkVersion.V4_Client))
        | "net45" -> Some(DotNetFramework(FrameworkVersion.V4_5))
        | "net45-full" -> Some(DotNetFramework(FrameworkVersion.V4_5))
        | "net451" -> Some(DotNetFramework(FrameworkVersion.V4_5_1))
        | "net452" -> Some(DotNetFramework(FrameworkVersion.V4_5_2))
        | "net453" -> Some(DotNetFramework(FrameworkVersion.V4_5_3))
        | "35" -> Some(DotNetFramework(FrameworkVersion.V3_5))
        | "40" -> Some(DotNetFramework(FrameworkVersion.V4))
        | "45" -> Some(DotNetFramework(FrameworkVersion.V4_5))
        | "sl3" -> Some(Silverlight "v3.0")
        | "sl4" -> Some(Silverlight "v4.0")
        | "sl5" -> Some(Silverlight "v5.0")
        | "sl50" -> Some(Silverlight "v5.0")
        | "sl4-wp" -> Some(WindowsPhoneApp "7.1")
        | "sl4-wp71" -> Some(WindowsPhoneApp "7.1")
        | "sl4-windowsphone71" -> Some(WindowsPhoneApp "7.1")
        | "win8" -> Some(Windows "v8.0")
        | "netcore45" -> Some(Windows "v8.0")
        | "wp8" -> Some(WindowsPhoneApp "v8.0")
        | "wpa81" -> Some(WindowsPhoneApp "v8.1")
        | "monoandroid" -> Some(MonoAndroid)
        | "monoandroid10" -> Some(MonoAndroid)
        | "monotouch" -> Some(MonoTouch)
        | "monotouch10" -> Some(MonoTouch)
        | _ ->                         
            if path.ToLower().StartsWith("portable-") then
                Some(PortableFramework("7.0", path.ToLower().Replace("portable-","")))
            else None
    
    member x.Group =
        let group =
            match x with
            | DotNetFramework _ -> ".NETFramework"
            | PortableFramework _ -> ".NETPortable"
            | WindowsPhoneApp _ -> "WindowsPhoneApp"
            | Windows _ -> "Windows"
            | Silverlight _ -> "Silverlight"
            | MonoAndroid -> "MonoAndroid"
            | MonoTouch -> "MonoTouch"

        sprintf "$(TargetFrameworkIdentifier) == '%s'" group

    member x.GetFrameworkIdentifier() = x.Group
    member x.GetPortableProfile() =
        match x with 
        | PortableFramework(_,profile) -> 
            let profileMapping = 
                [ "Profile2", "net4+sl4+netcore45+wp7"
                  "Profile3", "net4+sl4"
                  "Profile4", "net45+sl4+netcore45+wp7"
                  "Profile5", "net4+netcore45+MonoAndroid1+MonoTouch1"
                  "Profile6", "net403+netcore45+MonoAndroid1+MonoTouch1"
                  "Profile7", "net45+netcore45+MonoAndroid1+MonoTouch1"
                  "Profile14", "net4+sl5+MonoAndroid1+MonoTouch1"
                  "Profile18", "net403+sl4"
                  "Profile19", "net403+sl5+MonoAndroid1+MonoTouch1"
                  "Profile23", "net45+sl4"
                  "Profile24", "net45+sl5+MonoAndroid1+MonoTouch1"
                  "Profile31", "netcore451+wp81"
                  "Profile32", "netcore451+wpa81"
                  "Profile32", "win81+wpa81"
                  "Profile36", "net4+sl4+netcore45+wp8"
                  "Profile37", "net4+sl5+netcore45+MonoAndroid1+MonoTouch1"
                  "Profile41", "net403+sl4+netcore45"
                  "Profile42", "net403+sl5+netcore45+MonoAndroid1+MonoTouch1"
                  "Profile44", "net451+netcore451"
                  "Profile46", "net45+sl4+netcore45"
                  "Profile47", "net45+sl5+netcore45+MonoAndroid1+MonoTouch1"
                  "Profile49", "net45+wp8+MonoAndroid1+MonoTouch1"
                  "Profile78", "net45+netcore45+wp8+MonoAndroid1+MonoTouch1"
                  "Profile84", "wpa81+wp81"
                  "Profile88", "net4+sl4+netcore45+wp71"
                  "Profile92", "net4+netcore45+wpa81+MonoAndroid1+MonoTouch1"
                  "Profile95", "net403+sl4+netcore45+wp7"
                  "Profile96", "net403+sl4+netcore45+wp71"
                  "Profile102", "net403+netcore45+wpa81+MonoAndroid1+MonoTouch1"
                  "Profile104", "net45+sl4+netcore45+wp71"
                  "Profile111", "net45+netcore45+wpa81+MonoAndroid1+MonoTouch1"
                  "Profile136", "net4+sl5+netcore45+wp8+MonoAndroid1+MonoTouch1"
                  "Profile136", "net4+sl5+wp8+win8+wpa81+MonoTouch+MonoAndroid"
                  "Profile136", "net40+sl5+wp80+win8+wpa81"
                  "Profile143", "net403+sl4+netcore45+wp8"
                  "Profile147", "net403+sl5+netcore45+wp8+MonoAndroid1+MonoTouch1"
                  "Profile151", "net451+netcore451+wpa81"
                  "Profile154", "net45+sl4+netcore45+wp8"
                  "Profile157", "netcore451+wpa81+wp81"
                  "Profile158", "net45+sl5+netcore45+wp8+MonoAndroid1+MonoTouch1"
                  "Profile225", "net4+sl5+netcore45+wpa81+MonoAndroid1+MonoTouch1"                  
                  "Profile240", "net403+sl5+netcore45+wpa81"
                  "Profile255", "net45+sl5+netcore45+wpa81+MonoAndroid1+MonoTouch1"
                  "Profile259", "net45+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1"
                  "Profile259", "net45+win8+wp8+wpa81"
                  "Profile328", "net4+sl5+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1"
                  "Profile328", "net4+sl5+wp8+win8+wpa81+monoandroid16+monotouch40"
                  "Profile336", "net403+sl5+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1"
                  "Profile344", "net45+sl5+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1"

                  // unsure
                  "Profile88", "net40+sl4+win8+wp71+wpa81"
                  "Profile84", "net45+wp80+win8+wpa81" ]
            
            
            match profileMapping |> Seq.tryFind (fun (_, p) -> profile.ToLower() = p.ToLower()) with
            | None -> None
            | Some(mappedProfile, _) -> Some mappedProfile
        | _ -> None

    member x.GetFrameworkProfile() =        
        match x with 
        | DotNetFramework(FrameworkVersion.V4_Client) -> "$(TargetFrameworkProfile) == 'Client'" 
        | PortableFramework(_,profile) -> 
            match x.GetPortableProfile() with
            | None -> sprintf "$(TargetFrameworkProfile) == '%s'"  profile
            | Some mappedProfile -> sprintf "$(TargetFrameworkProfile) == '%s'"  mappedProfile
        | _ -> ""

    member x.GetPlatformIdentifier() =        
        match x with 
        | PortableFramework(_,_) -> sprintf "$(TargetPlatformIdentifier) == 'Portable'"
        | _ -> ""

    member x.GetPlatformVersion() =        
        match x with 
        | PortableFramework(v,_) -> sprintf "$(TargetPlatformVersion) == '%s'"  v
        | WindowsPhoneApp v -> sprintf "$(TargetPlatformVersion) == '%s'"  v
        | Windows v -> sprintf "$(TargetPlatformVersion) == '%s'"  v
        | _ -> ""

    member x.GetFrameworkCondition() =
        let (++) x y = 
           if String.IsNullOrEmpty y then 
                x 
           elif String.IsNullOrEmpty x then 
                y 
           else x + " And " + y
        match x with
        | DotNetFramework(fw) -> sprintf "$(TargetFrameworkVersion) == '%s'" (fw.ToString()) ++ x.GetFrameworkProfile()
        | PortableFramework _ -> x.GetFrameworkProfile() ++ x.GetPlatformIdentifier() ++ x.GetPlatformVersion()
        | WindowsPhoneApp _ -> x.GetPlatformVersion()
        | Windows _ -> x.GetPlatformVersion()
        | Silverlight v -> sprintf "$(SilverlightVersion) == '%s'" v
        | MonoAndroid -> ""
        | MonoTouch -> ""

    member x.GetGroupCondition() = sprintf "%s" (x.GetFrameworkIdentifier())

    override x.ToString() = 
        match x with
        | DotNetFramework v ->
            "net" + 
                match v with
                | FrameworkVersion.V1 -> "10"
                | FrameworkVersion.V1_1 -> "11"
                | FrameworkVersion.V2 -> "20"
                | FrameworkVersion.V3_5 -> "35"
                | FrameworkVersion.V4_Client -> "40"
                | FrameworkVersion.V4 -> "40"
                | FrameworkVersion.V4_5 -> "45"
                | FrameworkVersion.V4_5_1 -> "451"
                | FrameworkVersion.V4_5_2 -> "452"
                | FrameworkVersion.V4_5_3 -> "453"
        | PortableFramework(v,p) -> "portable-" + v.ToString()
        | MonoAndroid -> "monoandroid"
        | MonoTouch -> "monotouch"
        | Windows v -> "win" + v
        | WindowsPhoneApp v -> "wp" + v
        | Silverlight v -> "sl" + v

    static member DetectFromPath(path : string) : FrameworkIdentifier option = 
        
        let path = path.Replace("\\", "/").ToLower()
        let fi = new FileInfo(path)
        
        if path.Contains("lib/" + fi.Name.ToLower()) then Some(DotNetFramework(FrameworkVersion.V1))
        else 
            let startPos = path.LastIndexOf("lib/")
            let endPos = path.LastIndexOf(fi.Name.ToLower())
            if startPos < 0 || endPos < 0 then None
            else 
                path.Substring(startPos + 4, endPos - startPos - 5) 
                |> FrameworkIdentifier.Extract

    static member DefaultGroup = DotNetFramework(FrameworkVersion.V4).Group

module SpecialTargets =
    let KnownSpecialTargets = 
        [ Silverlight "v3.0"
          Silverlight "v4.0"
          Silverlight "v5.0"
          Silverlight "v5.0"
          WindowsPhoneApp "7.1"
          Windows "v8.0"
          WindowsPhoneApp "v8.0"
          WindowsPhoneApp "v8.1"
          MonoAndroid
          MonoTouch ]