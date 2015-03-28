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
        | FrameworkRestriction.Between(min,max) -> sprintf ">= %s < %s" (min.ToString()) (max.ToString())

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
        | None -> ()
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

let optimizeRestrictions packages =
    let grouped = packages |> Seq.groupBy (fun (n,v,_) -> n,v) |> Seq.toList    

    let invertedRestrictions =
        let expanded =
            [for (n,vr,r:FrameworkRestrictions) in packages do
                for r' in r do
                    yield n,vr,r']
            |> Seq.groupBy (fun (_,_,r) -> r)

        [for restriction,packages in expanded do
            match restriction with
            | FrameworkRestriction.Exactly r -> 
                if r.ToString().StartsWith("net") then
                    yield r,packages |> Seq.map (fun (n,v,_) -> n,v) |> Seq.toList
            | _ -> () ]
        |> List.sortBy fst

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
                    |> Seq.map (fun (_,_,res) -> res) 
                    |> Seq.concat 
                    |> Seq.toList

                let localMaxDotNetRestriction = findMaxDotNetRestriction plain        

                let restrictions =
                    plain
                    |> List.map (fun restriction ->
                        match restriction with
                        | FrameworkRestriction.Exactly r ->                     
                            if r = localMaxDotNetRestriction then
                                let globalMax = 
                                    invertedRestrictions
                                    |> Seq.skipWhile (fun (r,l) -> r <= localMaxDotNetRestriction && l |> List.exists (fun (n,vr) -> n = name && vr = versionRequirement))
                                    |> Seq.map fst
                                    |> Seq.toList

                                if globalMax = [] || r >= globalMax.Head then
                                    FrameworkRestriction.AtLeast r
                                else
                                    FrameworkRestriction.Between(r,globalMax.Head)
                            else
                                restriction
                        | _ -> restriction)
                    |> Seq.toList
                    |> List.sort

                yield name,versionRequirement,restrictions]

type InstallSettings = 
    { ImportTargets : bool
      FrameworkRestrictions: FrameworkRestrictions
      OmitContent : bool 
      CopyLocal : bool }

    static member Default =
        { CopyLocal = true
          ImportTargets = true
          FrameworkRestrictions = []
          OmitContent = false }

    member this.ToString(asLines) =
        let options =
            [ if not this.CopyLocal then yield "copy_local: false"
              if not this.ImportTargets then yield "import_targets: false"
              if this.OmitContent then yield "content: none"
              match this.FrameworkRestrictions with
              | [] -> ()
              | _  -> yield "framework: " + (String.Join(", ",this.FrameworkRestrictions))]

        let separator = if asLines then Environment.NewLine else ", "
        String.Join(separator,options)

    override this.ToString() = this.ToString(false)

    static member Parse(text:string) =
        let kvPairs = parseKeyValuePairs text

        { ImportTargets =
            match kvPairs.TryGetValue "import_targets" with
            | true, "false" -> false
            | _ -> true
          FrameworkRestrictions =
            match kvPairs.TryGetValue "framework" with
            | true, s -> parseRestrictions s
            | _ -> []
          OmitContent =
            match kvPairs.TryGetValue "content" with
            | true, "none" -> true 
            | _ -> false 
          CopyLocal =         
            match kvPairs.TryGetValue "copy_local" with
            | true, "false" -> false 
            | _ -> true }

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
          sprintf "%s %s" (name.ToString()) (version.ToString())


/// Represents an unresolved package.
[<CustomEquality;CustomComparison>]
type PackageRequirement =
    { Name : PackageName
      VersionRequirement : VersionRequirement
      ResolverStrategy : ResolverStrategy
      Parent: PackageRequirementSource
      Settings: InstallSettings
      Sources : PackageSource list }

    override this.Equals(that) = 
        match that with
        | :? PackageRequirement as that -> this.Name = that.Name && this.VersionRequirement = that.VersionRequirement
        | _ -> false

    override this.ToString() =
        let (PackageName name) = this.Name
        sprintf "%s %s (from %s)" name (this.VersionRequirement.ToString()) (this.Parent.ToString())

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