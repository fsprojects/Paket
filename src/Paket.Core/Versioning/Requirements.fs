module Paket.Requirements

open System
open Paket
open Paket.Domain
open Paket.PackageSources
open Paket.Logging

[<RequireQualifiedAccess>]
type FrameworkRestriction = 
| Exactly of FrameworkIdentifier
| Portable of string
| AtLeast of FrameworkIdentifier
| Between of FrameworkIdentifier * FrameworkIdentifier
    
    override this.ToString() =
        match this with
        | FrameworkRestriction.Exactly r -> r.ToString()
        | FrameworkRestriction.Portable r -> r
        | FrameworkRestriction.AtLeast r -> ">= " + r.ToString()
        | FrameworkRestriction.Between(min,max) -> sprintf ">= %O < %O" min max

    // TODO: remove broken api
    member x.GetOneIdentifier =
        match x with
        | Exactly r -> Some r
        | Portable _ -> None
        | AtLeast r -> Some r
        | Between(r, _) -> Some r

    // TODO: remove broken api
    /// Return if the parameter is a restriction of the same framework category (dotnet, windows phone, silverlight, ...)
    member x.IsSameCategoryAs (y : FrameworkRestriction) =
        match (x.GetOneIdentifier, y.GetOneIdentifier) with
        | Some r, Some r' -> Some(r.IsSameCategoryAs r')
        | _ -> None

type FrameworkRestrictions = 
| FrameworkRestrictionList of FrameworkRestriction list
| AutoDetectFramework

let getRestrictionList (frameworkRestrictions:FrameworkRestrictions) =
    match frameworkRestrictions with 
    | FrameworkRestrictionList list -> list
    | AutoDetectFramework -> failwith "The framework restriction could not be determined."

let parseRestrictions failImmediatly (text:string) =
    let handleError =
        if failImmediatly then
            failwith
        else
            if verbose then
                (fun s ->
                    traceError s
                    traceVerbose Environment.StackTrace)
            else traceError
    let text =
        // workaround missing spaces
        text.Replace("<=","<= ").Replace(">=",">= ").Replace("=","= ")

    let commaSplit = text.Trim().Split(',')
    [for p in commaSplit do
        let operatorSplit = p.Trim().Split([|' '|],StringSplitOptions.RemoveEmptyEntries)
        let framework =
            if operatorSplit.Length < 2 then 
                operatorSplit.[0] 
            else 
                operatorSplit.[1]


        match FrameworkDetection.Extract(framework) with
        | None -> 
                if (PlatformMatching.extractPlatforms framework).Platforms |> List.isEmpty |> not then
                    yield FrameworkRestriction.Portable framework
                else
                    handleError <| sprintf "Could not parse framework '%s'. Try to update or install again or report a paket bug." framework
        | Some x -> 
            if operatorSplit.[0] = ">=" then
                if operatorSplit.Length < 4 then
                    yield FrameworkRestriction.AtLeast x
                else
                    let item = operatorSplit.[3]
                    match FrameworkDetection.Extract(item) with
                    | None ->
                        handleError <| sprintf "Could not parse second framework of between operator '%s'. Try to update or install again or report a paket bug." item
                    | Some y -> yield FrameworkRestriction.Between(x,y)
            else
                yield FrameworkRestriction.Exactly x]

// TODO: remove broken api
let private minRestriction = FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V1))

// TODO: remove broken api
let private minStandardRestriction = FrameworkRestriction.Exactly(DotNetStandard(DotNetStandardVersion.V1_0))

// TODO: remove broken api
let findMaxDotNetRestriction restrictions =
    minRestriction :: restrictions
    |> List.filter (fun (r:FrameworkRestriction) ->
        match r with
        | FrameworkRestriction.Exactly(DotNetFramework _)  -> true
        | _ -> false)
    |> List.max
    |> fun r ->
        match r with
        | FrameworkRestriction.Exactly r -> r
        | _ -> failwith "error"

// TODO: remove broken api
let findMaxStandardRestriction restrictions =
    minStandardRestriction :: restrictions
    |> List.filter (fun (r:FrameworkRestriction) ->
        match r with
        | FrameworkRestriction.Exactly(DotNetStandard _)  -> true
        | _ -> false)
    |> List.max
    |> fun r ->
        match r with
        | FrameworkRestriction.Exactly r -> r
        | _ -> failwith "error"

let rec optimizeRestrictions restrictions = 
    let sorting xs =
        xs
        |> List.sortBy (fun x ->
            match x with
            | FrameworkRestriction.Exactly r -> r
            | FrameworkRestriction.Portable r -> FrameworkIdentifier.MonoMac
            | FrameworkRestriction.AtLeast r -> r
            | FrameworkRestriction.Between(min,max) -> min
            |> fun y -> y,x)
            
    match sorting restrictions |> List.distinct with
    | [] -> []
    | [x] -> [x]
    | odered ->
        let newRestrictions =
            match odered |> Seq.tryFind (function | FrameworkRestriction.AtLeast r -> true | _ -> false) with
            | Some((FrameworkRestriction.AtLeast(DotNetFramework(v)) as r)) ->
                odered
                |> List.filter (fun r' ->
                    match r' with
                    | FrameworkRestriction.Exactly(DotNetFramework(x)) when x > v -> false
                    | FrameworkRestriction.AtLeast(DotNetFramework(x)) when x > v -> false
                    | _ -> true)
            | Some((FrameworkRestriction.AtLeast(DotNetStandard(v)) as r)) ->
                odered
                |> List.filter (fun r' ->
                    match r' with
                    | FrameworkRestriction.Exactly(DotNetStandard(x)) when x > v -> false
                    | FrameworkRestriction.AtLeast(DotNetStandard(x)) when x > v -> false
                    | _ -> true)
            | _ -> odered

        let filtered =
            match newRestrictions |> Seq.rev |> Seq.tryFind (function | FrameworkRestriction.AtLeast(DotNetFramework r) -> true | FrameworkRestriction.AtLeast(DotNetStandard r) -> true | _ -> false) with
            | None -> newRestrictions
            | Some r ->
                let currentVersion =
                    match r with
                    | FrameworkRestriction.AtLeast(DotNetFramework(x)) -> DotNetFramework x
                    | FrameworkRestriction.AtLeast(DotNetStandard(x)) -> DotNetStandard x
                    | x -> failwithf "Unknown .NET moniker %O" x
                                                                                                           
                let isLowerVersion (currentVersion:FrameworkIdentifier) compareVersion =
                    let isMatching (x:FrameworkIdentifier) =
                        if x = DotNetFramework FrameworkVersion.V3_5 && currentVersion = DotNetFramework FrameworkVersion.V4 then true else
                        if x = DotNetFramework FrameworkVersion.V3_5 && currentVersion = DotNetFramework FrameworkVersion.V4_Client then true else
                        if x = DotNetFramework FrameworkVersion.V4_Client && currentVersion = DotNetFramework FrameworkVersion.V4_5 then true else
                        let hasFrameworksBetween = 
                            KnownTargetProfiles.DotNetFrameworkVersions 
                            |> Seq.exists (fun p -> DotNetFramework p > x && DotNetFramework p < currentVersion)
                        let hasStandardsBetween = 
                            KnownTargetProfiles.DotNetStandardVersions 
                            |> Seq.exists (fun p -> DotNetStandard p > x && DotNetStandard p < currentVersion)

                        not hasFrameworksBetween && not hasStandardsBetween

                    match compareVersion with
                    | FrameworkRestriction.Exactly(DotNetFramework(x)) -> isMatching (DotNetFramework x)
                    | FrameworkRestriction.Exactly(DotNetStandard(x)) -> isMatching (DotNetStandard x)
                    | FrameworkRestriction.AtLeast(DotNetFramework(x)) -> 
                        isMatching (DotNetFramework x) || 
                            (match currentVersion with
                             | DotNetFramework(y) when x < y -> true
                             | _ -> false)
                    | FrameworkRestriction.AtLeast(DotNetStandard(x)) -> 
                        isMatching (DotNetStandard x) ||
                            (match currentVersion with
                             | DotNetStandard(y) when x < y -> true
                             | _ -> false)
                    | _ -> false

                match newRestrictions |> Seq.tryFind (isLowerVersion currentVersion) with
                | None -> newRestrictions
                | Some n -> 
                    let newLowest =
                        match n with
                        | FrameworkRestriction.Exactly(DotNetFramework(x)) -> DotNetFramework x
                        | FrameworkRestriction.AtLeast(DotNetFramework(x)) -> DotNetFramework x                        
                        | FrameworkRestriction.Exactly(DotNetStandard(x)) -> DotNetStandard x
                        | FrameworkRestriction.AtLeast(DotNetStandard(x)) -> DotNetStandard x
                        | x -> failwithf "Unknown .NET moniker %O" x

                    let filtered =
                        newRestrictions
                        |> List.filter (fun x -> x <> r && x <> n)

                    filtered @ [FrameworkRestriction.AtLeast(newLowest)]
                                        
        if restrictions = filtered then sorting filtered else optimizeRestrictions filtered

let hasDotNetFrameworkOrAnyCase =
    List.exists (fun (_,_,rs) ->
        rs = [] ||
        rs
        |> List.exists (function
                        | FrameworkRestriction.Exactly(DotNetFramework(_)) -> true
                        | FrameworkRestriction.AtLeast(DotNetFramework(_)) -> true
                        | _ -> false))

let optimizeDependencies originalDependencies =
    let grouped = originalDependencies |> List.groupBy (fun (n,v,_) -> n,v)

    let expanded =
        [for (n,vr,r:FrameworkRestriction list) in originalDependencies do
            for r' in r do
                yield n,vr,r']
        |> List.groupBy (fun (_,_,r) -> r)

    let invertedRestrictions =
        [for restriction,packages in expanded do
            match restriction with
            | FrameworkRestriction.Exactly(DotNetFramework _ as r) -> 
                yield r,packages |> List.map (fun (n,v,_) -> n,v)
            | _ -> () ]
        |> List.sortBy fst

    let invertedStandardRestrictions =
        [for restriction,packages in expanded do
            match restriction with
            | FrameworkRestriction.Exactly(DotNetStandard _ as r) -> 
                yield r,packages |> List.map (fun (n,v,_) -> n,v)
            | _ -> () ]
        |> List.sortBy fst

    let globalMax = 
        invertedRestrictions
        |> List.tryLast
        |> Option.map fst

    let globalStandardMax = 
        invertedStandardRestrictions
        |> List.tryLast
        |> Option.map fst

    let globalMin = 
        invertedRestrictions
        |> List.tryHead
        |> Option.map fst

    let globalStandardMin = 
        invertedStandardRestrictions
        |> List.tryHead
        |> Option.map fst

    let emptyRestrictions =
        [for (n,vr,r:FrameworkRestriction list) in originalDependencies do
            if r = [] then
                yield n,vr]
        |> Set.ofList

    let allRestrictions =
        [for (n,vr,r:FrameworkRestriction list) in originalDependencies do
            yield r]
        |> Set.ofList

    let restrictionsPerPackage =
        originalDependencies
        |> List.groupBy (fun (n,vr,r) -> n,vr)
        |> List.map (fun ((n,vr),rs) ->
            n,vr,
             (rs 
              |> List.map (fun (_,_,r) -> r)
              |> Set.ofList))

    let packagesWithAllRestrictions =
        restrictionsPerPackage
        |> List.filter (fun (_,_,rs) -> rs = allRestrictions)
        |> List.map (fun (n,vr,_) -> n,vr)
        |> Set.ofList

    let newRestictions =
        [for (name,versionRequirement:VersionRequirement),group in grouped do
            if name <> PackageName "" then
                let hasEmpty = not (Set.isEmpty emptyRestrictions) && Set.contains (name,versionRequirement) emptyRestrictions 
                let hasAll = not (Set.isEmpty packagesWithAllRestrictions) && Set.contains (name,versionRequirement) packagesWithAllRestrictions 
            
                if hasEmpty && hasAll then
                    yield name,versionRequirement,[]
                else
                    let sorted = 
                        group 
                        |> List.map (fun (_,_,res) -> res) 
                        |> List.concat
                        |> List.distinct
                        |> List.sort

                    let localMaxDotNetRestriction = findMaxDotNetRestriction sorted
                    let localMaxStandardRestriction = findMaxStandardRestriction sorted
                    let globalMax = defaultArg globalMax localMaxDotNetRestriction
                    let globalStandardMax = defaultArg globalStandardMax localMaxStandardRestriction

                    let plain =
                        match sorted with
                        | [] ->                            
                            let globalStandardMin = defaultArg globalStandardMin localMaxDotNetRestriction
                            
                            let frameworks =
                                match globalMin with
                                | Some globalMin ->
                                    KnownTargetProfiles.DotNetFrameworkVersions 
                                    |> List.filter (fun fw -> DotNetFramework(fw) < globalMin)
                                    |> List.map (fun fw -> FrameworkRestriction.Exactly(DotNetFramework(fw)))
                                | _ ->
                                    KnownTargetProfiles.DotNetFrameworkVersions                                     
                                    |> List.map (fun fw -> FrameworkRestriction.Exactly(DotNetFramework(fw)))

                            let standards =
                                KnownTargetProfiles.DotNetStandardVersions 
                                |> List.filter (fun fw -> DotNetStandard(fw) < globalStandardMin)
                                |> List.map (fun fw -> FrameworkRestriction.Exactly(DotNetStandard(fw)))

                            frameworks @ standards

                        | _ -> sorted

                    let dotnetRestrictions,others = 
                        plain
                        |> List.partition (fun p ->
                            match p with
                            | FrameworkRestriction.Exactly(DotNetFramework(_)) -> true 
                            | FrameworkRestriction.AtLeast(DotNetFramework(_)) -> true 
                            | FrameworkRestriction.Exactly(DotNetStandard(_)) -> true 
                            | FrameworkRestriction.AtLeast(DotNetStandard(_)) -> true 
                            | _ -> false)

                    let restrictions' = 
                        dotnetRestrictions
                        |> List.map (fun restriction ->
                            match restriction with
                            | FrameworkRestriction.Exactly(DotNetFramework _ as r) ->
                                if r = localMaxDotNetRestriction && r = globalMax then
                                    FrameworkRestriction.AtLeast r
                                else
                                    restriction
                            | FrameworkRestriction.Exactly (DotNetStandard _ as r) ->
                                if r = localMaxStandardRestriction && r = globalStandardMax then
                                    FrameworkRestriction.AtLeast r
                                else
                                    restriction
                            | _ -> restriction)

                    let restrictions = optimizeRestrictions restrictions'

                    yield name,versionRequirement,others @ restrictions]

    let hasDotNetFrameworkOrStandard =
        newRestictions
        |> List.exists (fun (_,_,rs) ->
            rs
            |> List.exists (function
                            | FrameworkRestriction.Exactly(DotNetFramework(_)) -> true 
                            | FrameworkRestriction.AtLeast(DotNetFramework(_)) -> true 
                            | FrameworkRestriction.Exactly(DotNetStandard(_)) -> true 
                            | FrameworkRestriction.AtLeast(DotNetStandard(_)) -> true
                            | _ -> false))

    if not hasDotNetFrameworkOrStandard then 
        newRestictions 
    else
        let newRestictions =
            if hasDotNetFrameworkOrAnyCase originalDependencies then 
                newRestictions 
            else
                newRestictions
                //|> List.map (fun (name,vr,rs) ->
                //    let newRs = 
                //        rs
                //        |> List.collect (function
                //            | FrameworkRestriction.Exactly(DotNetStandard(r)) -> 
                //                let compatible = 
                //                    KnownTargetProfiles.DotNetFrameworkIdentifiers
                //                    |> List.filter (fun p -> p.IsCompatible(DotNetStandard r))
                //                    |> List.map (fun p -> FrameworkRestriction.Exactly p)
                //                FrameworkRestriction.AtLeast(DotNetStandard(r)) :: compatible
                //            | FrameworkRestriction.AtLeast(DotNetStandard(r)) -> 
                //                let compatible = 
                //                    KnownTargetProfiles.DotNetFrameworkIdentifiers
                //                    |> List.filter (fun p -> p.IsCompatible(DotNetStandard r))
                //                    |> List.map (fun p -> FrameworkRestriction.AtLeast p)
                //                FrameworkRestriction.AtLeast(DotNetStandard(r)) :: compatible
                //            | r -> [r])
                //        |> optimizeRestrictions
                //
                //    name,vr,newRs)
                    

        newRestictions
        |> List.map (fun (p,v,rs) ->
            let filtered =
                rs
                |> List.map (fun r ->
                    match r with
                    | FrameworkRestriction.Portable portable ->
                        let newPortable =
                            portable.Split('+')
                            |> Array.filter (fun s -> s.StartsWith "net" |> not)
                            |> fun xs -> String.Join("+",xs)
                        FrameworkRestriction.Portable newPortable
                    | _ -> r)
            p,v,filtered)


    |> List.map (fun (a,b,c) -> a,b, FrameworkRestrictionList c)

let private combineSameCategoryOrPortableRestrictions x y =
    match x with
    | FrameworkRestriction.Exactly r -> 
        match y with
        | FrameworkRestriction.Exactly r' -> if r = r' then [FrameworkRestriction.Exactly r] else []
        | FrameworkRestriction.Portable _ -> []
        | FrameworkRestriction.AtLeast r' -> if r' <= r then [FrameworkRestriction.Exactly r] else []
        | FrameworkRestriction.Between(min,max) -> if min <= r && r < max then [FrameworkRestriction.Exactly r] else []
    | FrameworkRestriction.Portable r ->
        match y with
        | FrameworkRestriction.Portable r' -> if r = r' then [FrameworkRestriction.Portable r] else []
        | _ -> []
    | FrameworkRestriction.AtLeast r ->
        match y with
        | FrameworkRestriction.Exactly r' -> if r <= r' then [FrameworkRestriction.Exactly r'] else []
        | FrameworkRestriction.Portable _ -> []
        | FrameworkRestriction.AtLeast r' -> [FrameworkRestriction.AtLeast (max r r')]
        | FrameworkRestriction.Between(min,max') -> if r < max' then [FrameworkRestriction.Between(max r min,max')] else []
    | FrameworkRestriction.Between(min1,max1) ->
        match y with
        | FrameworkRestriction.Exactly r -> if min1 <= r && r < max1 then [FrameworkRestriction.Exactly r] else []
        | FrameworkRestriction.Portable _ -> []
        | FrameworkRestriction.AtLeast r -> if r < max1 then [FrameworkRestriction.Between(max r min1,max1)] else []
        | FrameworkRestriction.Between(min2,max2) -> 
            let min' = max min1 min2
            let max' = min max1 max2
            if min' < max' then [FrameworkRestriction.Between(min',max')] else
            if min' = max' then [FrameworkRestriction.Exactly(min')] else
            []

let combineRestrictions loose (x : FrameworkRestriction) y =
    if x.IsSameCategoryAs(y) <> Some false then
        combineSameCategoryOrPortableRestrictions x y
    else
        if loose then
             match (x.GetOneIdentifier, y.GetOneIdentifier) with
             | Some (FrameworkIdentifier.DotNetFramework _ ), Some (FrameworkIdentifier.DotNetStandard _ ) -> [x]
             | Some (FrameworkIdentifier.DotNetStandard _ ), Some (FrameworkIdentifier.DotNetFramework _ ) -> [y]
             | _ -> []
        else
            []

let filterRestrictions (list1:FrameworkRestrictions) (list2:FrameworkRestrictions) =
    match list1,list2 with 
    | FrameworkRestrictionList [], AutoDetectFramework -> AutoDetectFramework
    | AutoDetectFramework, FrameworkRestrictionList [] -> AutoDetectFramework
    | AutoDetectFramework, AutoDetectFramework -> AutoDetectFramework
    | FrameworkRestrictionList list1 , FrameworkRestrictionList list2 ->
        let filtered =
            match list1, list2 with
            | [],_ -> list2
            | _,[] -> list1
            | _ ->
                [for x in list1 do
                    for y in list2 do
                        let c = combineRestrictions false x y
                        if c <> [] then yield! c]

        let tryLoose = 
            (filtered |> List.exists (fun r -> match r.GetOneIdentifier with | Some (FrameworkIdentifier.DotNetFramework _ ) -> true | _ -> false) |> not) &&
                (list2 |> List.exists (fun r -> match r.GetOneIdentifier with | Some (FrameworkIdentifier.DotNetFramework _ ) -> true | _ -> false))

        let filtered = 
            if tryLoose then
                match list1, list2 with
                | [],_ -> list2
                | _,[] -> list1
                | _ ->
                    [for x in list1 do
                        for y in list2 do
                            let c = combineRestrictions true x y
                            if c <> [] then yield! c]
            else
                filtered

        let optimized =
            filtered
            |> optimizeRestrictions
        FrameworkRestrictionList optimized
    | _ -> failwithf "The framework restriction %O and %O could not be combined." list1 list2

/// Get if a target should be considered with the specified restrictions
let isTargetMatchingRestrictions =
    memoize <| fun (restrictions:FrameworkRestriction list, target) ->
        if List.isEmpty restrictions then true else
        match target with
        | SinglePlatform pf ->
            restrictions
            |> List.exists (fun restriction ->
                    match restriction with
                    | FrameworkRestriction.Exactly (Native(NoBuildMode,NoPlatform)) -> 
                        match pf with 
                        | Native(_) -> true 
                        | _ -> false
                    | FrameworkRestriction.Exactly fw ->
                        pf = fw
                    | FrameworkRestriction.Portable _ -> false
                    | FrameworkRestriction.AtLeast fw ->
                        pf.IsAtLeast(fw)
                    | FrameworkRestriction.Between(min,max) -> 
                        pf.IsBetween(min,max))
        | _ ->
            restrictions
            |> List.exists (fun restriction ->
                    match restriction with
                    | FrameworkRestriction.Portable r -> true
                    | _ -> false)

/// Get all targets that should be considered with the specified restrictions
let applyRestrictionsToTargets (restrictions:FrameworkRestriction list) (targets: TargetProfile list) =
    targets 
    |> List.filter (fun t -> isTargetMatchingRestrictions(restrictions,t))

type ContentCopySettings =
| Omit
| Overwrite
| OmitIfExisting

type CopyToOutputDirectorySettings =
| Never
| Always
| PreserveNewest

type BindingRedirectsSettings =
| On
| Off
| Force

type InstallSettings = 
    { ImportTargets : bool option
      FrameworkRestrictions: FrameworkRestrictions
      OmitContent : ContentCopySettings option
      IncludeVersionInPath: bool option
      ReferenceCondition : string option
      CreateBindingRedirects : BindingRedirectsSettings option
      CopyLocal : bool option
      Excludes : string list
      Aliases : Map<string,string>
      CopyContentToOutputDirectory : CopyToOutputDirectorySettings option 
      GenerateLoadScripts : bool option }

    static member Default =
        { CopyLocal = None
          ImportTargets = None
          FrameworkRestrictions = FrameworkRestrictionList []
          IncludeVersionInPath = None
          ReferenceCondition = None
          CreateBindingRedirects = None
          Excludes = []
          Aliases = Map.empty
          CopyContentToOutputDirectory = None
          OmitContent = None 
          GenerateLoadScripts = None }

    member this.ToString(asLines) =
        let options =
            [ match this.CopyLocal with
              | Some x -> yield "copy_local: " + x.ToString().ToLower()
              | None -> ()
              match this.CopyContentToOutputDirectory with
              | Some CopyToOutputDirectorySettings.Never -> yield "copy_content_to_output_dir: never"
              | Some CopyToOutputDirectorySettings.Always -> yield "copy_content_to_output_dir: always"
              | Some CopyToOutputDirectorySettings.PreserveNewest -> yield "copy_content_to_output_dir: preserve_newest"
              | None -> ()
              match this.ImportTargets with
              | Some x -> yield "import_targets: " + x.ToString().ToLower()
              | None -> ()
              match this.OmitContent with
              | Some ContentCopySettings.Omit -> yield "content: none"
              | Some ContentCopySettings.Overwrite -> yield "content: true"
              | Some ContentCopySettings.OmitIfExisting -> yield "content: once"
              | None -> ()
              match this.IncludeVersionInPath with
              | Some x -> yield "version_in_path: " + x.ToString().ToLower()
              | None -> ()
              match this.ReferenceCondition with
              | Some x -> yield "condition: " + x.ToUpper()
              | None -> ()
              match this.CreateBindingRedirects with
              | Some On -> yield "redirects: on"
              | Some Off -> yield "redirects: off"
              | Some Force -> yield "redirects: force"
              | None -> ()
              match this.FrameworkRestrictions with
              | FrameworkRestrictionList [] -> ()
              | AutoDetectFramework -> ()
              | FrameworkRestrictionList list -> yield "framework: " + (String.Join(", ",list))
              match this.GenerateLoadScripts with
              | Some true -> yield "generate_load_scripts: true"
              | Some false -> yield "generate_load_scripts: false"
              | None -> () ]

        let separator = if asLines then Environment.NewLine else ", "
        String.Join(separator,options)

    override this.ToString() = this.ToString(false)

    static member (+)(self, other : InstallSettings) =
        {
            self with 
                ImportTargets = self.ImportTargets ++ other.ImportTargets
                FrameworkRestrictions = filterRestrictions self.FrameworkRestrictions other.FrameworkRestrictions
                OmitContent = self.OmitContent ++ other.OmitContent
                CopyLocal = self.CopyLocal ++ other.CopyLocal
                CopyContentToOutputDirectory = self.CopyContentToOutputDirectory ++ other.CopyContentToOutputDirectory
                ReferenceCondition = self.ReferenceCondition ++ other.ReferenceCondition
                Excludes = self.Excludes @ other.Excludes
                CreateBindingRedirects = self.CreateBindingRedirects ++ other.CreateBindingRedirects
                IncludeVersionInPath = self.IncludeVersionInPath ++ other.IncludeVersionInPath
        }

    static member Parse(text:string) : InstallSettings =
        let kvPairs = parseKeyValuePairs (text.ToLower())

        let getPair key =
            match kvPairs.TryGetValue key with
            | true, x -> kvPairs.Remove key |> ignore; Some x
            | _ -> None

        let settings =
            { ImportTargets =
                match getPair "import_targets" with
                | Some "false" -> Some false 
                | Some "true" -> Some true
                | _ -> None
              FrameworkRestrictions =
                match getPair "framework" with
                | Some s -> FrameworkRestrictionList(parseRestrictions true s)
                | _ -> FrameworkRestrictionList []
              OmitContent =
                match getPair "content" with
                | Some "none" -> Some ContentCopySettings.Omit 
                | Some "once" -> Some ContentCopySettings.OmitIfExisting
                | Some "true" -> Some ContentCopySettings.Overwrite
                | _ ->  None
              CreateBindingRedirects =
                match getPair "redirects" with
                | Some "on" -> Some On 
                | Some "off" -> Some Off
                | Some "force" -> Some Force
                | _ ->  None
              IncludeVersionInPath =
                match getPair "version_in_path" with
                | Some "false" -> Some false 
                | Some "true" -> Some true
                | _ -> None 
              ReferenceCondition =
                match getPair "condition" with
                | Some c -> Some(c.ToUpper())
                | _ -> None 
              CopyContentToOutputDirectory =
                match getPair "copy_content_to_output_dir" with
                | Some "preserve_newest" -> Some CopyToOutputDirectorySettings.PreserveNewest 
                | Some "always" -> Some CopyToOutputDirectorySettings.Always 
                | Some "never" -> Some CopyToOutputDirectorySettings.Never
                | None -> None
                | x -> failwithf "Unknown copy_content_to_output_dir settings: %A" x
              Excludes = []
              Aliases = Map.empty
              CopyLocal =
                match getPair "copy_local" with
                | Some "false" -> Some false 
                | Some "true" -> Some true
                | _ -> None
              GenerateLoadScripts =
                match getPair "generate_load_scripts" with
                | Some "on"  | Some "true" -> Some true
                | Some "off" | Some "false" -> Some true
                | _ -> None }

        // ignore resolver settings here
        getPair "strategy" |> ignore
        getPair "lowest_matching" |> ignore

        for kv in kvPairs do
            failwithf "Unknown package settings %s: %s" kv.Key kv.Value

        settings

    member this.AdjustWithSpecialCases(packageName) =
        if packageName = PackageName "Microsoft.Bcl.Build" && this.ImportTargets = None then
            // Microsoft.Bcl.Build targets file causes the build to fail in VS
            // so users have to be very explicit with the targets file
            { this with ImportTargets = Some false }
        else
            this


type RemoteFileInstallSettings = 
    { Link : bool option }

    static member Default =
        { Link = None }

    member this.ToString(asLines) =
        let options =
            [ match this.Link with
              | Some x -> yield "link: " + x.ToString().ToLower()
              | None -> ()]

        let separator = if asLines then Environment.NewLine else ", "
        String.Join(separator,options)

    override this.ToString() = this.ToString(false)

    static member Parse(text:string) : RemoteFileInstallSettings =
        let kvPairs = parseKeyValuePairs text

        { Link =
            match kvPairs.TryGetValue "link" with
            | true, "false" -> Some false 
            | true, "true" -> Some true
            | _ -> None }

type PackageRequirementSource =
| DependenciesFile of string
| Package of PackageName * SemVerInfo * PackageSource
    member this.IsRootRequirement() =
        match this with
        | DependenciesFile _ -> true
        | _ -> false

    override this.ToString() =
        match this with
        | DependenciesFile x -> x
        | Package(name,version,_) ->
          sprintf "%O %O" name version

/// Represents an unresolved package.
[<CustomEquality;CustomComparison>]
type PackageRequirement =
    { Name : PackageName
      VersionRequirement : VersionRequirement
      ResolverStrategyForDirectDependencies : ResolverStrategy option
      ResolverStrategyForTransitives : ResolverStrategy option
      Parent: PackageRequirementSource
      Graph: PackageRequirement list
      Sources: PackageSource list
      Settings: InstallSettings }

    override this.Equals(that) = 
        match that with
        | :? PackageRequirement as that -> 
            this.Name = that.Name && 
            this.VersionRequirement = that.VersionRequirement && 
            this.ResolverStrategyForTransitives = that.ResolverStrategyForTransitives && 
            this.ResolverStrategyForDirectDependencies = that.ResolverStrategyForDirectDependencies && 
            this.Settings.FrameworkRestrictions = that.Settings.FrameworkRestrictions &&
            this.Parent = that.Parent
        | _ -> false

    override this.ToString() =
        sprintf "%O %O (from %O)" this.Name this.VersionRequirement this.Parent


    override this.GetHashCode() = hash (this.Name,this.VersionRequirement)
    
    member this.IncludingPrereleases(releaseStatus) = 
        { this with VersionRequirement = VersionRequirement(this.VersionRequirement.Range,releaseStatus) }

    member this.IncludingPrereleases() = this.IncludingPrereleases(PreReleaseStatus.All)    

    member this.Depth = this.Graph.Length

    static member Compare(x,y,startWithPackage:PackageFilter option,boostX,boostY) =
        if obj.ReferenceEquals(x, y) then 0 else
        let c = compare
                  (not x.VersionRequirement.Range.IsGlobalOverride,x.Depth)
                  (not y.VersionRequirement.Range.IsGlobalOverride,y.Depth)
        if c <> 0 then c else
        let c = match startWithPackage with
                    | Some filter when filter.Match x.Name -> -1
                    | Some filter when filter.Match y.Name -> 1
                    | _ -> 0
        if c <> 0 then c else
        let c = -compare x.ResolverStrategyForDirectDependencies y.ResolverStrategyForDirectDependencies
        if c <> 0 then c else
        let c = -compare x.ResolverStrategyForTransitives y.ResolverStrategyForTransitives
        if c <> 0 then c else
        let c = compare boostX boostY
        if c <> 0 then c else
        let c = -compare x.VersionRequirement y.VersionRequirement
        if c <> 0 then c else
        let c = compare x.Settings.FrameworkRestrictions y.Settings.FrameworkRestrictions
        if c <> 0 then c else
        let c = compare x.Parent y.Parent
        if c <> 0 then c else
        let c = compare x.Name y.Name
        if c <> 0 then c else 0

    interface System.IComparable with
       member this.CompareTo that = 
          match that with 
          | :? PackageRequirement as that ->
                PackageRequirement.Compare(this,that,None,0,0)
          | _ -> invalidArg "that" "cannot compare value of different types"