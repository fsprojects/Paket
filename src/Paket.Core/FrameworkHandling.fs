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

module KnownAliases =
    let Data =
        [".net", "net"
         "netframework", "net"
         ".netframework", "net"
         ".netcore", "netcore"
         "winrt", "netcore"
         "silverlight", "sl"
         "windowsphone", "wp"
         "windows", "win"
         "windowsPhoneApp", "wpa"
         "0.0", ""
         ".", "" ]
        |> List.map (fun (p,r) -> p.ToLower(),r.ToLower())


/// Framework Identifier type.
type FrameworkIdentifier = 
    | DotNetFramework of FrameworkVersion
    | MonoAndroid
    | MonoTouch
    | MonoMac
    | Windows of string
    | WindowsPhoneApp of string
    | WindowsPhoneSilverlight of string
    | Silverlight of string

    
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
        | MonoMac -> "monomac"
        | Windows v -> "win" + v
        | WindowsPhoneApp v -> "wp" + v
        | WindowsPhoneSilverlight v -> "wp" + v
        | Silverlight v -> "sl" + v


    // returns a list of compatible platforms that this platform also supports
    member x.SupportedPlatforms =
        match x with
        | MonoAndroid -> [ DotNetFramework FrameworkVersion.V4_5_3 ]
        | MonoTouch -> [ DotNetFramework FrameworkVersion.V4_5_3 ]
        | MonoMac -> [ DotNetFramework FrameworkVersion.V4_5_3 ]
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
        | Windows "v4.5" -> [ ]
        | Windows "v4.5.1" -> [ Windows "v4.5" ]
        | WindowsPhoneApp "v8.1" -> [ ]
        | WindowsPhoneSilverlight "v7.0" -> [ ]
        | WindowsPhoneSilverlight "v7.1" -> [ WindowsPhoneSilverlight "v7.0" ]
        | WindowsPhoneSilverlight "v8.0" -> [ WindowsPhoneSilverlight "v7.1" ]
        | WindowsPhoneSilverlight "v8.1" -> [ WindowsPhoneSilverlight "v8.0" ]

        // wildcards for future versions. new versions should be added above, though, so the penalty will be calculated correctly.
        | Silverlight _ -> [ Silverlight "v5.0" ]
        | Windows _ -> [ Windows "v4.5.1" ]
        | WindowsPhoneApp _ -> [ WindowsPhoneApp "v8.1" ]
        | WindowsPhoneSilverlight _ -> [ WindowsPhoneSilverlight "v8.1" ]


module FrameworkDetection =
    let private cache = System.Collections.Concurrent.ConcurrentDictionary<_,_>()

    let Extract(path:string) =
        match cache.TryGetValue path with
        | true,x -> x
        | _ ->
            let path = 
                let sb = new Text.StringBuilder(path.ToLower())
                for pattern,replacement in KnownAliases.Data do
                     sb.Replace(pattern,replacement) |> ignore
                sb.ToString()

            let result = 
                match path with
                | "net10" | "net1" | "10" -> Some (DotNetFramework FrameworkVersion.V1)
                | "net11" | "11" -> Some (DotNetFramework FrameworkVersion.V1_1)
                | "net20" | "net2" | "net" | "net20-full" | "net20-client" | "20" -> Some (DotNetFramework FrameworkVersion.V2)
                | "net35" | "net35-client" | "net35-full" | "35" -> Some (DotNetFramework FrameworkVersion.V3_5)
                | "net40" | "net4" | "40" | "net40-client" | "net4-client" -> Some (DotNetFramework FrameworkVersion.V4_Client)
                | "net40-full" | "net403" -> Some (DotNetFramework FrameworkVersion.V4)
                | "net45" | "net45-full" | "45" -> Some (DotNetFramework FrameworkVersion.V4_5)
                | "net451" -> Some (DotNetFramework FrameworkVersion.V4_5_1)
                | "net452" -> Some (DotNetFramework FrameworkVersion.V4_5_2)
                | "net453" -> Some (DotNetFramework FrameworkVersion.V4_5_3)
                | "monotouch" | "monotouch10" -> Some MonoTouch
                | "monoandroid" | "monoandroid10" -> Some MonoAndroid
                | "monomac" | "monomac10" -> Some MonoMac
                | "sl3" | "sl30" -> Some (Silverlight "v3.0")
                | "sl4" | "sl40" -> Some (Silverlight "v4.0")
                | "sl5" | "sl50" -> Some (Silverlight "v5.0")
                | "win8" | "win80" | "netcore45" | "win" -> Some (Windows "v4.5")
                | "win81" | "netcore46" -> Some (Windows "v4.5.1")
                | "wp7" | "wp70" | "sl4-wp7"| "sl4-wp70" -> Some (WindowsPhoneSilverlight "v7.0")
                | "wp71" | "sl4-wp71" | "sl4-wp"  -> Some (WindowsPhoneSilverlight "v7.1")
                | "wp8" | "wp80" -> Some (WindowsPhoneSilverlight "v8.0")
                | "wpa00" | "wpa81" -> Some (WindowsPhoneApp "v8.1")
                | _ -> None

            cache.[path] <- result
            result

    let DetectFromPath(path : string) : FrameworkIdentifier option =         
        let path = path.Replace("\\", "/").ToLower()
        let fi = new FileInfo(path)
        
        if path.Contains("lib/" + fi.Name.ToLower()) then Some(DotNetFramework(FrameworkVersion.V1))
        else 
            let startPos = path.LastIndexOf("lib/")
            let endPos = path.LastIndexOf(fi.Name.ToLower())
            if startPos < 0 || endPos < 0 then None
            else 
                path.Substring(startPos + 4, endPos - startPos - 5) 
                |> Extract


type TargetProfile =
    | SinglePlatform of FrameworkIdentifier
    | PortableProfile of string * FrameworkIdentifier list

module KnownTargetProfiles =
    let DotNetFrameworkProfiles =
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

    let WindowsProfiles =
       [SinglePlatform(Windows "v4.5")
        SinglePlatform(Windows "v4.5.1")]

    let SilverlightProfiles =
       [SinglePlatform(Silverlight "v3.0")
        SinglePlatform(Silverlight "v4.0")
        SinglePlatform(Silverlight "v5.0")]

    let WindowsPhoneSilverlightProfiles =
       [SinglePlatform(WindowsPhoneSilverlight "v7.0")
        SinglePlatform(WindowsPhoneSilverlight "v7.1")
        SinglePlatform(WindowsPhoneSilverlight "v8.0")
        SinglePlatform(WindowsPhoneSilverlight "v8.1")]

    let AllProfiles =
       DotNetFrameworkProfiles @ 
       WindowsProfiles @ 
       SilverlightProfiles @
       WindowsPhoneSilverlightProfiles @
       [SinglePlatform(MonoAndroid)
        SinglePlatform(MonoTouch)        
        SinglePlatform(WindowsPhoneApp "v8.1")
        PortableProfile("Profile2", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v4.5"; WindowsPhoneSilverlight "v7.0" ])
        PortableProfile("Profile3", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0" ])
        PortableProfile("Profile4", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v4.0"; Windows "v4.5"; WindowsPhoneSilverlight "v7.0" ])
        PortableProfile("Profile5", [ DotNetFramework FrameworkVersion.V4; Windows "v4.5" ])
        PortableProfile("Profile6", [ DotNetFramework FrameworkVersion.V4; Windows "v4.5" ])
        PortableProfile("Profile7" , [ DotNetFramework FrameworkVersion.V4_5; Windows "v4.5" ])
        PortableProfile("Profile14", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0" ])
        PortableProfile("Profile18", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0" ])
        PortableProfile("Profile19", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0" ])
        PortableProfile("Profile23", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v4.0" ])
        PortableProfile("Profile24", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v5.0" ])
        PortableProfile("Profile31", [ Windows "v4.5.1"; WindowsPhoneSilverlight "v8.1" ])
        PortableProfile("Profile32", [ Windows "v4.5.1"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile36", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v4.5"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile37", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; Windows "v4.5" ])
        PortableProfile("Profile41", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v4.5" ])
        PortableProfile("Profile42", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; Windows "v4.5" ])
        PortableProfile("Profile44", [ DotNetFramework FrameworkVersion.V4_5_1; Windows "v4.5.1" ])
        PortableProfile("Profile46", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v4.0"; Windows "v4.5" ])
        PortableProfile("Profile47", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v5.0"; Windows "v4.5" ])
        PortableProfile("Profile49", [ DotNetFramework FrameworkVersion.V4_5; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile78", [ DotNetFramework FrameworkVersion.V4_5; Windows "v4.5"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile84", [ WindowsPhoneApp "v8.1"; WindowsPhoneSilverlight "v8.1" ])
        PortableProfile("Profile88", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v4.5"; WindowsPhoneSilverlight "v7.1" ])
        PortableProfile("Profile92", [ DotNetFramework FrameworkVersion.V4; Windows "v4.5"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile95", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v4.5"; WindowsPhoneSilverlight "v7.0" ])
        PortableProfile("Profile96", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v4.5"; WindowsPhoneSilverlight "v7.1" ])
        PortableProfile("Profile102", [ DotNetFramework FrameworkVersion.V4; Windows "v4.5"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile104", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v4.0"; Windows "v4.5"; WindowsPhoneSilverlight "v7.1" ])
        PortableProfile("Profile111", [ DotNetFramework FrameworkVersion.V4_5; Windows "v4.5"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile136", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; WindowsPhoneSilverlight "v8.0"; Windows "v4.5"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile143", [ DotNetFramework FrameworkVersion.V4; Silverlight "v4.0"; Windows "v4.5"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile147", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; Windows "v4.5"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile151", [ DotNetFramework FrameworkVersion.V4_5_1; Windows "v4.5.1"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile154", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v4.0"; Windows "v4.5"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile157", [ Windows "v4.5.1"; WindowsPhoneApp "v8.1"; WindowsPhoneSilverlight "v8.1" ])
        PortableProfile("Profile158", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v5.0"; Windows "v4.5"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile225", [ DotNetFramework  FrameworkVersion.V4; Silverlight "v5.0"; Windows "v4.5"; WindowsPhoneApp "v8.1" ])                  
        PortableProfile("Profile240", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; Windows "v4.5"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile255", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v5.0"; Windows "v4.5"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile259", [ DotNetFramework FrameworkVersion.V4_5; Windows "v4.5"; WindowsPhoneSilverlight "v8.0"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile328", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; WindowsPhoneSilverlight "v8.0"; Windows "v4.5"; WindowsPhoneApp "v8.1" ])
        PortableProfile("Profile336", [ DotNetFramework FrameworkVersion.V4; Silverlight "v5.0"; Windows "v4.5"; WindowsPhoneApp "v8.1"; WindowsPhoneSilverlight "v8.0" ])
        PortableProfile("Profile344", [ DotNetFramework FrameworkVersion.V4_5; Silverlight "v5.0"; Windows "v4.5"; WindowsPhoneApp "v8.1"; WindowsPhoneSilverlight "v8.0" ])]

    let FindPortableProfile name =
        AllProfiles
        |> List.pick (function
                      | PortableProfile(n, _) as p when n = name -> Some p
                      | _ -> None)