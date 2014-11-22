module Paket.PlatformMatching

open System

let inline split (path : string) = 
    path.Split('+')
    |> Array.map (fun s -> s.Replace("portable-", ""))
    |> Array.toList
    
let inline extractPlatforms path = split path |> List.choose FrameworkIdentifier.Extract

let rec getPlatformPenalty (targetPlatform:FrameworkIdentifier) (packagePlatform:FrameworkIdentifier) =
    if packagePlatform = targetPlatform then
        0
    else
        let penalties = targetPlatform.SupportedPlatforms
                        |> List.map (fun target -> getPlatformPenalty target packagePlatform)
        List.min (1000::penalties) + 1

let getPathPenalty (path:string) (platform:FrameworkIdentifier) =
    if String.IsNullOrWhiteSpace path then
        999 // an empty path is considered compatible with every target, but with a high penalty so explicit paths are preferred
    else
        extractPlatforms path
        |> List.map (getPlatformPenalty platform)
        |> List.append [1000]
        |> List.min

// Checks wether a list of target platforms is supported by this path and with which penalty. 
let getPenalty (requiredPlatforms:FrameworkIdentifier list) (path:string) =
    requiredPlatforms
    |> List.map (getPathPenalty path)
    |> List.sum

let findBestMatch (paths : string list) (targetProfile : TargetProfile) = 
    let requiredPlatforms = 
        match targetProfile with
        | PortableProfile(_, platforms) -> platforms
        | SinglePlatform(platform) -> [ platform ]
    
    let pathPenalties = paths |> List.map (fun path -> (path, getPenalty requiredPlatforms path))
    
    let minPenalty = 
        pathPenalties
        |> List.map (fun (path, penalty) -> penalty)
        |> List.min

    pathPenalties
    |> List.filter (fun (path, penalty) -> penalty = minPenalty && minPenalty < 1000)
    |> List.map (fun (path, penalty) -> path)
    |> List.sortBy (fun path -> (extractPlatforms path).Length)
    |> List.tryFind (fun _ -> true)

// For a given list of paths and target profiles return tuples of paths with their supported target profiles.
// Every target profile will only be listed for own path - the one that best supports it. 
let getSupportedTargetProfiles (paths : string list) = 
    let test = TargetProfile.KnownTargetProfiles |> List.map (fun target -> ((findBestMatch paths target), target))
    TargetProfile.KnownTargetProfiles
    |> List.map (fun target -> ((findBestMatch paths target), target))
    |> List.collect (fun (path, target) -> 
           match path with
           | Some(p) -> [ (p, target) ]
           | _ -> [])
    |> Seq.groupBy (fun (path, target) -> path)
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
    match targets with
    | [ target ] -> getTargetCondition target
    | target :: rest -> getTargetCondition target + " Or " + getCondition rest
    | [] -> ""