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

//let knownInPortable =
//  KnownTargetProfiles.AllPortableProfiles 
//  |> List.collect snd
//  |> List.distinct

//let tryGetProfile platforms =
//    let filtered =
//      platforms.Platforms
//      |> List.filter (fun p -> knownInPortable |> Seq.exists ((=) p))
//      |> List.sort

//    KnownTargetProfiles.AllPortableProfiles |> Seq.tryFind (snd >> (=) filtered)
//    |> Option.map PortableProfile

// TODO: In future work this stuff should be rewritten. This penalty stuff is more random than a proper implementation.
let rec getPlatformPenalty =
    memoize (fun (targetPlatform:TargetProfile,packagePlatform:TargetProfile) ->
    //let rec getPlatformPenalty alreadyChecked (targetPlatform:TargetProfile) (packagePlatform:TargetProfile) =
        if packagePlatform = targetPlatform then
            0
        else
            match targetPlatform, packagePlatform with
            // There is no point in searching for frameworks in portables...
            | PortableProfile _, SinglePlatform _ -> MaxPenalty
            | _ ->
                let penalty =
                    targetPlatform.SupportedPlatforms
                    //|> List.filter (fun x ->
                    //    System.Diagnostics.Debug.WriteLine(sprintf "Supported %O -> %O (trying to find %O)" targetPlatform x packagePlatform)
                    //    Set.contains x alreadyChecked |> not)
                    |> List.map (fun target ->
                        //System.Diagnostics.Debug.WriteLine(sprintf "Supported %O -> %O (trying to find %O)" targetPlatform target packagePlatform)
                        getPlatformPenalty (target, packagePlatform))
                    |> List.append [MaxPenalty]
                    |> List.min
                    |> fun p -> p + 1

                match targetPlatform, packagePlatform with
                | SinglePlatform (DotNetFramework _), SinglePlatform (DotNetStandard _) -> 200 + penalty
                | SinglePlatform (DotNetStandard _), SinglePlatform(DotNetFramework _) -> 200 + penalty
                | SinglePlatform _, PortableProfile _ -> 500 + penalty
                | PortableProfile _, SinglePlatform _ -> 500 + penalty
                | _ -> penalty)

let getFrameworkPenalty (fr1, fr2) =
    getPlatformPenalty (SinglePlatform fr1, SinglePlatform fr2)


let getPathPenalty =
    memoize 
      (fun (path:ParsedPlatformPath,platform:TargetProfile) ->
        let handleEmpty () =
            match platform with
            | SinglePlatform(Native(_)) -> MaxPenalty // an empty path is considered incompatible with native targets            
            | _ -> 2000 // an empty path is considered compatible with every .NET target, but with a high penalty so explicit paths are preferred
        match path.Platforms with
        | _ when String.IsNullOrWhiteSpace path.Name -> handleEmpty()
        | [] -> handleEmpty()
        | [ h ] -> getPlatformPenalty(platform,SinglePlatform h)
        | _ ->
            getPlatformPenalty(platform, TargetProfile.FindPortable path.Platforms))

[<Obsolete("Used in test code, use getPathPenalty instead.")>]
let getFrameworkPathPenalty fr path =
    match fr with
    | [ h ] -> getPathPenalty (path, SinglePlatform h)
    | _ ->
        getPathPenalty (path, TargetProfile.FindPortable fr)

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
    let rec loop (acc:TargetProfile list) (framework:TargetProfile) (profls:TargetProfile list) =
        match profls with 
        | [] -> acc 
        | f::tl -> 
            if f.SupportedPlatforms |> List.exists ((=) framework) 
            then loop (f::acc) framework tl 
            else loop acc framework tl 
        //| _::tl -> loop acc framework tl
    memoize (fun (framework,profls) -> loop ([]:TargetProfile list) framework profls)

let getPlatformsSupporting =
    // http://nugettoolsdev.azurewebsites.net
    let calculate (x:TargetProfile) =
        KnownTargetProfiles.AllProfiles
        //|> List.collect(function
        //    | SinglePlatform p -> [p]
        //    | PortableProfile (_, fws) -> fws)
        //|> List.distinct
        |> List.filter (fun plat -> plat.SupportedPlatformsTransitive |> Seq.contains x)
        //match x with
        //| PortableProfile (name,fws) ->
        //    // Portables can be supported by other portables and by all fws given.
        //    KnownTargetProfiles.AllProfiles
        //    |> List.filter (function
        //        | PortableProfile (otherName,otherfws) ->
        //            // the other portable profile is supported, when the transitive supported set is a superset
        //            let currentSupportedFrameworks =
        //                fws
        //                |> List.collect (fun fw -> fw.SupportedPlatformsTransitive)
        //                |> List.distinct
        //                |> List.sort
        //            let otherSupportedFrameworks =
        //                otherfws
        //                |> List.collect (fun fw -> fw.SupportedPlatformsTransitive)
        //                |> List.distinct
        //                |> List.sort
        //
        //            // take if the other profile supports all frameworks we support
        //            currentSupportedFrameworks
        //            |> Seq.forall (fun fw -> otherSupportedFrameworks |> Seq.exists ((=) fw))
        //        | SinglePlatform otherfw ->
        //            // otherfw is supported if it is supported by of any fw in fws
        //            fws
        //            |> Seq.exists (fun fw -> fw.SupportedPlatformsTransitive |> Seq.contains otherfw))
        //| SinglePlatform tf ->
        //    // SinglePlatforms are only supported by other SinglePlatforms
        //    getFrameworksSupporting tf
        //    |> List.map SinglePlatform
    memoize calculate

let platformsSupport = 
    let rec platformsSupport platform platforms = 
        if List.isEmpty platforms then MaxPenalty
        elif platforms |> List.exists ((=) platform) then 1
        else 
            platforms |> Array.ofList 
            |> Array.Parallel.map (fun (p : TargetProfile) -> 
                collectPlatforms (p,KnownTargetProfiles.AllProfiles)
            ) |> List.concat
            |> platformsSupport platform |> (+) 1
    memoize (fun (platform,platforms) -> platformsSupport platform platforms)


let findBestMatch = 
    let rec findBestMatch (paths : ParsedPlatformPath list, targetProfile : TargetProfile) = 

        //let supported =
            paths
            |> List.map (fun path -> path, (getPathPenalty (path, targetProfile)))
            |> List.filter (fun (_, penalty) -> penalty < MaxPenalty)
            |> List.sortWith comparePaths
            |> List.map fst
            |> List.tryHead

        //let findBestPortableMatch findPenalty (portableProfile:TargetProfile) paths =
        //    paths
        //    |> Seq.tryFind (fun p -> tryGetProfile p = Some portableProfile)
        //    |> Option.map (fun p -> p, findPenalty)
        //
        //match supported with
        //| None ->
        //    // Fallback Portable Library
        //    KnownTargetProfiles.AllProfiles
        //    |> List.choose (fun p ->
        //        match targetProfile with
        //        | SinglePlatform x ->
        //            match platformsSupport(x,p.ProfilesCompatibleWithPortableProfile) with
        //            | pen when pen < MaxPenalty ->
        //                findBestPortableMatch pen p paths
        //            | _ -> 
        //                None
        //        | _ -> None)
        //    |> List.distinct
        //    |> List.sortBy (fun (x, pen) -> pen, x.Platforms.Length) // prefer portable platform with less platforms
        //    |> List.map fst
        //    |> List.tryHead
        //| path -> path

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
        //| DotNetFramework(version) when version = FrameworkVersion.V4 ->
        //    "$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "($(TargetFrameworkVersion) == '%O')" version
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
        | WindowsPhone(version) -> "$(TargetFrameworkIdentifier) == 'WindowsPhone'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | MonoAndroid -> "$(TargetFrameworkIdentifier) == 'MonoAndroid'", ""
        | MonoTouch -> "$(TargetFrameworkIdentifier) == 'MonoTouch'", ""
        | MonoMac -> "$(TargetFrameworkIdentifier) == 'MonoMac'", ""
        | XamariniOS -> "$(TargetFrameworkIdentifier) == 'Xamarin.iOS'", ""
        | UAP(version) ->"$(TargetPlatformIdentifier) == 'UAP'", sprintf "$(TargetPlatformVersion.StartsWith('%O'))" version
        | XamarinMac -> "$(TargetFrameworkIdentifier) == 'Xamarin.Mac'", ""
        | Native(NoBuildMode,NoPlatform) -> "true", ""
        | Native(NoBuildMode,bits) -> (sprintf "'$(Platform)'=='%s'" bits.AsString), ""
        | Native(profile,bits) -> (sprintf "'$(Configuration)|$(Platform)'=='%s|%s'" profile.AsString bits.AsString), ""
    | PortableProfile p -> sprintf "$(TargetFrameworkProfile) == '%O'" p.ProfileName,""

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
        |> CheckIfFullyInGroup "WindowsPhone" (function SinglePlatform (WindowsPhone _) -> true | _ -> false)

    //let targets =
    //    targets 
    //    |> List.map (fun target ->
    //        match target with
    //        | SinglePlatform(DotNetFramework(FrameworkVersion.V4_Client)) ->
    //            if allTargets |> List.exists (List.contains (SinglePlatform(DotNetFramework(FrameworkVersion.V4)))) |> not then
    //                SinglePlatform(DotNetFramework(FrameworkVersion.V4))
    //            else
    //                target
    //        | _ -> target)

    let conditions =
        if targets = [ SinglePlatform(Native(NoBuildMode,NoPlatform)) ] then 
            targets
        else 
            targets 
            |> List.filter (function
                           | SinglePlatform(Native(NoBuildMode,NoPlatform)) -> false
                           //| SinglePlatform(DotNetFramework(FrameworkVersion.V4)) ->
                           //     targets |> List.contains (SinglePlatform(DotNetFramework(FrameworkVersion.V4))) |> not
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