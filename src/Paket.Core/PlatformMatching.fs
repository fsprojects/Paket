module Paket.PlatformMatching

open System

[<Literal>]
let MaxPenalty = 1000000

let inline split (path : string) = 
    path.Split('+')
    |> Array.map (fun s -> s.Replace("portable-", ""))
    
let inline extractPlatforms path = split path |> Array.choose FrameworkDetection.Extract

let private platformPenalties = System.Collections.Concurrent.ConcurrentDictionary<_,_>()

let rec getPlatformPenalty alreadyChecked (targetPlatform:FrameworkIdentifier) (packagePlatform:FrameworkIdentifier) =
    if packagePlatform = targetPlatform then
        0
    else
        let key = targetPlatform,packagePlatform
        match platformPenalties.TryGetValue key with
        | true, penalty -> penalty
        | _ ->
            let penalty =
                targetPlatform.SupportedPlatforms
                |> List.filter (fun x -> Set.contains x alreadyChecked |> not)
                |> List.map (fun target -> getPlatformPenalty (Set.add target alreadyChecked) target packagePlatform)
                |> List.append [MaxPenalty]
                |> List.min
                |> fun p -> p + 1

            let penalty =
                match targetPlatform, packagePlatform with
                | DotNetFramework _, DotNetStandard _ -> 200 + penalty
                | DotNetStandard _, DotNetFramework _ -> 200 + penalty
                | _ -> penalty

            platformPenalties.[key] <- penalty
            penalty

let private pathPenalties = System.Collections.Concurrent.ConcurrentDictionary<_,_>()

let getPathPenalty (path:string) (platform:FrameworkIdentifier) =
    if String.IsNullOrWhiteSpace path then
        match platform with
        | Native(_) -> MaxPenalty // an empty path is inconsidered compatible with native targets            
        | _ -> 10 // an empty path is considered compatible with every .NET target, but with a high penalty so explicit paths are preferred
    else
        let key = path,platform
        match pathPenalties.TryGetValue key with
        | true,penalty -> penalty
        | _ ->
            let penalty =
                extractPlatforms path
                |> Array.map (getPlatformPenalty Set.empty platform)
                |> Array.append [| MaxPenalty |]
                |> Array.min
            pathPenalties.[key] <- penalty
            penalty

// Checks wether a list of target platforms is supported by this path and with which penalty. 
let getPenalty (requiredPlatforms:FrameworkIdentifier list) (path:string) =
    requiredPlatforms
    |> List.sumBy (getPathPenalty path)

type PathPenalty = (string * int)

let comparePaths (p1 : PathPenalty) (p2 : PathPenalty) =
    let platformCount1 = (extractPlatforms (fst p1)).Length
    let platformCount2 = (extractPlatforms (fst p2)).Length

    // prefer full framework over portable
    if platformCount1 = 1 && platformCount2 > 1 then
        -1
    else if platformCount1 > 1 && platformCount2 = 1 then
        1
    // prefer lower version penalty
    else if snd p1 < snd p2 then
       -1
    else if snd p1 > snd p2 then
       1
    // prefer portable platform whith less platforms
    else if platformCount1 < platformCount2 then
        -1
    else if platformCount1 > platformCount2 then
        1
    else
        0

let rec findBestMatch (paths : #seq<string>) (targetProfile : TargetProfile) = 
    let requiredPlatforms = 
        match targetProfile with
        | PortableProfile(_, platforms) -> platforms
        | SinglePlatform(platform) -> [ platform ]

    match
        paths 
        |> Seq.map (fun path -> path, (getPenalty requiredPlatforms path))
        |> Seq.filter (fun (_, penalty) -> penalty < MaxPenalty)
        |> Seq.sortWith comparePaths
        |> Seq.map fst
        |> Seq.tryFind (fun _ -> true) with
    | None ->
        // Fallback Portable Library
        KnownTargetProfiles.AllProfiles
        |> Seq.choose (fun p ->
            match targetProfile with
            | SinglePlatform x ->
                if p.ProfilesCompatibleWithPortableProfile |> Seq.exists ((=) x)
                then findBestMatch paths p
                else None
            | _ -> None
        )
        |> Seq.sortBy (fun x -> (extractPlatforms x).Length) // prefer portable platform whith less platforms
        |> Seq.tryHead
    | path -> path

let private matchedCache = System.Collections.Concurrent.ConcurrentDictionary<_,_>()

// For a given list of paths and target profiles return tuples of paths with their supported target profiles.
// Every target profile will only be listed for own path - the one that best supports it. 
let getSupportedTargetProfiles (paths : string seq) =    
    let key = paths
    match matchedCache.TryGetValue key with
    | true, supportedFrameworks -> supportedFrameworks
    | _ ->
        let supportedFrameworks =
            KnownTargetProfiles.AllProfiles
            |> Seq.map (fun target -> findBestMatch paths target, target)
            |> Seq.collect (fun (path, target) -> 
                    match path with
                    | Some p -> [ p, target ]
                    | _ -> [])
            |> Seq.groupBy fst
            |> Seq.map (fun (path, group) -> path, Seq.map snd group)
            |> Map.ofSeq
        matchedCache.[key] <- supportedFrameworks
        supportedFrameworks


let getTargetCondition (target:TargetProfile) =
    match target with
    | SinglePlatform(platform) -> 
        match platform with
        | DotNetFramework(version) ->"$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DNX(version) ->"$(TargetFrameworkIdentifier) == 'DNX'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DNXCore(version) ->"$(TargetFrameworkIdentifier) == 'DNXCore'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | DotNetStandard(version) ->"$(TargetFrameworkIdentifier) == '.NETStandard'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | Windows(version) -> "$(TargetFrameworkIdentifier) == '.NETCore'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | Silverlight(version) -> "$(TargetFrameworkIdentifier) == 'Silverlight'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | WindowsPhoneApp(version) -> "$(TargetFrameworkIdentifier) == 'WindowsPhoneApp'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | WindowsPhoneSilverlight(version) -> "$(TargetFrameworkIdentifier) == 'WindowsPhone'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | MonoAndroid -> "$(TargetFrameworkIdentifier) == 'MonoAndroid'", ""
        | MonoTouch -> "$(TargetFrameworkIdentifier) == 'MonoTouch'", ""
        | MonoMac -> "$(TargetFrameworkIdentifier) == 'MonoMac'", ""
        | XamariniOS -> "$(TargetFrameworkIdentifier) == 'Xamarin.iOS'", ""
        | XamarinMac -> "$(TargetFrameworkIdentifier) == 'Xamarin.Mac'", ""
        | Native("","") -> "true", ""
        | Native("",bits) -> (sprintf "'$(Platform)'=='%s'" bits), ""
        | Runtimes(platform) -> failwithf "Runtime dependencies are unsupported in project files."
        | Native(profile,bits) -> (sprintf "'$(Configuration)|$(Platform)'=='%s|%s'" profile bits), ""
    | PortableProfile(name, _) -> sprintf "$(TargetFrameworkProfile) == '%O'" name,""

let getCondition (referenceCondition:string option) (targets : TargetProfile list) =
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
        |> CheckIfFullyInGroup ".NETFramework" (fun x -> match x with | SinglePlatform(DotNetFramework(_)) -> true | _ -> false)
        |> CheckIfFullyInGroup ".NETCore" (fun x -> match x with | SinglePlatform(Windows(_)) -> true | _ -> false)
        |> CheckIfFullyInGroup "Silverlight" (fun x -> match x with |SinglePlatform(Silverlight(_)) -> true | _ -> false)
        |> CheckIfFullyInGroup "WindowsPhoneApp" (fun x -> match x with | SinglePlatform(WindowsPhoneApp(_)) -> true | _ -> false)
        |> CheckIfFullyInGroup "WindowsPhone" (fun x -> match x with | SinglePlatform(WindowsPhoneSilverlight(_)) -> true | _ -> false)

    let conditions = 
        if targets = [ SinglePlatform(Native("", "")) ] then 
            targets
        else 
            targets 
            |> List.filter (function
                           | SinglePlatform(Native("", "")) -> false
                           | SinglePlatform(Runtimes(_)) -> false
                           | _ -> true)
        |> List.map getTargetCondition
        |> List.filter (fun (_, v) -> v <> "false")
        |> List.append grouped
        |> List.groupBy fst

    let conditionString =
        let andString = 
            conditions
            |> List.map (fun (group,conditions) ->
                match List.ofSeq conditions with
                | [ _,"" ] -> group
                | [ _,detail ] -> sprintf "%s And %s" group detail
                | [] -> "false"
                | conditions ->
                    let detail =
                        conditions
                        |> List.map snd
                        |> Set.ofSeq
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
        if conditionString = "$(TargetFrameworkIdentifier) == 'true'" then
            sprintf "'$(%s)' == 'True'" condition
        else
            sprintf "'$(%s)' == 'True' And (%s)" condition conditionString