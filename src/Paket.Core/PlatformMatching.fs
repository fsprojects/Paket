module Paket.PlatformMatching

open System

[<Literal>]
let maxPenalty = 1000

let inline split (path : string) = 
    path.Split('+')
    |> Array.map (fun s -> s.Replace("portable-", ""))
    
let inline extractPlatforms path = split path |> Array.choose FrameworkIdentifier.Extract

let rec getPlatformPenalty (targetPlatform:FrameworkIdentifier) (packagePlatform:FrameworkIdentifier) =
    if packagePlatform = targetPlatform then
        0
    else
        targetPlatform.SupportedPlatforms
        |> List.map (fun target -> getPlatformPenalty target packagePlatform)
        |> List.append [maxPenalty]
        |> List.min
        |> fun p -> p + 1

let getPathPenalty (path:string) (platform:FrameworkIdentifier) =
    if String.IsNullOrWhiteSpace path then
        // an empty path is considered compatible with every target, but with a high penalty so explicit paths are preferred
        maxPenalty - 1 
    else
        extractPlatforms path
        |> Array.map (getPlatformPenalty platform)
        |> Array.append [| maxPenalty |]
        |> Array.min

// Checks wether a list of target platforms is supported by this path and with which penalty. 
let getPenalty (requiredPlatforms:FrameworkIdentifier list) (path:string) =
    requiredPlatforms
    |> List.sumBy (getPathPenalty path)

let findBestMatch (paths : string list) (targetProfile : TargetProfile) = 
    let requiredPlatforms = 
        match targetProfile with
        | PortableProfile(_, platforms) -> platforms
        | SinglePlatform(platform) -> [ platform ]
    
    let pathPenalties = 
        paths 
        |> List.map (fun path -> (path, getPenalty requiredPlatforms path))
    
    let minPenalty = 
        pathPenalties
        |> Seq.map snd
        |> Seq.min

    pathPenalties
    |> Seq.filter (fun (path, penalty) -> penalty = minPenalty && minPenalty < maxPenalty)
    |> Seq.map fst
    |> Seq.sortBy (fun path -> (extractPlatforms path).Length)
    |> Seq.tryFind (fun _ -> true)

// For a given list of paths and target profiles return tuples of paths with their supported target profiles.
// Every target profile will only be listed for own path - the one that best supports it. 
let getSupportedTargetProfiles (paths : string list) =     
    TargetProfile.KnownTargetProfiles
    |> Seq.map (fun target -> findBestMatch paths target, target)
    |> Seq.collect (fun (path, target) -> 
           match path with
           | Some(p) -> [ (p, target) ]
           | _ -> [])
    |> Seq.groupBy fst
    |> Seq.map (fun (path, group) -> (path, Seq.map (fun (_, target) -> target) group))
    |> Map.ofSeq

let getTargetCondition (target:TargetProfile) =
    match target with
    | SinglePlatform(platform) -> 
        match platform with
        | DotNetFramework(version) ->"$(TargetFrameworkIdentifier) == '.NETFramework'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | Windows(version) -> "$(TargetFrameworkIdentifier) == '.NETCore'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | Silverlight(version) -> "$(TargetFrameworkIdentifier) == 'Silverlight'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | WindowsPhoneApp(version) -> "$(TargetFrameworkIdentifier) == 'WindowsPhoneApp", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | WindowsPhoneSilverlight(version) -> "$(TargetFrameworkIdentifier) == 'WindowsPhone'", sprintf "$(TargetFrameworkVersion) == '%O'" version
        | MonoAndroid | MonoTouch -> "","false" // should be covered by the .NET case above
    | PortableProfile(name, _) -> sprintf "$(TargetFrameworkProfile) == '%O'" name,""

let getCondition (targets : TargetProfile list) =
    let inline CheckIfFullyInGroup typeName matchF (processed,targets) =
        let inline filter target =
            match target with 
            | SinglePlatform(x) ->  matchF x
            | _ -> false

        let fullyContained = 
            TargetProfile.KnownTargetProfiles 
            |> List.filter filter
            |> List.forall (fun p -> targets |> Seq.exists ((=) p))

        if fullyContained then
            (sprintf "$(TargetFrameworkIdentifier) == '%s'" typeName,"") :: processed,targets |> List.filter (filter >> not)
        else
            processed,targets

    let grouped,targets =
        ([],targets)
        |> CheckIfFullyInGroup ".NETFramework" (fun x -> match x with | DotNetFramework(_) -> true | _ -> false)
        |> CheckIfFullyInGroup ".NETCore" (fun x -> match x with | Windows(_) -> true | _ -> false)
        |> CheckIfFullyInGroup "Silverlight" (fun x -> match x with | Silverlight(_) -> true | _ -> false)
        |> CheckIfFullyInGroup "WindowsPhoneApp" (fun x -> match x with | WindowsPhoneApp(_) -> true | _ -> false)
        |> CheckIfFullyInGroup "WindowsPhone" (fun x -> match x with | WindowsPhoneSilverlight(_) -> true | _ -> false)

    let conditions = 
        targets
        |> List.map getTargetCondition
        |> List.filter (fun (_,v) -> v <> "false")
        |> List.append grouped
        |> Seq.groupBy fst

    conditions
    |> Seq.map (fun (group,conditions) ->
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
    |> Seq.toList
    |> fun l -> 
            match l with
            | [] -> ""
            | [x] -> x
            | xs -> String.Join(" Or ", List.map (fun cs -> sprintf "(%s)" cs) xs)