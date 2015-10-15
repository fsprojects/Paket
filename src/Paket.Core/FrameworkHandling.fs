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
    | V4_6
    | V5_0
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
        | V4_6 -> "v4.6"
        | V5_0 -> "v5.0"

    member this.ShortString() =
        match this with
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
        | FrameworkVersion.V4_6 -> "46"
        | FrameworkVersion.V5_0 -> "50"

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
         ".netportable", "portable"
         "netportable", "portable"
         "0.0", ""
         ".", "" ]
        |> List.map (fun (p,r) -> p.ToLower(),r.ToLower())


/// Framework Identifier type.
type FrameworkIdentifier = 
    | DotNetFramework of FrameworkVersion
    | DNX of FrameworkVersion
    | DNXCore of FrameworkVersion
    | MonoAndroid
    | MonoTouch
    | MonoMac
    | XamariniOS
    | XamarinMac
    | Windows of string
    | WindowsPhoneApp of string
    | WindowsPhoneSilverlight of string
    | Silverlight of string

    
    override x.ToString() = 
        match x with
        | DotNetFramework v -> "net" + v.ShortString()
        | DNX v -> "dnx" + v.ShortString()             
        | DNXCore v -> "dnxcore" + v.ShortString()             
        | MonoAndroid -> "monoandroid"
        | MonoTouch -> "monotouch"
        | MonoMac -> "monomac"
        | XamariniOS -> "xamarinios"
        | XamarinMac -> "xamarinmac"
        | Windows v -> "win" + v
        | WindowsPhoneApp v -> "wp" + v
        | WindowsPhoneSilverlight v -> "wp" + v
        | Silverlight v -> "sl" + v.Replace("v","").Replace(".","")


    // returns a list of compatible platforms that this platform also supports
    member x.SupportedPlatforms =
        match x with
        | MonoAndroid -> [ ]
        | MonoTouch -> [ ]
        | MonoMac -> [ ]
        | XamariniOS -> [ ]
        | XamarinMac -> [ ]
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
        | DotNetFramework FrameworkVersion.V4_6 -> [ DotNetFramework FrameworkVersion.V4_5_3 ]
        | DotNetFramework FrameworkVersion.V5_0 -> [ DotNetFramework FrameworkVersion.V4_6 ]
        | DNX _ -> [ ]
        | DNXCore _ -> [ ]
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

    /// Return if the parameter is of the same framework category (dotnet, windows phone, silverlight, ...)
    member x.IsSameCategoryAs y =
        match (x, y) with
        | DotNetFramework _, DotNetFramework _ -> true
        | Silverlight _, Silverlight _ -> true
        | DNX _, DNX _ -> true
        | DNXCore _, DNXCore _ -> true
        | MonoAndroid _, MonoAndroid _ -> true
        | MonoMac _, MonoMac _ -> true
        | MonoTouch _, MonoTouch _ -> true
        | Windows _, Windows _ -> true
        | WindowsPhoneApp _, WindowsPhoneApp _ -> true
        | WindowsPhoneSilverlight _, WindowsPhoneSilverlight _ -> true
        | XamarinMac _, XamarinMac _ -> true
        | XamariniOS _, XamariniOS _ -> true
        | _ -> false


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
                | "net46" -> Some (DotNetFramework FrameworkVersion.V4_6)
                | "monotouch" | "monotouch10" | "monotouch1" -> Some MonoTouch
                | "monoandroid" | "monoandroid10" | "monoandroid1" -> Some MonoAndroid
                | "monomac" | "monomac10" | "monomac1" -> Some MonoMac
                | "xamarinios" | "xamarinios10" | "xamarinios1" | "xamarin.ios10" -> Some XamariniOS
                | "xamarinmac" | "xamarinmac20" | "xamarin.mac20" -> Some XamarinMac
                | "sl"  | "sl3" | "sl30" -> Some (Silverlight "v3.0")
                | "sl4" | "sl40" -> Some (Silverlight "v4.0")
                | "sl5" | "sl50" -> Some (Silverlight "v5.0")
                | "win8" | "win80" | "netcore45" | "win" | "winv45" -> Some (Windows "v4.5")
                | "win81" | "netcore46" -> Some (Windows "v4.5.1")
                | "wp7" | "wp70" | "sl4-wp7"| "sl4-wp70" -> Some (WindowsPhoneSilverlight "v7.0")
                | "wp71" | "sl4-wp71" | "sl4-wp"  -> Some (WindowsPhoneSilverlight "v7.1")
                | "wp8" | "wp80"  | "wpv80" -> Some (WindowsPhoneSilverlight "v8.0")
                | "wpa00" | "wpa" | "wpa81" | "wpapp81" | "wpapp" -> Some (WindowsPhoneApp "v8.1")
                | "dnx451" -> Some(DNX FrameworkVersion.V4_5_1)
                | "dnxcore50" | "netplatform50" | "netcore50" | "aspnetcore50" | "aspnet50" | "dotnet" -> Some(DNXCore FrameworkVersion.V5_0)
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

    member this.ProfilesCompatibleWithPortableProfile =
        match this with
        | SinglePlatform _ -> [ ]
        | PortableProfile(_,required) ->
            required
            |> List.map (function
                | DotNetFramework FrameworkVersion.V4_5
                | DotNetFramework FrameworkVersion.V4_5_1
                | DotNetFramework FrameworkVersion.V4_5_2
                | DotNetFramework FrameworkVersion.V4_5_3 ->
                    [
                        MonoTouch
                        MonoAndroid
                        XamariniOS
                        XamarinMac
                    ]
                | _ -> [ ]
            )
            |> List.reduce (@)
            |> List.distinct

    override this.ToString() =
        match this with
        | SinglePlatform x -> x.ToString()
        | PortableProfile(name,elements) ->
            match name with
            | "Profile5" -> "portable-net4+netcore45+MonoAndroid1+MonoTouch1"
            | "Profile6" -> "portable-net403+netcore45+MonoAndroid1+MonoTouch1"
            | "Profile7" -> "portable-net45+netcore45+MonoAndroid1+MonoTouch1"
            | "Profile14" -> "portable-net4+sl5+MonoAndroid1+MonoTouch1"
            | "Profile19" -> "portable-net403+sl5+MonoAndroid1+MonoTouch1"
            | "Profile24" -> "portable-net45+sl5+MonoAndroid1+MonoTouch1"
            | "Profile31" -> "portable-netcore451+wp81"
            | "Profile32" -> "portable-netcore451+wpa81"
            | "Profile37" -> "portable-net4+sl5+netcore45+MonoAndroid1+MonoTouch1"
            | "Profile42" -> "portable-net403+sl5+netcore45+MonoAndroid1+MonoTouch1"
            | "Profile44" -> "portable-net451+netcore451"
            | "Profile47" -> "portable-net45+sl5+netcore45+MonoAndroid1+MonoTouch1"
            | "Profile49" -> "portable-net45+wp8+MonoAndroid1+MonoTouch1"
            | "Profile78" -> "portable-net45+netcore45+wp8+MonoAndroid1+MonoTouch1"
            | "Profile84" -> "portable-wpa81+wp81"
            | "Profile92" -> "portable-net4+netcore45+wpa81+MonoAndroid1+MonoTouch1"
            | "Profile102" -> "portable-net403+netcore45+wpa81+MonoAndroid1+MonoTouch1"
            | "Profile111" -> "portable-net45+netcore45+wpa81+MonoAndroid1+MonoTouch1"
            | "Profile136" -> "portable-net4+sl5+netcore45+wp8+MonoAndroid1+MonoTouch1"
            | "Profile147" -> "portable-net403+sl5+netcore45+wp8+MonoAndroid1+MonoTouch1"
            | "Profile151" -> "portable-net451+netcore451+wpa81"
            | "Profile157" -> "portable-netcore451+wpa81+wp81"
            | "Profile158" -> "portable-net45+sl5+netcore45+wp8+MonoAndroid1+MonoTouch1"
            | "Profile225" -> "portable-net4+sl5+netcore45+wpa81+MonoAndroid1+MonoTouch1"
            | "Profile240" -> "portable-net403+sl5+netcore45+wpa81"
            | "Profile255" -> "portable-net45+sl5+netcore45+wpa81+MonoAndroid1+MonoTouch1"
            | "Profile259" -> "portable-net45+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1"
            | "Profile328" -> "portable-net4+sl5+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1"
            | "Profile336" -> "portable-net403+sl5+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1"
            | "Profile344" -> "portable-net45+sl5+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1"
            | _ -> "portable-net45+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1" // Use Portable259 as default

module KnownTargetProfiles =
    let DotNetFrameworkVersions =
       [FrameworkVersion.V1
        FrameworkVersion.V1_1
        FrameworkVersion.V2
        FrameworkVersion.V3
        FrameworkVersion.V3_5
        FrameworkVersion.V4_Client
        FrameworkVersion.V4
        FrameworkVersion.V4_5
        FrameworkVersion.V4_5_1
        FrameworkVersion.V4_5_2
        FrameworkVersion.V4_5_3
        FrameworkVersion.V4_6]

    let DotNetFrameworkProfiles =
       DotNetFrameworkVersions
       |> List.map (fun x -> SinglePlatform(DotNetFramework(x)))

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
        SinglePlatform(XamariniOS)
        SinglePlatform(XamarinMac)
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