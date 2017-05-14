module Paket.PlatformMatching

open System
open ProviderImplementation.AssemblyReader.Utils.SHA1

[<Literal>]
let MaxPenalty = 1000000

type ParsedPlatformPath =
  { Name : string
    Platforms : FrameworkIdentifier list }
  static member Empty = { Name = ""; Platforms = [] }

let inline split (path : string) =
    path.Split('+')
    |> Array.map (fun s -> System.Text.RegularExpressions.Regex.Replace(s, "portable\\d*-",""))

let extractPlatforms = memoize (fun path -> { Name = path; Platforms = split path |> Array.choose FrameworkDetection.Extract |> Array.toList })

let knownInPortable =
  KnownTargetProfiles.AllPortableProfiles 
  |> List.collect snd
  |> List.distinct

let tryGetProfile platforms =
    let filtered =
      platforms.Platforms
      |> List.filter (fun p -> knownInPortable |> Seq.exists ((=) p))
      |> List.sort

    KnownTargetProfiles.AllPortableProfiles |> Seq.tryFind (snd >> (=) filtered)
    |> Option.map PortableProfile

let getPlatformPenalty =
    let rec getPlatformPenalty alreadyChecked (targetPlatform:FrameworkIdentifier) (packagePlatform:FrameworkIdentifier) =
        if packagePlatform = targetPlatform then
            0
        else
            let penalty =
                targetPlatform.SupportedPlatforms
                |> List.filter (fun x -> Set.contains x alreadyChecked |> not)
                |> List.map (fun target -> getPlatformPenalty (Set.add target alreadyChecked) target packagePlatform)
                |> List.append [MaxPenalty]
                |> List.min
                |> fun p -> p + 1

            match targetPlatform, packagePlatform with
            | DotNetFramework _, DotNetStandard _ -> 200 + penalty
            | DotNetStandard _, DotNetFramework _ -> 200 + penalty
            | _ -> penalty

    memoize (fun (targetPlatform:FrameworkIdentifier,packagePlatform:FrameworkIdentifier) -> getPlatformPenalty Set.empty targetPlatform packagePlatform)

let getPathPenalty =
    memoize 
      (fun (path:ParsedPlatformPath,platform:FrameworkIdentifier) ->
        if String.IsNullOrWhiteSpace path.Name then
            match platform with
            | Native(_) -> MaxPenalty // an empty path is considered incompatible with native targets            
            | _ -> 500 // an empty path is considered compatible with every .NET target, but with a high penalty so explicit paths are preferred
        else
            path.Platforms
            |> List.map (fun target -> getPlatformPenalty(platform,target))
            |> List.append [ MaxPenalty ]
            |> List.min)

// Checks wether a list of target platforms is supported by this path and with which penalty. 
let getPenalty (requiredPlatforms:FrameworkIdentifier list) (path:ParsedPlatformPath) =
    requiredPlatforms
    |> List.sumBy (fun p -> getPathPenalty(path,p))

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
    let rec loop (acc:FrameworkIdentifier list) (framework:FrameworkIdentifier) (profls:TargetProfile list) =
        match profls with 
        | [] -> acc 
        | (SinglePlatform f)::tl -> 
            if f.SupportedPlatforms |> List.exists ((=) framework) 
            then loop (f::acc) framework tl 
            else loop acc framework tl 
        | _::tl -> loop acc framework tl
    memoize (fun (framework,profls) -> loop ([]:FrameworkIdentifier list) framework profls)

/// Returns all framework identifiers which (transitively) support the given framework identifier
let getFrameworksSupporting =
    let cache = System.Collections.Concurrent.ConcurrentDictionary<_,FrameworkIdentifier list>()
    fun (x:FrameworkIdentifier) ->
        cache.GetOrAdd(x, fun _ ->
            // calculate
            KnownTargetProfiles.AllProfiles
            |> List.collect(function
                | SinglePlatform p -> [p]
                | PortableProfile (_, fws) -> fws)
            |> List.distinct
            |> List.filter (fun fw -> fw.SupportedPlatformsTransitive |> Seq.contains x)
            )

let platformsSupport = 
    let rec platformsSupport platform platforms = 
        if List.isEmpty platforms then MaxPenalty
        elif platforms |> List.exists ((=) platform) then 1
        else 
            platforms |> Array.ofList 
            |> Array.Parallel.map (fun (p : FrameworkIdentifier) -> 
                collectPlatforms (p,KnownTargetProfiles.AllProfiles)
            ) |> List.concat
            |> platformsSupport platform |> (+) 1
    memoize (fun (platform,platforms) -> platformsSupport platform platforms)


let findBestMatch = 
    let rec findBestMatch (paths : ParsedPlatformPath list, targetProfile : TargetProfile) = 
        let requiredPlatforms = 
            match targetProfile with
            | PortableProfile(_, platforms) -> platforms
            | SinglePlatform(platform) -> [ platform ]

        let supported =
            paths
            |> List.map (fun path -> path, (getPenalty requiredPlatforms path))
            |> List.filter (fun (_, penalty) -> penalty < MaxPenalty)
            |> List.sortWith comparePaths
            |> List.map fst
            |> List.tryHead

        let findBestPortableMatch findPenalty (portableProfile:TargetProfile) paths =
            paths
            |> Seq.tryFind (fun p -> tryGetProfile p = Some portableProfile)
            |> Option.map (fun p -> p, findPenalty)

        match supported with
        | None ->
            // Fallback Portable Library
            KnownTargetProfiles.AllProfiles
            |> List.choose (fun p ->
                match targetProfile with
                | SinglePlatform x ->
                    match platformsSupport(x,p.ProfilesCompatibleWithPortableProfile) with
                    | pen when pen < MaxPenalty ->
                        findBestPortableMatch pen p paths
                    | _ -> 
                        None
                | _ -> None)
            |> List.distinct
            |> List.sortBy (fun (x, pen) -> pen, x.Platforms.Length) // prefer portable platform with less platforms
            |> List.map fst
            |> List.tryHead
        | path -> path

    memoize (fun (paths : ParsedPlatformPath list,targetProfile : TargetProfile) -> findBestMatch(paths,targetProfile))

// For a given list of paths and target profiles return tuples of paths with their supported target profiles.
// Every target profile will only be listed for own path - the one that best supports it. 
let getSupportedTargetProfiles =    
    memoize 
        (fun (paths : ParsedPlatformPath list) ->
            KnownTargetProfiles.AllProfiles
            |> List.choose (fun target ->
                match findBestMatch(paths,target) with
                | Some p -> Some(p, target)
                | _ -> None)
            |> List.groupBy fst
            |> List.map (fun (path, group) -> path, List.map snd group)
            |> Map.ofList)


let getTargetCondition (target:TargetProfile) =
    match target with
    | SinglePlatform(platform) ->
        // BUG: Pattern incomplete!
        match platform with
        | DotNetFramework(version) when version = FrameworkVersion.V4_Client ->
            "$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "($(TargetFrameworkVersion) == '%O' And $(TargetFrameworkProfile) == 'Client')" version
        | DotNetFramework(version) ->"$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DNX(version) ->"$(TargetFrameworkIdentifier) == 'DNX'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DNXCore(version) ->"$(TargetFrameworkIdentifier) == 'DNXCore'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DotNetStandard(version) ->"$(TargetFrameworkIdentifier) == '.NETStandard'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DotNetCore(version) ->"$(TargetFrameworkIdentifier) == '.NETCoreApp'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DotNetUnity(version) when version = DotNetUnityVersion.V3_5_Full ->
            "$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "($(TargetFrameworkVersion) == '%O' And $(TargetFrameworkProfile) == 'Unity Full v3.5')" version
        | DotNetUnity(version) when version = DotNetUnityVersion.V3_5_Subset ->
            "$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "($(TargetFrameworkVersion) == '%O' And $(TargetFrameworkProfile) == 'Unity Subset v3.5')" version
        | DotNetUnity(version) when version = DotNetUnityVersion.V3_5_Micro ->
            "$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "($(TargetFrameworkVersion) == '%O' And $(TargetFrameworkProfile) == 'Unity Micro v3.5')" version
        | DotNetUnity(version) when version = DotNetUnityVersion.V3_5_Web ->
            "$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "($(TargetFrameworkVersion) == '%O' And $(TargetFrameworkProfile) == 'Unity Web v3.5')" version               
        | Windows(version) -> "$(TargetFrameworkIdentifier) == '.NETCore'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | Silverlight(version) -> "$(TargetFrameworkIdentifier) == 'Silverlight'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | WindowsPhoneApp(version) -> "$(TargetFrameworkIdentifier) == 'WindowsPhoneApp'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | WindowsPhoneSilverlight(version) -> "$(TargetFrameworkIdentifier) == 'WindowsPhone'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | MonoAndroid -> "$(TargetFrameworkIdentifier) == 'MonoAndroid'", ""
        | MonoTouch -> "$(TargetFrameworkIdentifier) == 'MonoTouch'", ""
        | MonoMac -> "$(TargetFrameworkIdentifier) == 'MonoMac'", ""
        | XamariniOS -> "$(TargetFrameworkIdentifier) == 'Xamarin.iOS'", ""
        | UAP(version) ->"$(TargetPlatformIdentifier) == 'UAP'", sprintf "$(TargetPlatformVersion.StartsWith('%O'))" version
        | XamarinMac -> "$(TargetFrameworkIdentifier) == 'Xamarin.Mac'", ""
        | Native(NoBuildMode,NoPlatform) -> "true", ""
        | Native(NoBuildMode,bits) -> (sprintf "'$(Platform)'=='%s'" bits.AsString), ""
        | Native(profile,bits) -> (sprintf "'$(Configuration)|$(Platform)'=='%s|%s'" profile.AsString bits.AsString), ""
    | PortableProfile(name, _) -> sprintf "$(TargetFrameworkProfile) == '%O'" name,""

let getCondition (referenceCondition:string option) (allTargets: TargetProfile list list) (targets : TargetProfile list) =
    let inline CheckIfFullyInGroup typeName matchF (processed,targets) =
        let fullyContained = 
            KnownTargetProfiles.AllDotNetProfiles 
            |> List.filter matchF
            |> List.forall (fun p -> targets |> Seq.exists ((=) p))

        if fullyContained then
            (sprintf "$(TargetFrameworkIdentifier) == '%s'" typeName,"") :: processed,targets |> List.filter (matchF >> not)
        else
            processed,targets

    let grouped,targets =
        ([],targets)
        |> CheckIfFullyInGroup "true" (fun _ -> true)
        |> CheckIfFullyInGroup ".NETFramework" (function SinglePlatform (DotNetFramework _) -> true | _ -> false)
        |> CheckIfFullyInGroup ".NETCore" (function SinglePlatform (Windows _) -> true | _ -> false)
        |> CheckIfFullyInGroup "Silverlight" (function SinglePlatform (Silverlight _) -> true | _ -> false)
        |> CheckIfFullyInGroup "WindowsPhoneApp" (function SinglePlatform (WindowsPhoneApp _) -> true | _ -> false)
        |> CheckIfFullyInGroup "WindowsPhone" (function SinglePlatform (WindowsPhoneSilverlight _) -> true | _ -> false)

    let targets =
        targets 
        |> List.map (fun target ->
            match target with
            | SinglePlatform(DotNetFramework(FrameworkVersion.V4_Client)) ->
                if allTargets |> List.exists (List.contains (SinglePlatform(DotNetFramework(FrameworkVersion.V4)))) |> not then
                    SinglePlatform(DotNetFramework(FrameworkVersion.V4))
                else
                    target
            | _ -> target)

    let conditions =
        if targets = [ SinglePlatform(Native(NoBuildMode,NoPlatform)) ] then 
            targets
        else 
            targets 
            |> List.filter (function
                           | SinglePlatform(Native(NoBuildMode,NoPlatform)) -> false
                           | SinglePlatform(DotNetFramework(FrameworkVersion.V4_Client)) ->
                                targets |> List.contains (SinglePlatform(DotNetFramework(FrameworkVersion.V4))) |> not
                           | _ -> true)
        |> List.map getTargetCondition
        |> List.filter (fun (_, v) -> v <> "false")
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