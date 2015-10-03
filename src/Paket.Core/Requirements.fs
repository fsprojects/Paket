module Paket.Requirements

open System
open Paket
open Paket.Domain
open Paket.PackageSources

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

type FrameworkRestrictions = FrameworkRestriction list



let parseRestrictions(text:string) =
    let commaSplit = text.Trim().Split(',')
    [for p in commaSplit do
        let operatorSplit = p.Trim().Split(' ')
        let framework =
            if operatorSplit.Length < 2 then 
                operatorSplit.[0] 
            else 
                operatorSplit.[1]


        match FrameworkDetection.Extract(framework) with
        | None -> 
                if PlatformMatching.extractPlatforms framework |> Array.isEmpty |> not then
                    yield FrameworkRestriction.Portable framework
        | Some x -> 
            if operatorSplit.[0] = ">=" then
                if operatorSplit.Length < 4 then
                    yield FrameworkRestriction.AtLeast x
                else
                    match FrameworkDetection.Extract(operatorSplit.[3]) with
                    | None -> ()
                    | Some y -> yield FrameworkRestriction.Between(x,y)
            else
                yield FrameworkRestriction.Exactly x]

let private minRestriction = FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V1))

let findMaxDotNetRestriction restrictions =
    minRestriction :: restrictions
    |> List.filter (fun (r:FrameworkRestriction) ->
        match r with
        | FrameworkRestriction.Exactly r -> r.ToString().StartsWith("net")
        | _ -> false)
    |> List.max
    |> fun r ->
        match r with
        | FrameworkRestriction.Exactly r -> r
        | _ -> failwith "error"

let rec optimizeRestrictions restrictions = 
    match restrictions with
    | [] -> restrictions
    | [x] -> restrictions
    | _ ->
                            
        let newRestrictions' = 
            restrictions
            |> List.distinct
            |> List.sort                            

        let newRestrictions =
            match newRestrictions' |> Seq.tryFind (function | FrameworkRestriction.AtLeast r -> true | _ -> false) with
            | None -> newRestrictions'
            | Some r ->
                let currentVersion =
                    match r with
                    | FrameworkRestriction.AtLeast(DotNetFramework(x)) -> x
                    | x -> failwithf "Unknown .NET moniker %O" x     
                                                                                                           
                let isLowerVersion x =
                    let isMatching x =
                        if x = FrameworkVersion.V3_5 && currentVersion = FrameworkVersion.V4 then true else
                        if x = FrameworkVersion.V4_Client && currentVersion = FrameworkVersion.V4_5 then true else
                        let hasFrameworksBetween = KnownTargetProfiles.DotNetFrameworkVersions |> Seq.exists (fun p -> p > x && p < currentVersion)
                        not hasFrameworksBetween

                    match x with
                    | FrameworkRestriction.Exactly(DotNetFramework(x)) -> isMatching x
                    | FrameworkRestriction.AtLeast(DotNetFramework(x)) -> isMatching x
                    | _ -> false

                match newRestrictions' |> Seq.tryFind isLowerVersion with
                | None -> newRestrictions'
                | Some n -> 
                    let newLowest =
                        match n with
                        | FrameworkRestriction.Exactly(DotNetFramework(x)) -> x
                        | FrameworkRestriction.AtLeast(DotNetFramework(x)) -> x
                        | x -> failwithf "Unknown .NET moniker %O" x     

                    (newRestrictions'
                        |> List.filter (fun x -> x <> r && x <> n)) @ [FrameworkRestriction.AtLeast(DotNetFramework(newLowest))]
                                        
        if restrictions = newRestrictions then newRestrictions else optimizeRestrictions newRestrictions


let optimizeDependencies packages =
    let grouped = packages |> List.groupBy (fun (n,v,_) -> n,v)

    let invertedRestrictions =
        let expanded =
            [for (n,vr,r:FrameworkRestrictions) in packages do
                for r' in r do
                    yield n,vr,r']
            |> List.groupBy (fun (_,_,r) -> r)

        [for restriction,packages in expanded do
            match restriction with
            | FrameworkRestriction.Exactly r -> 
                let s = r.ToString()
                if s.StartsWith("net") then
                    yield r,packages |> List.map (fun (n,v,_) -> n,v)
            | _ -> () ]
        |> List.sortBy fst

    let globalMax = 
        invertedRestrictions
        |> List.tryLast
        |> Option.map fst

    let emptyRestrictions =
        [for (n,vr,r:FrameworkRestrictions) in packages do
            if r = [] then
                yield n,vr]
        |> Set.ofList

    [for (name,versionRequirement:VersionRequirement),group in grouped do
        if name <> PackageName "" then
            if not (Set.isEmpty emptyRestrictions) && Set.contains (name,versionRequirement) emptyRestrictions then
                yield name,versionRequirement,[]
            else
                let plain = 
                    group 
                    |> List.map (fun (_,_,res) -> res) 
                    |> List.concat
                    |> List.distinct
                    |> List.sort

                let localMaxDotNetRestriction = findMaxDotNetRestriction plain
                let globalMax = defaultArg globalMax localMaxDotNetRestriction          

                let dotnetRestrictions,others = List.partition (function | FrameworkRestriction.Exactly(DotNetFramework(_)) -> true | FrameworkRestriction.AtLeast(DotNetFramework(_)) -> true | _ -> false) plain

                let restrictions' = 
                    dotnetRestrictions
                    |> List.map (fun restriction ->
                        match restriction with
                        | FrameworkRestriction.Exactly r ->                     
                            if r = localMaxDotNetRestriction && r = globalMax then
                                FrameworkRestriction.AtLeast r
                            else
                                restriction
                        | _ -> restriction)

                let restrictions = optimizeRestrictions restrictions'

                yield name,versionRequirement,others @ restrictions]

let combineRestrictions x y =
    match x with
    | FrameworkRestriction.Exactly r -> 
        match y with
        | FrameworkRestriction.Exactly r' -> if r = r' then [FrameworkRestriction.Exactly r] else []
        | FrameworkRestriction.Portable _ -> []
        | FrameworkRestriction.AtLeast r' -> if r' <= r then [FrameworkRestriction.Exactly r] else []
        | FrameworkRestriction.Between(min,max) -> if min <= r && r <= max then [FrameworkRestriction.Exactly r] else []
    | FrameworkRestriction.Portable r ->
        match y with
        | FrameworkRestriction.Portable r' -> if r = r' then [FrameworkRestriction.Portable r] else []
        | _ -> []
    | FrameworkRestriction.AtLeast r ->
        match y with
        | FrameworkRestriction.Exactly r' -> if r <= r' then [FrameworkRestriction.Exactly r'] else []
        | FrameworkRestriction.Portable _ -> []
        | FrameworkRestriction.AtLeast r' -> [FrameworkRestriction.AtLeast (max r r')]
        | FrameworkRestriction.Between(min,max) -> if min <= r && r <= max then [FrameworkRestriction.Between(r,max)] else []
    | FrameworkRestriction.Between(min1,max1) ->
        match y with
        | FrameworkRestriction.Exactly r -> if min1 <= r && r <= max1 then [FrameworkRestriction.Exactly r] else []
        | FrameworkRestriction.Portable _ -> []
        | FrameworkRestriction.AtLeast r -> if min1 <= r && r <= max1 then [FrameworkRestriction.Between(r,max1)] else []
        | FrameworkRestriction.Between(min2,max2) -> 
            let min' = max min1 min2
            let max' = min max1 max2
            if min' < max' then [FrameworkRestriction.Between(min',max')] else
            if min' = max' then [FrameworkRestriction.Exactly(min')] else
            []

let filterRestrictions (list1:FrameworkRestrictions) (list2:FrameworkRestrictions) =
    match list1,list2 with
    | [],_ -> list2
    | _,[] -> list1
    | _ ->
        [for x in list1 do
            for y in list2 do
                let c = combineRestrictions x y
                if c <> [] then yield! c]
    |> optimizeRestrictions

type ContentCopySettings =
| Omit
| Overwrite
| OmitIfExisting

type InstallSettings = 
    { ImportTargets : bool option
      FrameworkRestrictions: FrameworkRestrictions
      OmitContent : ContentCopySettings option
      IncludeVersionInPath: bool option
      ReferenceCondition : string option
      CreateBindingRedirects : bool option
      CopyLocal : bool option }

    static member Default =
        { CopyLocal = None
          ImportTargets = None
          FrameworkRestrictions = []
          IncludeVersionInPath = None
          ReferenceCondition = None
          CreateBindingRedirects = None
          OmitContent = None }

    member this.ToString(asLines) =
        let options =
            [ match this.CopyLocal with
              | Some x -> yield "copy_local: " + x.ToString().ToLower()
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
              | Some true -> yield "redirects: on"
              | Some false -> yield "redirects: off"
              | None -> ()
              match this.FrameworkRestrictions with
              | [] -> ()
              | _  -> yield "framework: " + (String.Join(", ",this.FrameworkRestrictions))]

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
                ReferenceCondition = self.ReferenceCondition ++ other.ReferenceCondition
                IncludeVersionInPath = self.IncludeVersionInPath ++ other.IncludeVersionInPath
        }

    static member Parse(text:string) : InstallSettings =
        let kvPairs = parseKeyValuePairs text

        { ImportTargets =
            match kvPairs.TryGetValue "import_targets" with
            | true, "false" -> Some false 
            | true, "true" -> Some true
            | _ -> None
          FrameworkRestrictions =
            match kvPairs.TryGetValue "framework" with
            | true, s -> parseRestrictions s
            | _ -> []
          OmitContent =
            match kvPairs.TryGetValue "content" with
            | true, "none" -> Some ContentCopySettings.Omit 
            | true, "once" -> Some ContentCopySettings.OmitIfExisting
            | true, "true" -> Some ContentCopySettings.Overwrite
            | _ ->  None
          CreateBindingRedirects =
            match kvPairs.TryGetValue "redirects" with
            | true, "on" -> Some true 
            | true, "off" -> Some false 
            | _ ->  None
          IncludeVersionInPath =         
            match kvPairs.TryGetValue "version_in_path" with
            | true, "false" -> Some false 
            | true, "true" -> Some true
            | _ -> None 
          ReferenceCondition =         
            match kvPairs.TryGetValue "condition" with
            | true, c -> Some(c.ToUpper())
            | _ -> None 
          CopyLocal =         
            match kvPairs.TryGetValue "copy_local" with
            | true, "false" -> Some false 
            | true, "true" -> Some true
            | _ -> None }

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
| Package of PackageName * SemVerInfo 
    member this.IsRootRequirement() =
        match this with
        | DependenciesFile _ -> true
        | _ -> false

    override this.ToString() =
        match this with
        | DependenciesFile x -> x
        | Package(name,version) ->
          sprintf "%O %O" name version

/// Represents an unresolved package.
[<CustomEquality;CustomComparison>]
type PackageRequirement =
    { Name : PackageName
      VersionRequirement : VersionRequirement
      ResolverStrategy : ResolverStrategy
      Parent: PackageRequirementSource
      Settings: InstallSettings }

    override this.Equals(that) = 
        match that with
        | :? PackageRequirement as that -> this.Name = that.Name && this.VersionRequirement = that.VersionRequirement
        | _ -> false

    override this.ToString() =
        sprintf "%O %O (from %O)" this.Name this.VersionRequirement this.Parent

    override this.GetHashCode() = hash (this.Name,this.VersionRequirement)

    member this.IncludingPrereleases() = 
        { this with VersionRequirement = VersionRequirement(this.VersionRequirement.Range,PreReleaseStatus.All) }
    
    static member Compare(x,y,boostX,boostY) =
        if x = y then 0 else
        let c1 =
            compare 
                (not x.VersionRequirement.Range.IsGlobalOverride,x.Parent)
                (not y.VersionRequirement.Range.IsGlobalOverride,x.Parent)
        if c1 <> 0 then c1 else
        let c2 = -1 * compare x.ResolverStrategy y.ResolverStrategy
        if c2 <> 0 then c2 else
        let cBoost = compare boostX boostY
        if cBoost <> 0 then cBoost else
        let c3 = -1 * compare x.VersionRequirement y.VersionRequirement
        if c3 <> 0 then c3 else
        compare x.Name y.Name

    interface System.IComparable with
       member this.CompareTo that = 
          match that with 
          | :? PackageRequirement as that ->
                PackageRequirement.Compare(this,that,0,0)
          | _ -> invalidArg "that" "cannot compare value of different types"