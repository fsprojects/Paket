namespace Paket

open System.IO
open System

[<RequireQualifiedAccess>]
/// The Framework version.
type FrameworkVersion = 
    | V1
    | V1_1
    | V2
    | V3
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
        | V3 -> "v3.0"
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
    | MonoAndroid
    | MonoTouch
    | Windows of string
    | WindowsPhoneApp of string
    | WindowsPhoneSilverlight of string
    | Silverlight of string

    static member Extract(path:string) =
        let knownAliases =
            [".net", "net"
             "netframework", "net"
             ".netframework", "net"
             ".netcore", "netcore"
             "winrt", "netcore"
             "silverlight", "sl"
             "windowsphone", "wp"
             "windows", "win"
             "windowsPhoneApp", "wpa"
         
             "1.0", "10" 
             "1.1", "11" 
             "2.0", "20" 
             "3.5", "35" 
             "4.0", "40" 
             "4.5", "45" 
             "0.0", "" ]

        let path = 
            knownAliases
            |> List.fold (fun (path:string) (pattern,replacement) -> path.Replace(pattern.ToLower(),replacement.ToLower())) (path.ToLower())

        match path with
        | "net10" | "net1" | "10" -> Some (DotNetFramework FrameworkVersion.V1)
        | "net11" | "11" -> Some (DotNetFramework FrameworkVersion.V1_1)
        | "net20" | "net2" | "net" | "net20-full" | "20" -> Some (DotNetFramework FrameworkVersion.V2)
        | "net35" | "net35-full" | "35" -> Some (DotNetFramework FrameworkVersion.V3_5)
        | "net40" | "net4" | "40" | "net40-client" | "net4-client" -> Some (DotNetFramework FrameworkVersion.V4_Client)
        | "net40-full" | "net403" -> Some (DotNetFramework FrameworkVersion.V4)
        | "net45" | "net45-full" | "45" -> Some (DotNetFramework FrameworkVersion.V4_5)
        | "net451" -> Some (DotNetFramework FrameworkVersion.V4_5_1)
        | "net452" -> Some (DotNetFramework FrameworkVersion.V4_5_2)
        | "net453" -> Some (DotNetFramework FrameworkVersion.V4_5_3)
        | "monotouch" -> Some MonoTouch
        | "monoandroid" -> Some MonoAndroid
        | "sl3" | "sl30" -> Some (Silverlight "v3.0")
        | "sl4" | "sl40" -> Some (Silverlight "v4.0")
        | "sl5" | "sl50" -> Some (Silverlight "v5.0")
        | "win8" | "win80" | "netcore45" | "win" -> Some (Windows "v8.0")
        | "win81" | "netcore46" -> Some (Windows "v8.1")
        | "wp7" | "wp70" | "sl4-wp7"| "sl4-wp70" -> Some (WindowsPhoneSilverlight "v7.0")
        | "wp71" | "sl4-wp71" | "sl4-wp"  -> Some (WindowsPhoneSilverlight "v7.1")
        | "wp8" | "wp80" -> Some (WindowsPhoneSilverlight "v8.0")
        | "wpa81" -> Some (WindowsPhoneApp "v8.1")
        | _ -> None
    
    override x.ToString() = 
        match x with
        | DotNetFramework v ->
            "net" + 
                match v with
                | FrameworkVersion.V1 -> "10"
                | FrameworkVersion.V1_1 -> "11"
                | FrameworkVersion.V2 -> "20"
                | FrameworkVersion.V3 -> "30"
                | FrameworkVersion.V3_5 -> "35"
                | FrameworkVersion.V4_Client -> "40"
                | FrameworkVersion.V4 -> "40"
                | FrameworkVersion.V4_5 -> "45"
                | FrameworkVersion.V4_5_1 -> "451"
                | FrameworkVersion.V4_5_2 -> "452"
                | FrameworkVersion.V4_5_3 -> "453"
        | MonoAndroid -> "monoandroid"
        | MonoTouch -> "monotouch"
        | Windows v -> "win" + v
        | WindowsPhoneApp v -> "wp" + v
        | WindowsPhoneSilverlight v -> "wp" + v
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

    // returns a list of compatible platforms that this platform also supports
    member x.SupportedPlatforms =
        match x with
        | MonoAndroid -> [ DotNetFramework FrameworkVersion.V4_5_3 ]
        | MonoTouch -> [ DotNetFramework FrameworkVersion.V4_5_3 ]
        | DotNetFramework FrameworkVersion.V1 -> [ ]
        | DotNetFramework FrameworkVersion.V1_1 -> [ DotNetFramework FrameworkVersion.V1 ]
        | DotNetFramework FrameworkVersion.V2 -> [ DotNetFramework FrameworkVersion.V1_1 ]
        | DotNetFramework FrameworkVersion.V3 -> [ DotNetFramework FrameworkVersion.V2 ]
        | DotNetFramework FrameworkVersion.V3_5 -> [ DotNetFramework FrameworkVersion.V3 ]
        | DotNetFramework FrameworkVersion.V4_Client -> [ DotNetFramework FrameworkVersion.V3_5 ]
        | DotNetFramework FrameworkVersion.V4 -> [ DotNetFramework FrameworkVersion.V4_Client ]
        | DotNetFramework FrameworkVersion.V4_5 -> [ DotNetFramework FrameworkVersion.V4 ]
        | DotNetFramework FrameworkVersion.V4_5_1 -> [ DotNetFramework FrameworkVersion.V4_5 ]
        | DotNetFramework FrameworkVersion.V4_5_2 -> [ DotNetFramework FrameworkVersion.V4_5_1 ]
        | DotNetFramework FrameworkVersion.V4_5_3 -> [ DotNetFramework FrameworkVersion.V4_5_2 ]
        | Silverlight "v3.0" -> [ ]
        | Silverlight "v4.0" -> [ Silverlight "v3.0" ]
        | Silverlight "v5.0" -> [ Silverlight "v4.0" ]
        | Windows "v8.0" -> [ ]
        | Windows "v8.1" -> [ Windows "v8.0" ]
        | WindowsPhoneApp "v8.1" -> [ ]
        | WindowsPhoneSilverlight "v7.0" -> [ ]
        | WindowsPhoneSilverlight "v7.1" -> [ WindowsPhoneSilverlight "v7.0" ]
        | WindowsPhoneSilverlight "v8.0" -> [ WindowsPhoneSilverlight "v7.1" ]
        | WindowsPhoneSilverlight "v8.1" -> [ WindowsPhoneSilverlight "v8.0" ]

        // wildcards for future versions. new versions should be added above, though, so the penalty will be calculated correctly.
        | Silverlight _ -> [ Silverlight "v5.0" ]
        | Windows _ -> [ Windows "v8.1" ]
        | WindowsPhoneApp _ -> [ WindowsPhoneApp "v8.1" ]
        | WindowsPhoneSilverlight _ -> [ WindowsPhoneSilverlight "v8.1" ]

type TargetProfile =
    | SinglePlatform of FrameworkIdentifier
    | PortableProfile of string * FrameworkIdentifier list

    static member KnownDotNetFrameworkProfiles =
       [SinglePlatform(DotNetFramework FrameworkVersion.V1)
        SinglePlatform(DotNetFramework FrameworkVersion.V1_1)
        SinglePlatform(DotNetFramework FrameworkVersion.V2)
        SinglePlatform(DotNetFramework FrameworkVersion.V3)
        SinglePlatform(DotNetFramework FrameworkVersion.V3_5)
        SinglePlatform(DotNetFramework FrameworkVersion.V4_Client)
        SinglePlatform(DotNetFramework FrameworkVersion.V4)
        SinglePlatform(DotNetFramework FrameworkVersion.V4_5)
        SinglePlatform(DotNetFramework FrameworkVersion.V4_5_1)
        SinglePlatform(DotNetFramework FrameworkVersion.V4_5_2)
        SinglePlatform(DotNetFramework FrameworkVersion.V4_5_3)]

    static member KnownWindowsProfiles =
       [SinglePlatform(Windows "v8.0")
        SinglePlatform(Windows "v8.1")]

    static member KnownSilverlightProfiles =
       [SinglePlatform(Silverlight "v3.0")
        SinglePlatform(Silverlight "v4.0")
        SinglePlatform(Silverlight "v5.0")]

    static member KnownWindowsPhoneSilverlightProfiles =
       [SinglePlatform(WindowsPhoneSilverlight "v7.0")
        SinglePlatform(WindowsPhoneSilverlight "v7.1")
        SinglePlatform(WindowsPhoneSilverlight "v8.0")
        SinglePlatform(WindowsPhoneSilverlight "v8.1")]

    static member KnownTargetProfiles =
       TargetProfile.KnownDotNetFrameworkProfiles @ 
       TargetProfile.KnownWindowsProfiles @ 
       TargetProfile.KnownSilverlightProfiles @
       TargetProfile.KnownWindowsPhoneSilverlightProfiles @
       [SinglePlatform(MonoAndroid)
        SinglePlatform(MonoTouch)        
        SinglePlatform(WindowsPhoneApp "v8.1")
        PortableProfile("Profile2", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v8.0"; WindowsPhoneSilverlight "v7.0" ])
        PortableProfile("Profile3", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0" ])
        PortableProfile("Profile4", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v4.0"; Windows "v8.0"; WindowsPhoneSilverlight "v7.0" ])
        PortableProfile("Profile5", [ DotNetFramework FrameworkVersion.V4; Windows "v8.0"; MonoAndroid; MonoTouch ])
        PortableProfile("Profile5", [ DotNetFramework FrameworkVersion.V4; Windows "v8.0" ])
        PortableProfile("Profile6", [ DotNetFramework FrameworkVersion.V4; Windows "v8.0" ])
        PortableProfile("Profile7" , [ DotNetFramework FrameworkVersion.V4_5; Windows "v8.0" ])
        PortableProfile("Profile14", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0" ])
        PortableProfile("Profile18", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0" ])
        PortableProfile("Profile19", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0" ])
        PortableProfile("Profile23", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v4.0" ])
        PortableProfile("Profile24", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v5.0" ])
        PortableProfile("Profile31", [ Windows "v8.1"; WindowsPhoneSilverlight "v8.1" ])
        PortableProfile("Profile32", [ Windows "v8.1"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile36", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v8.0"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile37", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; Windows "v8.0" ])
        PortableProfile("Profile41", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v8.0" ])
        PortableProfile("Profile42", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; Windows "v8.0" ])
        PortableProfile("Profile44", [ DotNetFramework FrameworkVersion.V4_5_1; Windows "v8.1" ])
        PortableProfile("Profile46", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v4.0"; Windows "v8.0" ])
        PortableProfile("Profile47", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v5.0"; Windows "v8.0" ])
        PortableProfile("Profile49", [ DotNetFramework FrameworkVersion.V4_5; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile78", [ DotNetFramework FrameworkVersion.V4_5; Windows "v8.0"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile84", [ WindowsPhoneApp "v8.1"; WindowsPhoneSilverlight "v8.1" ])
        PortableProfile("Profile88", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v8.0"; WindowsPhoneSilverlight "v7.1" ])
        PortableProfile("Profile92", [ DotNetFramework FrameworkVersion.V4; Windows "v8.0"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile95", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v8.0"; WindowsPhoneSilverlight "v7.0" ])
        PortableProfile("Profile96", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v8.0"; WindowsPhoneSilverlight "v7.1" ])
        PortableProfile("Profile102", [ DotNetFramework FrameworkVersion.V4; Windows "v8.0"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile104", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v4.0"; Windows "v8.0"; WindowsPhoneSilverlight "v7.1" ])
        PortableProfile("Profile111", [ DotNetFramework FrameworkVersion.V4_5; Windows "v8.0"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile136", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; WindowsPhoneSilverlight "v8.0"; Windows "v8.0"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile143", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v8.0"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile147", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; Windows "v8.0"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile151", [ DotNetFramework FrameworkVersion.V4_5_1; Windows "v8.1"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile154", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v4.0"; Windows "v8.0"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile157", [ Windows "v8.1"; WindowsPhoneApp "v8.1"; WindowsPhoneSilverlight "v8.1" ])
        PortableProfile("Profile158", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v5.0"; Windows "v8.0"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile225", [ DotNetFramework  FrameworkVersion.V4; Silverlight "v5.0"; Windows "v8.0"; WindowsPhoneApp "v8.1" ])                  
        PortableProfile("Profile240", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; Windows "v8.0"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile255", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v5.0"; Windows "v8.0"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile259", [ DotNetFramework FrameworkVersion.V4_5; Windows "v8.0"; WindowsPhoneSilverlight "v8.0"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile328", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; WindowsPhoneSilverlight "v8.0"; Windows "v8.0"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile336", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; Windows "v8.0"; WindowsPhoneApp "v8.1"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile344", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v5.0"; Windows "v8.0"; WindowsPhoneApp "v8.1"; WindowsPhoneSilverlight "v8.0" ])]

    static member FindPortableProfile name =
        TargetProfile.KnownTargetProfiles
        |> List.pick (fun target -> match target with
                                    | PortableProfile(n, _) as p -> if n = name then Some(p) else None
                                    | _ -> None)

