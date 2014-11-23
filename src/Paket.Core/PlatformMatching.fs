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
        | DotNetFramework(version) -> sprintf "$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == '%O'" version
        | Windows(version) -> sprintf "$(TargetFrameworkIdentifier) == '.NETCore' And $(TargetFrameworkVersion) == '%O'" version
        | Silverlight(version) -> sprintf "$(TargetFrameworkIdentifier) == 'Silverlight' And $(TargetFrameworkVersion) == '%O'" version
        | WindowsPhoneApp(version) -> sprintf "$(TargetFrameworkIdentifier) == 'WindowsPhoneApp' And $(TargetFrameworkVersion) == '%O'" version
        | WindowsPhoneSilverlight(version) -> sprintf "$(TargetFrameworkIdentifier) == 'WindowsPhone' And $(TargetFrameworkVersion) == '%O'" version
        | MonoAndroid | MonoTouch -> "false" // should be covered by the .NET case above
    | PortableProfile(name, _) -> sprintf "$(TargetFrameworkProfile) == '%O'" name

let rec getCondition (targets : TargetProfile list) =
    let conditions = List.map getTargetCondition targets
    
     
    match conditions with
    | [ condition ] -> condition
    | [] -> ""
    | conditions -> 
        conditions
        |> List.map (fun c -> sprintf "(%s)" c)
        |> fun cs-> String.Join(" Or ",cs)    