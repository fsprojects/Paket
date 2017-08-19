module Paket.PlatformMatching

open System
open ProviderImplementation.AssemblyReader.Utils.SHA1
open Logging

[<Literal>]
let MaxPenalty = 10000000

type ParsedPlatformPath =
  { Name : string
    Platforms : FrameworkIdentifier list }
    static member Empty = { Name = ""; Platforms = [] }
    static member FromTargetProfile (p:TargetProfile) =
        { Name = p.ToString(); Platforms = p.Frameworks }
    member x.ToTargetProfile warnIfUnsupported =
        match x.Platforms with
        | _ when System.String.IsNullOrEmpty x.Name -> None
        | [] -> None // Not detected earlier.
        | [p] -> Some (SinglePlatform p)
        | plats -> Some (TargetProfile.FindPortable warnIfUnsupported plats)
    member pp.IsEmpty = String.IsNullOrEmpty pp.Name || pp.Platforms.IsEmpty

let inline split (path : string) =
    path.Split('+')
    |> Array.map (fun s -> System.Text.RegularExpressions.Regex.Replace(KnownAliases.normalizeFramework s, @"portable[\d\.]*-",""))

// TODO: This function does now quite a lot, there probably should be several functions.
let private extractPlatformsPriv = memoize (fun path ->
    if System.String.IsNullOrEmpty path then Some ParsedPlatformPath.Empty
    else
        let splits = split path
        let platforms = splits |> Array.choose FrameworkDetection.Extract |> Array.toList
        if platforms.Length = 0 then
            if splits.Length = 1 && splits.[0].StartsWith "profile" then
                // might be something like portable4.6-profile151
                let found =
                    KnownTargetProfiles.FindPortableProfile splits.[0]
                    |> ParsedPlatformPath.FromTargetProfile
                Some { found with Name = path }
            else
                None
        else Some { Name = path; Platforms = platforms })

let extractPlatforms warn path =
    match extractPlatformsPriv path with
    | None ->
        if warn then
            traceWarnfn "Could not detect any platforms from '%s'" path
        None
    | Some s -> Some s

let forceExtractPlatforms path =
    match extractPlatforms false path with
    | Some s -> s
    | None -> failwithf "Extracting platforms from path '%s' failed" path

// TODO: In future work this stuff should be rewritten. This penalty stuff is more random than a proper implementation.
// Penalty: 000000
//               ^ Minor adjustments
//              ^ Version jump
//             ^ Switch between netcore -> full
//            ^ Portable profiles
//           ^ Unsupported Profiles
//          ^ Fallback
let [<Literal>] Penalty_Client = 1
let [<Literal>] Penalty_VersionJump = 10
let [<Literal>] Penalty_Netcore = 100
let [<Literal>] Penalty_Portable = 1000
let [<Literal>] Penalty_UnsupportedProfile = 10000
let [<Literal>] Penalty_Fallback = 100000
let rec getPlatformPenalty =
    memoize (fun (targetPlatform:TargetProfile,packagePlatform:TargetProfile) ->
        if packagePlatform = targetPlatform then
            0
        else
            match targetPlatform, packagePlatform with
            | PortableProfile _, SinglePlatform _ ->
                // There is no point in searching for frameworks in portables...
                MaxPenalty
            | _, PortableProfile (PortableProfileType.UnsupportedProfile fws) ->
                // We cannot find unsupported profiles in our "SupportedPlatforms" list
                // Just check if we are compatible at all and return a high penalty
                
                if packagePlatform.IsSupportedBy targetPlatform then
                    Penalty_UnsupportedProfile
                else MaxPenalty
            | _ ->
                let penalty =
                    targetPlatform.SupportedPlatforms
                    |> Seq.map (fun target -> getPlatformPenalty (target, packagePlatform))
                    |> Seq.append [MaxPenalty]
                    |> Seq.min
                    |> fun p -> p + Penalty_VersionJump

                match targetPlatform, packagePlatform with
                | SinglePlatform (DotNetFramework _), SinglePlatform (DotNetStandard _) -> Penalty_Netcore + penalty
                | SinglePlatform (DotNetStandard _), SinglePlatform(DotNetFramework _) -> Penalty_Netcore + penalty
                | SinglePlatform _, PortableProfile _ -> Penalty_Portable + penalty
                | PortableProfile _, SinglePlatform _ -> Penalty_Portable + penalty
                | _ -> penalty)

let getFrameworkPenalty (fr1, fr2) =
    getPlatformPenalty (SinglePlatform fr1, SinglePlatform fr2)


let getPathPenalty =
    memoize
      (fun (path:ParsedPlatformPath,platform:TargetProfile) ->
        let handleEmpty () =
            match platform with
            | SinglePlatform(Native(_)) -> MaxPenalty // an empty path is considered incompatible with native targets
            | _ -> Penalty_Fallback // an empty path is considered compatible with every .NET target, but with a high penalty so explicit paths are preferred
        match path.Platforms with
        | _ when String.IsNullOrWhiteSpace path.Name -> handleEmpty()
        | [] -> MaxPenalty // Ignore this path as it contains no platforms, but the folder apparently has a name -> we failed to detect the framework and ignore it
        | [ h ] ->
            let additionalPen = if path.Name.EndsWith "-client" then Penalty_Client else 0
            additionalPen + getPlatformPenalty(platform,SinglePlatform h)
        | _ ->
            // No warnig -> should be reported later
            getPlatformPenalty(platform, TargetProfile.FindPortable false path.Platforms))

[<Obsolete("Used in test code, use getPathPenalty instead.")>]
let getFrameworkPathPenalty fr path =
    match fr with
    | [ h ] -> getPathPenalty (path, SinglePlatform h)
    | _ ->
        // No warnig -> should be reported later
        getPathPenalty (path, TargetProfile.FindPortable false fr)

type PathPenalty = (ParsedPlatformPath * int)

let comparePaths (p1 : PathPenalty) (p2 : PathPenalty) =
    let platformCount1 = (fst p1).Platforms.Length
    let platformCount2 = (fst p2).Platforms.Length

    // prefer full framework over portable
    if platformCount1 = 1 && platformCount2 > 1 then
        -1
    elif platformCount1 > 1 && platformCount2 = 1 then
        1
    // prefer lower version penalty
    elif snd p1 < snd p2 then
       -1
    elif snd p1 > snd p2 then
       1
    // prefer portable platform whith less platforms
    elif platformCount1 < platformCount2 then
        -1
    elif platformCount1 > platformCount2 then
        1
    else
        0


let collectPlatforms =
    let rec loop (acc:TargetProfile list) (framework:TargetProfile) (profls:TargetProfile Set) =
        profls
        |> Seq.fold (fun acc f ->
            if f.SupportedPlatforms |> Set.contains (framework) then
                Set.add f acc
            else acc) Set.empty
    memoize (fun (framework,profls) -> loop ([]:TargetProfile list) framework profls)


let platformsSupport = 
    let rec platformsSupport platform platforms = 
        if Set.isEmpty platforms then MaxPenalty
        elif platforms |> Set.contains (platform) then 1
        else 
            platforms |> Set.toArray
            |> Array.Parallel.map (fun (p : TargetProfile) -> 
                collectPlatforms (p,KnownTargetProfiles.AllProfiles)
            ) |> Set.unionMany
            |> platformsSupport platform |> (+) 1
    memoize (fun (platform,platforms) -> platformsSupport platform platforms)


let findBestMatch = 
    let rec findBestMatch (paths : ParsedPlatformPath list, targetProfile : TargetProfile) = 
        paths
        |> List.map (fun path -> path, (getPathPenalty (path, targetProfile)))
        |> List.filter (fun (_, penalty) -> penalty < MaxPenalty)
        |> List.sortWith comparePaths
        |> List.map fst
        |> List.tryHead

    memoize (fun (paths : ParsedPlatformPath list,targetProfile : TargetProfile) -> findBestMatch(paths,targetProfile))

// For a given list of paths and target profiles return tuples of paths with their supported target profiles.
// Every target profile will only be listed for own path - the one that best supports it. 
let getSupportedTargetProfiles =    
    memoize 
        (fun (paths : ParsedPlatformPath list) ->
            KnownTargetProfiles.AllProfiles
            |> Seq.choose (fun target ->
                match findBestMatch(paths,target) with
                | Some p -> Some(p, target)
                | _ -> None)
            |> Seq.groupBy fst
            |> Seq.map (fun (path, group) -> path, Seq.map snd group |> Set.ofSeq)
            |> Map.ofSeq)


let getTargetCondition (target:TargetProfile) =
    match target with
    | SinglePlatform(platform) ->
        match platform with
        | DotNetFramework(version) ->"$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DNX(version) ->"$(TargetFrameworkIdentifier) == 'DNX'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DNXCore(version) ->"$(TargetFrameworkIdentifier) == 'DNXCore'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DotNetStandard(version) ->"$(TargetFrameworkIdentifier) == '.NETStandard'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DotNetCoreApp(version) ->"$(TargetFrameworkIdentifier) == '.NETCoreApp'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DotNetUnity(DotNetUnityVersion.V3_5_Full as version) ->
            "$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "($(TargetFrameworkVersion) == '%O' And $(TargetFrameworkProfile) == 'Unity Full v3.5')" version
        | DotNetUnity(DotNetUnityVersion.V3_5_Subset as version) ->
            "$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "($(TargetFrameworkVersion) == '%O' And $(TargetFrameworkProfile) == 'Unity Subset v3.5')" version
        | DotNetUnity(DotNetUnityVersion.V3_5_Micro as version) ->
            "$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "($(TargetFrameworkVersion) == '%O' And $(TargetFrameworkProfile) == 'Unity Micro v3.5')" version
        | DotNetUnity(DotNetUnityVersion.V3_5_Web as version) ->
            "$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "($(TargetFrameworkVersion) == '%O' And $(TargetFrameworkProfile) == 'Unity Web v3.5')" version               
        | Windows(version) -> "$(TargetFrameworkIdentifier) == '.NETCore'", sprintf "$(TargetFrameworkVersion) == '%O'" version.NetCoreVersion
        | Silverlight(version) -> "$(TargetFrameworkIdentifier) == 'Silverlight'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | WindowsPhoneApp(version) -> "$(TargetFrameworkIdentifier) == 'WindowsPhoneApp'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | WindowsPhone(version) -> "$(TargetFrameworkIdentifier) == 'WindowsPhone'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | MonoAndroid(version) -> "$(TargetFrameworkIdentifier) == 'MonoAndroid'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | MonoTouch -> "$(TargetFrameworkIdentifier) == 'MonoTouch'", ""
        | MonoMac -> "$(TargetFrameworkIdentifier) == 'MonoMac'", ""
        | XamariniOS -> "$(TargetFrameworkIdentifier) == 'Xamarin.iOS'", ""
        | XamarinTV -> "$(TargetFrameworkIdentifier) == 'Xamarin.tvOS'", ""
        | XamarinWatch -> "$(TargetFrameworkIdentifier) == 'Xamarin.watchOS'", ""
        | UAP(version) -> "$(TargetFrameworkIdentifier) == '.NETCore'", sprintf "$(TargetFrameworkVersion) == '%O'" version.NetCoreVersion
        | XamarinMac -> "$(TargetFrameworkIdentifier) == 'Xamarin.Mac'", ""
        | Native(NoBuildMode,NoPlatform) -> "true", ""
        | Native(NoBuildMode,bits) -> (sprintf "'$(Platform)'=='%s'" bits.AsString), ""
        | Native(profile,bits) -> (sprintf "'$(Configuration)|$(Platform)'=='%s|%s'" profile.AsString bits.AsString), ""
        | Tizen version ->"$(TargetFrameworkIdentifier) == 'Tizen'", sprintf "$(TargetFrameworkVersion) == '%O'" version
    | PortableProfile p -> sprintf "$(TargetFrameworkProfile) == '%O'" p.ProfileName,""

let getCondition (referenceCondition:string option) (allTargets: TargetProfile Set list) (targets : TargetProfile Set) =
    let inline CheckIfFullyInGroup typeName matchF filterRestF (processed,targets) =
        let fullyContained = 
            KnownTargetProfiles.AllDotNetProfiles 
            |> List.filter matchF
            |> List.forall (fun p -> targets |> Set.contains p)

        if fullyContained then
            (sprintf "$(TargetFrameworkIdentifier) == '%s'" typeName,"") :: processed,targets |> Set.filter (filterRestF >> not)
        else
            processed,targets
    let inline CheckIfFullyInGroupS typeName matchF (processed,targets) =
        CheckIfFullyInGroup typeName matchF matchF (processed,targets)
    let grouped,targets =
        ([],targets)
        |> CheckIfFullyInGroupS "true" (fun _ -> true)
        |> CheckIfFullyInGroupS ".NETFramework" (function SinglePlatform (DotNetFramework _) -> true | _ -> false)
        |> CheckIfFullyInGroup ".NETCore"  (function SinglePlatform (Windows _) -> true | _ -> false) (function SinglePlatform (Windows _) -> true | SinglePlatform (UAP _) -> true | _ -> false)
        |> CheckIfFullyInGroupS "Silverlight" (function SinglePlatform (Silverlight _) -> true | _ -> false)
        |> CheckIfFullyInGroupS "WindowsPhoneApp" (function SinglePlatform (WindowsPhoneApp _) -> true | _ -> false)
        |> CheckIfFullyInGroupS "WindowsPhone" (function SinglePlatform (WindowsPhone _) -> true | _ -> false)

    let conditions =
        if targets.Count = 1 && targets |> Set.minElement = SinglePlatform(Native(NoBuildMode,NoPlatform)) then 
            targets
        else 
            targets 
            |> Set.filter (function
                           | SinglePlatform(Native(NoBuildMode,NoPlatform)) -> false
                           | _ -> true)
        |> Seq.map getTargetCondition
        |> Seq.filter (fun (_, v) -> v <> "false")
        |> Seq.toList
        |> List.append grouped
        |> List.groupBy fst

    let conditionString =
        let andString = 
            conditions
            |> List.map (fun (group,conditions) ->
                match List.ofSeq (conditions |> Seq.map snd |> Set.ofSeq) with
                | [ "" ] -> group
                | [] -> "false"
                | [ detail ] -> sprintf "%s And %s" group detail
                | conditions ->
                    if conditions |> Seq.exists (String.IsNullOrEmpty) then
                        failwithf "Something went wrong (Details: probably in CheckIfFullyInGroup). Please open an issue."
                    let detail =
                        conditions
                        |> fun cs -> String.Join(" Or ",cs)
                        
                    sprintf "%s And (%s)" group detail)
        
        match andString with
        | [] -> ""
        | [x] -> x
        | xs -> String.Join(" Or ", List.map (fun cs -> sprintf "(%s)" cs) xs)
    
    match referenceCondition with 
    | None -> conditionString
    | Some condition ->
        // msbuild triggers a warning MSB4130 when we leave out the quotes around the condition
        // and add the condition at the end
        if conditionString = "$(TargetFrameworkIdentifier) == 'true'" || String.IsNullOrWhiteSpace conditionString then
            sprintf "'$(%s)' == 'True'" condition
        else
            sprintf "'$(%s)' == 'True' And (%s)" condition conditionString