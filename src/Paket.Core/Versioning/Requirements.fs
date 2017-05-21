module Paket.Requirements

open System
open Paket
open Paket.Domain
open Paket.PackageSources
open Paket.Logging

let private allFrameworks =
    KnownTargetProfiles.AllProfiles
    //|> List.collect (function
    //    | SinglePlatform fw -> [fw]
    //    | PortableProfile (_, fws) -> fws)
    //|> List.distinct
    //|> List.sort
[<RequireQualifiedAccess>]
// To make reasoning and writing tests easier.
// Ideally we would "simplify" the trees to a "normal" form internally
[<CustomEquality; CustomComparison>]
[<System.Diagnostics.DebuggerDisplay("{InfixNotation}")>]
type FrameworkRestrictionP =
    private
    //| NoRestrictionP // = AndP []
    //| EmptySetP // = OrP []
    | ExactlyP of TargetProfile
    //[<Obsolete("Portable is a mess, don't use it")>]
    //| PortableP of string * FrameworkIdentifier list
    | AtLeastP of TargetProfile
    // Means: Take all frameworks NOT given by the restriction
    | NotP of FrameworkRestrictionP
    | OrP of FrameworkRestrictionP list
    | AndP of FrameworkRestrictionP list
    member x.InfixNotation =
        match x with
        | FrameworkRestrictionP.ExactlyP r -> r.ToString()
        //| FrameworkRestrictionP.PortableP (r,_) -> r
        | FrameworkRestrictionP.AtLeastP r -> ">= " + r.ToString()
        | FrameworkRestrictionP.NotP(FrameworkRestrictionP.AtLeastP r) -> sprintf "< " + r.ToString()
        | FrameworkRestrictionP.NotP(fr) -> sprintf "NOT (%O)" fr
        | FrameworkRestrictionP.OrP(frl) ->
            match frl with
            | [] -> "false"
            | [single] -> sprintf "%O" single
            | _ -> sprintf "(%s)" (System.String.Join(" || ", frl |> Seq.map (fun inner -> sprintf "(%s)" inner.InfixNotation)))
        | FrameworkRestrictionP.AndP(frl) ->
            match frl with
            | [] -> "true"
            | [single] -> sprintf "%O" single
            | _ -> sprintf "(%s)" (System.String.Join(" && ", frl |> Seq.map (fun inner -> sprintf "(%s)" inner.InfixNotation)))
    override this.ToString() =
        match this with
        | FrameworkRestrictionP.ExactlyP r -> r.ToString()
        //| FrameworkRestrictionP.PortableP (r,_) -> r
        | FrameworkRestrictionP.AtLeastP r -> ">= " + r.ToString()
        | FrameworkRestrictionP.NotP(FrameworkRestrictionP.AtLeastP r) -> sprintf "< " + r.ToString()
        | FrameworkRestrictionP.NotP(fr) -> sprintf "NOT (%O)" fr
        | FrameworkRestrictionP.OrP(frl) ->
            match frl with
            | [] -> "false"
            | [single] -> sprintf "%O" single
            | _ -> sprintf "|| %s" (System.String.Join(" ", frl |> Seq.map (sprintf "(%O)")))
        | FrameworkRestrictionP.AndP(frl) ->
            match frl with
            | [] -> "true"
            | [single] -> sprintf "%O" single
            | _ -> sprintf "&& %s" (System.String.Join(" ", frl |> Seq.map (sprintf "(%O)")))

    /// The list represented by this restriction (ie the included set of frameworks)
    member x.RepresentedFrameworks =
        match x with
        | FrameworkRestrictionP.ExactlyP r -> [ r ]
        //| FrameworkRestrictionP.PortableP (r, fws) ->
        //    fws
        //    |> List.collect (fun fw -> (FrameworkRestrictionP.AtLeastP fw).RepresentedFrameworks)
        //    |> List.distinct
        //    |> List.sort
        | FrameworkRestrictionP.AtLeastP r ->
            PlatformMatching.getPlatformsSupporting r
        | FrameworkRestrictionP.NotP(fr) ->
            let notTaken = fr.RepresentedFrameworks
            allFrameworks
            |> List.filter (fun fw -> notTaken |> Seq.contains fw |> not)
        | FrameworkRestrictionP.OrP (frl) ->
            frl
            |> List.collect (fun fr -> fr.RepresentedFrameworks)
            |> List.distinct
            |> List.sort
        | FrameworkRestrictionP.AndP (frl) ->
            match frl with
            | h :: _ ->
                let allLists = frl |> List.map (fun fr -> fr.RepresentedFrameworks)
                h.RepresentedFrameworks
                |> List.filter (fun fw -> allLists |> List.forall(fun l1 -> l1 |> Seq.contains fw))
            | [] -> 
                allFrameworks

    member x.IsMatch (tp:TargetProfile) =
        match x with
        | FrameworkRestrictionP.ExactlyP r -> r = tp
        //| FrameworkRestrictionP.PortableP (r, fws) ->
        //    fws
        //    |> List.collect (fun fw -> (FrameworkRestrictionP.AtLeastP fw).RepresentedFrameworks)
        //    |> List.distinct
        //    |> List.sort
        | FrameworkRestrictionP.AtLeastP r ->
            tp.SupportedPlatformsTransitive |> Seq.contains r
        | FrameworkRestrictionP.NotP(fr) ->
            fr.IsMatch tp |> not
        | FrameworkRestrictionP.OrP (frl) ->
            frl
            |> List.exists (fun fr -> fr.IsMatch tp)
        | FrameworkRestrictionP.AndP (frl) ->
            frl
            |> List.forall (fun fr -> fr.IsMatch tp)

    /// Returns true if the restriction x is a subset of the restriction y (a restriction basically represents a list, see RepresentedFrameworks)
    /// For example =net46 is a subset of >=netstandard13
    member x.IsSubsetOf (y:FrameworkRestrictionP) =
        let superset = y.RepresentedFrameworks
        x.RepresentedFrameworks
        |> List.forall (fun inner -> superset |> Seq.contains inner)
    static member ExactlyFramework (tf: FrameworkIdentifier) =
        ExactlyP (SinglePlatform tf)

    override x.Equals(y) = (match y with :? FrameworkRestrictionP as r -> r.RepresentedFrameworks = x.RepresentedFrameworks | _ -> false)
    override x.GetHashCode() = x.RepresentedFrameworks.GetHashCode()
    interface System.IComparable with
        member x.CompareTo(y) = (match y with :? FrameworkRestrictionP as r -> compare x.RepresentedFrameworks r.RepresentedFrameworks | _ -> failwith "wrong type")

type FrameworkRestrictionLiteralI =
    | ExactlyL of TargetProfile
    //| PortableL of string * FrameworkIdentifier list
    | AtLeastL of TargetProfile
    member internal x.RawFormular =
        match x with
        | ExactlyL id -> FrameworkRestrictionP.ExactlyP id
        //| PortableL (name, fws) -> FrameworkRestrictionP.PortableP (name, fws)
        | AtLeastL id -> FrameworkRestrictionP.AtLeastP id
type FrameworkRestrictionLiteral =
    { LiteraL : FrameworkRestrictionLiteralI; IsNegated : bool }
    member internal x.RawFormular =
        let raw = x.LiteraL.RawFormular
        if x.IsNegated then FrameworkRestrictionP.NotP raw else raw
    static member FromLiteral l =
        { LiteraL = l ; IsNegated = false }
    static member FromNegatedLiteral l =
        { LiteraL = l ; IsNegated = true }
type FrameworkRestrictionAndList =
    { Literals : FrameworkRestrictionLiteral list }
    member internal x.RawFormular =
        FrameworkRestrictionP.AndP (x.Literals |> List.map (fun literal -> literal.RawFormular))

type FrameworkRestriction =
    { OrFormulas : FrameworkRestrictionAndList list }
    member internal x.RawFormular =
        FrameworkRestrictionP.OrP (x.OrFormulas |> List.map (fun andList -> andList.RawFormular))
    override x.ToString() =
        x.RawFormular.ToString()
    member x.IsSubsetOf (y:FrameworkRestriction) =
        x.RawFormular.IsSubsetOf y.RawFormular
    member x.RepresentedFrameworks =
        x.RawFormular.RepresentedFrameworks
    member x.IsMatch tp =
        x.RawFormular.IsMatch tp
module FrameworkRestriction =
    let EmptySet = { OrFormulas = [] } // false
    let NoRestriction = { OrFormulas = [ { Literals = [] } ] } // true
    let AtLeastPlatform pf = { OrFormulas = [ { Literals = [ FrameworkRestrictionLiteral.FromLiteral (AtLeastL pf) ] } ] }
    let Exactly id = AtLeastPlatform (SinglePlatform id)
    let AtLeastPortable (name, fws)= AtLeastPlatform (PortableProfile (name, fws))
    let AtLeast id = { OrFormulas = [ { Literals = [ FrameworkRestrictionLiteral.FromLiteral (AtLeastL (SinglePlatform id)) ] } ] }
    let NotAtLeast id = { OrFormulas = [ { Literals = [ FrameworkRestrictionLiteral.FromNegatedLiteral (AtLeastL (SinglePlatform id)) ] } ] }

    let private simplify (fr:FrameworkRestriction) =
        /// When we have a restriction like (>=net35 && <net45) || >=net45
        /// then we can "optimize" / simplify to (>=net35 || >= net45)
        /// because we don't need to "pseudo" restrict the set with the first restriction 
        /// when we add back later all the things we removed.
        /// Generally: We can remove all negated literals in all clauses when a positive literal exists as a standalone Or clause
        let rec removeNegatedLiteralsWhichOccurSinglePositive (fr:FrameworkRestriction) =
            let positiveSingles =
                fr.OrFormulas
                |> List.choose (fun andFormular -> match andFormular.Literals with [ h ] -> Some h | _ -> None)
            let workDone, reworked =
                fr.OrFormulas
                |> List.fold (fun (workDone, reworkedOrFormulas) andFormula ->
                    let reworkedAnd =
                        andFormula.Literals
                        |> List.filter (fun literal ->
                            positiveSingles
                            |> List.exists (fun p -> literal.IsNegated && literal.LiteraL = p.LiteraL)
                            |> not
                        )
                    if reworkedAnd.Length < andFormula.Literals.Length then
                        true, { Literals = reworkedAnd } :: reworkedOrFormulas
                    else
                        workDone, andFormula :: reworkedOrFormulas
                    ) (false, [])
            if workDone then removeNegatedLiteralsWhichOccurSinglePositive { OrFormulas = reworked }
            else fr
        /// (>= net40-full) && (< net46) && (>= net20) can be simplified to (< net46) && (>= net40-full) because (>= net40-full) is a subset of (>= net20)
        // NOTE: This optimization is kind of dangerous as future frameworks might make it invalid
        // However a lot of tests expect this simplification... We maybe want to remove it (or at least test) after we know the new framework restriction works.
        let removeSubsetLiteralsInAndClause (fr:FrameworkRestriction) =
            let simplifyAndClause (andClause:FrameworkRestrictionAndList) =
                let literals = andClause.Literals
                { Literals =
                    andClause.Literals
                    |> List.filter (fun literal ->
                        // we filter out literals, for which another literal exists which is a subset
                        literals
                        |> Seq.filter (fun l -> l <> literal)
                        |> Seq.exists (fun otherLiteral ->
                            otherLiteral.RawFormular.IsSubsetOf literal.RawFormular)
                        |> not) }
                //andClause
            { OrFormulas = fr.OrFormulas |> List.map simplifyAndClause }
        
        /// (>= net40-full) || (< net46) || (>= net20) can be simplified to (< net46) || (>= net20) because (>= net40-full) is a subset of (>= net20)
        // NOTE: This optimization is kind of dangerous as future frameworks might make it invalid
        // However a lot of tests expect this simplification... We maybe want to remove it (or at least test) after we know the new framework restriction works.
        let removeSubsetLiteralsInOrClause (fr:FrameworkRestriction) =
            let simpleOrLiterals =
                fr.OrFormulas
                |> List.choose (function { Literals = [h] } -> Some h | _ -> None)
            { OrFormulas = 
                fr.OrFormulas
                |> List.filter (function
                    | { Literals = [h] } ->
                        simpleOrLiterals
                        |> Seq.filter (fun l -> l <> h)
                        |> Seq.exists (fun otherLiteral ->
                            h.RawFormular.IsSubsetOf otherLiteral.RawFormular)
                        |> not
                    | _ -> true) }

        /// When we optmized a clause away completely we can replace the hole formula with "NoRestriction"
        /// This happens for example with ( <net45 || >=net45) and the removeNegatedLiteralsWhichOccurSinglePositive
        /// optimization
        let replaceWithNoRestrictionIfAnyLiteralListIsEmpty (fr:FrameworkRestriction) =
            let containsEmptyAnd =
                fr.OrFormulas
                |> Seq.exists (fun andFormular -> andFormular.Literals |> Seq.isEmpty)
            if containsEmptyAnd then NoRestriction else fr

        fr
        |> removeNegatedLiteralsWhichOccurSinglePositive
        |> removeSubsetLiteralsInAndClause
        |> removeSubsetLiteralsInOrClause
        |> replaceWithNoRestrictionIfAnyLiteralListIsEmpty

    let rec private And2 (left : FrameworkRestriction) (right : FrameworkRestriction) =
        match left.OrFormulas with
        | [] -> right
        | [h] ->
            { OrFormulas =
                right.OrFormulas
                |> List.map (fun andFormula -> { Literals = andFormula.Literals @ h.Literals } ) }
        | h :: t ->
            { OrFormulas = (And2 {OrFormulas = [h]} right).OrFormulas @ ((And2 {OrFormulas = t} right).OrFormulas) }
    
    let And (rst:FrameworkRestriction list) =
        List.fold And2 EmptySet rst
        |> simplify
    
    let private Or2 (left : FrameworkRestriction) (right : FrameworkRestriction) =
        { OrFormulas = left.OrFormulas @ right.OrFormulas }
    
    let Or (rst:FrameworkRestriction list) =
        List.fold Or2 NoRestriction rst
        |> simplify
    
    //[<Obsolete ("Method is provided for completeness sake. But I don't think its needed")>]
    //let Not (rst:FrameworkRestriction) =
    //    Unchecked.defaultof<_>

    //let NotLiteral (lit : FrameworkRestrictionLiteral) = { lit with IsNegated = not lit.IsNegated }

    //let fromLiteral = { OrFormulas = [ { Literals = [lit] } ] }

    let Between (x, y) =
        And2 (AtLeast x) (NotAtLeast y)
        //FrameworkRestrictionP.AndP[FrameworkRestrictionP.AtLeastP x; FrameworkRestrictionP.NotP (FrameworkRestrictionP.AtLeastP y)]
    
    let combineRestrictionsWithOr (x : FrameworkRestriction) y =
        Or2 x y
        |> simplify
        //if x.IsSubsetOf y then
        //    y
        //elif y.IsSubsetOf x then
        //    x
        //else
        //    match x, y with
        //    | FrameworkRestriction.AndP[FrameworkRestriction.NotP negate; general], other
        //    | FrameworkRestriction.AndP[general; FrameworkRestriction.NotP negate], other
        //    | other, FrameworkRestriction.AndP[FrameworkRestriction.NotP negate; general]
        //    | other, FrameworkRestriction.AndP[general; FrameworkRestriction.NotP negate] when negate = other ->
        //        // "negate && NOT general" might not be empty, ie general might not be a superset of negate
        //        if other.IsSubsetOf general then general else FrameworkRestriction.OrP[general; other]
        //    | _ -> 
        //        let combined = FrameworkRestriction.OrP[x; y]
        //        if combined.RepresentedFrameworks.Length = FrameworkRestriction.NoRestriction.RepresentedFrameworks.Length then
        //            FrameworkRestriction.NoRestriction
        //        else
        //            combined

    let (|HasNoRestriction|_|) x =
        if x = NoRestriction then Some () else None

    let combineRestrictionsWithAnd (x : FrameworkRestriction) y = 
        And2 x y
        |> simplify
        // combine means basically we say AND (both need to be satisfied)
        //if x.IsSubsetOf y then
        //    x
        //elif y.IsSubsetOf x then
        //    y
        //else
        //    let combined = FrameworkRestriction.AndP[x; y]
        //    if combined.RepresentedFrameworks |> Seq.isEmpty then
        //        FrameworkRestriction.EmptySet
        //    else combined
        //if x.IsSameCategoryAs(y) <> Some false then
        //    combineSameCategoryOrPortableRestrictions x y
        //else
        //    if loose then
        //         match (x.GetOneIdentifier, y.GetOneIdentifier) with
        //         | Some (FrameworkIdentifier.DotNetFramework _ ), Some (FrameworkIdentifier.DotNetStandard _ ) -> [x]
        //         | Some (FrameworkIdentifier.DotNetStandard _ ), Some (FrameworkIdentifier.DotNetFramework _ ) -> [y]
        //         | _ -> []
        //    else
        //        []
type FrameworkRestrictions =
| ExplicitRestriction of FrameworkRestriction
| AutoDetectFramework
    override x.ToString() =
        match x with
        | ExplicitRestriction r -> r.ToString()
        | AutoDetectFramework -> "AutoDetect"
    member x.GetExplicitRestriction () =
        match x with
        | ExplicitRestriction list -> list
        | AutoDetectFramework -> failwith "The framework restriction could not be determined."

    member x.IsSupersetOf (fw:FrameworkRestriction) =
        fw.IsSubsetOf(x.GetExplicitRestriction ())

let getExplicitRestriction (frameworkRestrictions:FrameworkRestrictions) =
    frameworkRestrictions.GetExplicitRestriction()


let parseRestrictionsLegacy failImmediatly (text:string) =
    // TODO: Change this code to convert old "framework" sematics in
    // older lockfiles to the new "restriction" semantics
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
    if text.StartsWith("||") || text.StartsWith("&&") then
        raise <| NotImplementedException("&& and || are not yet implemented.")

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
                let platforms = (PlatformMatching.extractPlatforms framework).Platforms
                if platforms |> List.isEmpty |> not then
                    yield FrameworkRestriction.AtLeastPortable (framework, platforms)
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
                    | Some y -> yield FrameworkRestriction.Between(x, y)
            else
                yield FrameworkRestriction.Exactly x]
    |> List.fold (fun state item -> FrameworkRestriction.combineRestrictionsWithOr state item) FrameworkRestriction.EmptySet

let rec optimizeRestrictions restrictions = 
    restrictions
//    let sorting xs =
//        xs
//        |> List.sortBy (fun x ->
//            match x with
//            | FrameworkRestriction.Exactly r -> r
//            | FrameworkRestriction.Portable _ -> FrameworkIdentifier.MonoMac
//            | FrameworkRestriction.AtLeast r -> r
//            | FrameworkRestriction.Between(min,max) -> min
//            |> fun y -> y,x)
//            
//    match sorting restrictions |> List.distinct with
//    | [] -> []
//    | [x] -> [x]
//    | odered ->
//        let newRestrictions =
//            match odered |> Seq.tryFind (function | FrameworkRestriction.AtLeast r -> true | _ -> false) with
//            | Some((FrameworkRestriction.AtLeast(DotNetFramework(v)) as r)) ->
//                odered
//                |> List.filter (fun r' ->
//                    match r' with
//                    | FrameworkRestriction.Exactly(DotNetFramework(x)) when x > v -> false
//                    | FrameworkRestriction.AtLeast(DotNetFramework(x)) when x > v -> false
//                    | _ -> true)
//            | Some((FrameworkRestriction.AtLeast(DotNetStandard(v)) as r)) ->
//                odered
//                |> List.filter (fun r' ->
//                    match r' with
//                    | FrameworkRestriction.Exactly(DotNetStandard(x)) when x > v -> false
//                    | FrameworkRestriction.AtLeast(DotNetStandard(x)) when x > v -> false
//                    | _ -> true)
//            | _ -> odered
//
//        let filtered =
//            match newRestrictions |> Seq.rev |> Seq.tryFind (function | FrameworkRestriction.AtLeast(DotNetFramework r) -> true | FrameworkRestriction.AtLeast(DotNetStandard r) -> true | _ -> false) with
//            | None -> newRestrictions
//            | Some r ->
//                let currentVersion =
//                    match r with
//                    | FrameworkRestriction.AtLeast(DotNetFramework(x)) -> DotNetFramework x
//                    | FrameworkRestriction.AtLeast(DotNetStandard(x)) -> DotNetStandard x
//                    | x -> failwithf "Unknown .NET moniker %O" x
//                                                                                                           
//                let isLowerVersion (currentVersion:FrameworkIdentifier) compareVersion =
//                    let isMatching (x:FrameworkIdentifier) =
//                        if x = DotNetFramework FrameworkVersion.V3_5 && currentVersion = DotNetFramework FrameworkVersion.V4 then true else
//                        if x = DotNetFramework FrameworkVersion.V3_5 && currentVersion = DotNetFramework FrameworkVersion.V4_Client then true else
//                        if x = DotNetFramework FrameworkVersion.V4_Client && currentVersion = DotNetFramework FrameworkVersion.V4_5 then true else
//                        let hasFrameworksBetween = 
//                            KnownTargetProfiles.DotNetFrameworkVersions 
//                            |> Seq.exists (fun p -> DotNetFramework p > x && DotNetFramework p < currentVersion)
//                        let hasStandardsBetween = 
//                            KnownTargetProfiles.DotNetStandardVersions 
//                            |> Seq.exists (fun p -> DotNetStandard p > x && DotNetStandard p < currentVersion)
//
//                        not hasFrameworksBetween && not hasStandardsBetween
//
//                    match compareVersion with
//                    | FrameworkRestriction.Exactly(DotNetFramework(x)) -> isMatching (DotNetFramework x)
//                    | FrameworkRestriction.Exactly(DotNetStandard(x)) -> isMatching (DotNetStandard x)
//                    | FrameworkRestriction.AtLeast(DotNetFramework(x)) -> 
//                        isMatching (DotNetFramework x) || 
//                            (match currentVersion with
//                             | DotNetFramework(y) when x < y -> true
//                             | _ -> false)
//                    | FrameworkRestriction.AtLeast(DotNetStandard(x)) -> 
//                        isMatching (DotNetStandard x) ||
//                            (match currentVersion with
//                             | DotNetStandard(y) when x < y -> true
//                             | _ -> false)
//                    | _ -> false
//
//                match newRestrictions |> Seq.tryFind (isLowerVersion currentVersion) with
//                | None -> newRestrictions
//                | Some n -> 
//                    let newLowest =
//                        match n with
//                        | FrameworkRestriction.Exactly(DotNetFramework(x)) -> DotNetFramework x
//                        | FrameworkRestriction.AtLeast(DotNetFramework(x)) -> DotNetFramework x                        
//                        | FrameworkRestriction.Exactly(DotNetStandard(x)) -> DotNetStandard x
//                        | FrameworkRestriction.AtLeast(DotNetStandard(x)) -> DotNetStandard x
//                        | x -> failwithf "Unknown .NET moniker %O" x
//
//                    let filtered =
//                        newRestrictions
//                        |> List.filter (fun x -> x <> r && x <> n)
//
//                    filtered @ [FrameworkRestriction.AtLeast(newLowest)]
//
//        if restrictions = filtered then sorting filtered else optimizeRestrictions filtered

//let hasDotNetFrameworkOrAnyCase =
//    List.exists (fun (_,_,rs) ->
//        rs = [] ||
//        rs
//        |> List.exists (function
//                        | FrameworkRestriction.Exactly(DotNetFramework(_)) -> true
//                        | FrameworkRestriction.AtLeast(DotNetFramework(_)) -> true
//                        | _ -> false))

let optimizeDependencies originalDependencies =
    //let grouped = originalDependencies |> List.groupBy (fun (n,v,_) -> n,v)
    //
    //let expanded =
    //    [for (n,vr,r:FrameworkRestriction list) in originalDependencies do
    //        for r' in r do
    //            yield n,vr,r']
    //    |> List.groupBy (fun (_,_,r) -> r)
    //
    //let invertedRestrictions =
    //    [for restriction,packages in expanded do
    //        match restriction with
    //        | FrameworkRestriction.Exactly(DotNetFramework _ as r) -> 
    //            yield r,packages |> List.map (fun (n,v,_) -> n,v)
    //        | _ -> () ]
    //    |> List.sortBy fst
    //
    //let invertedStandardRestrictions =
    //    [for restriction,packages in expanded do
    //        match restriction with
    //        | FrameworkRestriction.Exactly(DotNetStandard _ as r) -> 
    //            yield r,packages |> List.map (fun (n,v,_) -> n,v)
    //        | _ -> () ]
    //    |> List.sortBy fst
    //
    //let globalMax = 
    //    invertedRestrictions
    //    |> List.tryLast
    //    |> Option.map fst
    //
    //let globalStandardMax = 
    //    invertedStandardRestrictions
    //    |> List.tryLast
    //    |> Option.map fst
    //
    //let globalMin = 
    //    invertedRestrictions
    //    |> List.tryHead
    //    |> Option.map fst
    //
    //let globalStandardMin = 
    //    invertedStandardRestrictions
    //    |> List.tryHead
    //    |> Option.map fst
    //
    //let emptyRestrictions =
    //    [for (n,vr,r:FrameworkRestriction list) in originalDependencies do
    //        if r = [] then
    //            yield n,vr]
    //    |> Set.ofList
    //
    //let allRestrictions =
    //    [for (n,vr,r:FrameworkRestriction list) in originalDependencies do
    //        yield r]
    //    |> Set.ofList
    //
    //let restrictionsPerPackage =
    //    originalDependencies
    //    |> List.groupBy (fun (n,vr,r) -> n,vr)
    //    |> List.map (fun ((n,vr),rs) ->
    //        n,vr,
    //         (rs 
    //          |> List.map (fun (_,_,r) -> r)
    //          |> Set.ofList))
    //
    //let packagesWithAllRestrictions =
    //    restrictionsPerPackage
    //    |> List.filter (fun (_,_,rs) -> rs = allRestrictions)
    //    |> List.map (fun (n,vr,_) -> n,vr)
    //    |> Set.ofList
    //
    //let newRestictions =
    //    [for (name,versionRequirement:VersionRequirement),group in grouped do
    //        if name <> PackageName "" then
    //            let hasEmpty = not (Set.isEmpty emptyRestrictions) && Set.contains (name,versionRequirement) emptyRestrictions 
    //            let hasAll = not (Set.isEmpty packagesWithAllRestrictions) && Set.contains (name,versionRequirement) packagesWithAllRestrictions 
    //        
    //            if hasEmpty && hasAll then
    //                yield name,versionRequirement,[]
    //            else
    //                let sorted = 
    //                    group 
    //                    |> List.map (fun (_,_,res) -> res) 
    //                    |> List.concat
    //                    |> List.distinct
    //                    |> List.sort
    //
    //                let localMaxDotNetRestriction = findMaxDotNetRestriction sorted
    //                let localMaxStandardRestriction = findMaxStandardRestriction sorted
    //                let globalMax = defaultArg globalMax localMaxDotNetRestriction
    //                let globalStandardMax = defaultArg globalStandardMax localMaxStandardRestriction
    //
    //                let plain =
    //                    match sorted with
    //                    | [] ->                            
    //                        let globalStandardMin = defaultArg globalStandardMin localMaxDotNetRestriction
    //                        
    //                        let frameworks =
    //                            match globalMin with
    //                            | Some globalMin ->
    //                                KnownTargetProfiles.DotNetFrameworkVersions 
    //                                |> List.filter (fun fw -> DotNetFramework(fw) < globalMin)
    //                                |> List.map (fun fw -> FrameworkRestriction.Exactly(DotNetFramework(fw)))
    //                            | _ ->
    //                                KnownTargetProfiles.DotNetFrameworkVersions                                     
    //                                |> List.map (fun fw -> FrameworkRestriction.Exactly(DotNetFramework(fw)))
    //
    //                        let standards =
    //                            KnownTargetProfiles.DotNetStandardVersions 
    //                            |> List.filter (fun fw -> DotNetStandard(fw) < globalStandardMin)
    //                            |> List.map (fun fw -> FrameworkRestriction.Exactly(DotNetStandard(fw)))
    //
    //                        frameworks @ standards
    //
    //                    | _ -> sorted
    //
    //                let dotnetRestrictions,others = 
    //                    plain
    //                    |> List.partition (fun p ->
    //                        match p with
    //                        | FrameworkRestriction.Exactly(DotNetFramework(_)) -> true 
    //                        | FrameworkRestriction.AtLeast(DotNetFramework(_)) -> true 
    //                        | FrameworkRestriction.Exactly(DotNetStandard(_)) -> true 
    //                        | FrameworkRestriction.AtLeast(DotNetStandard(_)) -> true 
    //                        | _ -> false)
    //
    //                let restrictions' = 
    //                    dotnetRestrictions
    //                    |> List.map (fun restriction ->
    //                        match restriction with
    //                        | FrameworkRestriction.Exactly(DotNetFramework _ as r) ->
    //                            if r = localMaxDotNetRestriction && r = globalMax then
    //                                FrameworkRestriction.AtLeast r
    //                            else
    //                                restriction
    //                        | FrameworkRestriction.Exactly (DotNetStandard _ as r) ->
    //                            if r = localMaxStandardRestriction && r = globalStandardMax then
    //                                FrameworkRestriction.AtLeast r
    //                            else
    //                                restriction
    //                        | _ -> restriction)
    //
    //                let restrictions = optimizeRestrictions restrictions'
    //
    //                yield name,versionRequirement,others @ restrictions]

    //let hasDotNetFrameworkOrStandard =
    //    newRestictions
    //    |> List.exists (fun (_,_,rs) ->
    //        rs
    //        |> List.exists (function
    //                        | FrameworkRestriction.Exactly(DotNetFramework(_)) -> true 
    //                        | FrameworkRestriction.AtLeast(DotNetFramework(_)) -> true 
    //                        | FrameworkRestriction.Exactly(DotNetStandard(_)) -> true 
    //                        | FrameworkRestriction.AtLeast(DotNetStandard(_)) -> true
    //                        | _ -> false))

    //if not hasDotNetFrameworkOrStandard then 
    //    newRestictions 
    //else
    //    let newRestictions =
    //        if hasDotNetFrameworkOrAnyCase originalDependencies then 
    //            newRestictions 
    //        else
    //            newRestictions
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
                    

        //newRestictions
        //|> List.map (fun (p,v,rs) ->
        //    let filtered =
        //        rs
        //        |> List.map (fun r ->
        //            match r with
        //            | FrameworkRestriction.Portable (portable, fws) ->
        //                let newPortable =
        //                    portable.Split('+')
        //                    |> Array.filter (fun s -> s.StartsWith "net" |> not)
        //                    |> fun xs -> String.Join("+",xs)
        //                let newFws =
        //                    fws
        //                    |> List.filter (fun fw ->
        //                        fw.IsSameCategoryAs (DotNetFramework FrameworkVersion.V1) ||
        //                        fw.IsSameCategoryAs (DotNetUnity DotNetUnityVersion.V3_5_Web) ||
        //                        fw.IsSameCategoryAs (DotNetStandard DotNetStandardVersion.V1_0) ||
        //                        fw.IsSameCategoryAs (DotNetCore DotNetCoreVersion.V1_0)
        //                        |> not)
        //                FrameworkRestriction.Portable (newPortable, newFws)
        //            | _ -> r)
        //    p,v,filtered)

    originalDependencies
    |> List.map (fun (a,b,c) -> a,b, ExplicitRestriction c)


let filterRestrictions (list1:FrameworkRestrictions) (list2:FrameworkRestrictions) =
    match list1,list2 with 
    | ExplicitRestriction FrameworkRestriction.HasNoRestriction, AutoDetectFramework -> AutoDetectFramework
    | AutoDetectFramework, ExplicitRestriction FrameworkRestriction.HasNoRestriction -> AutoDetectFramework
    | AutoDetectFramework, AutoDetectFramework -> AutoDetectFramework
    | ExplicitRestriction fr1 , ExplicitRestriction fr2 -> ExplicitRestriction (FrameworkRestriction.combineRestrictionsWithAnd fr1 fr2)
        //let filtered =
        //    match list1, list2 with
        //    | [],_ -> list2
        //    | _,[] -> list1
        //    | _ ->
        //        [for x in list1 do
        //            for y in list2 do
        //                let c = combineRestrictions false x y
        //                if c <> [] then yield! c]
        //
        //let tryLoose = 
        //    (filtered |> List.exists (fun r -> match r.GetOneIdentifier with | Some (FrameworkIdentifier.DotNetFramework _ ) -> true | _ -> false) |> not) &&
        //        (list2 |> List.exists (fun r -> match r.GetOneIdentifier with | Some (FrameworkIdentifier.DotNetFramework _ ) -> true | _ -> false))
        //
        //let filtered = 
        //    if tryLoose then
        //        match list1, list2 with
        //        | [],_ -> list2
        //        | _,[] -> list1
        //        | _ ->
        //            [for x in list1 do
        //                for y in list2 do
        //                    let c = combineRestrictions true x y
        //                    if c <> [] then yield! c]
        //    else
        //        filtered
        //
        //let optimized =
        //    filtered
        //    |> optimizeRestrictions
        //FrameworkRestrictionList optimized
    | _ -> failwithf "The framework restriction %O and %O could not be combined." list1 list2

/// Get if a target should be considered with the specified restrictions
let isTargetMatchingRestrictions (restriction:FrameworkRestriction, target)=
    restriction.IsMatch target
    
    //match target with
    //| SinglePlatform pf -> restriction.RepresentedFrameworks |> Seq.contains pf
    //| PortableProfile (_, fws) ->
    //    // TODO: Review - check if we need to use exists instead of forall?
    //    fws
    //    |> Seq.forall (fun fw ->
    //        restriction.RepresentedFrameworks |> Seq.contains fw)

    //memoize <| fun (restrictions:FrameworkRestriction list, target) ->
    //    if List.isEmpty restrictions then true else
    //    match target with
    //    | SinglePlatform pf ->
    //        restrictions
    //        |> List.exists (fun restriction ->
    //                match restriction with
    //                | FrameworkRestriction.Exactly (Native(NoBuildMode,NoPlatform)) -> 
    //                    match pf with 
    //                    | Native(_) -> true 
    //                    | _ -> false
    //                | FrameworkRestriction.Exactly fw ->
    //                    pf = fw
    //                | FrameworkRestriction.Portable _ -> false
    //                | FrameworkRestriction.AtLeast fw ->
    //                    pf.IsAtLeast(fw)
    //                | FrameworkRestriction.Between(min,max) ->
    //                    pf.IsBetween(min,max))
    //    | _ ->
    //        restrictions
    //        |> List.exists (fun restriction ->
    //                match restriction with
    //                | FrameworkRestriction.Portable _ -> true
    //                | _ -> false)

/// Get all targets that should be considered with the specified restrictions
let applyRestrictionsToTargets (restriction:FrameworkRestriction) (targets: TargetProfile list) =
    targets 
    |> List.filter (fun t -> isTargetMatchingRestrictions(restriction,t))

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
          FrameworkRestrictions = ExplicitRestriction FrameworkRestriction.NoRestriction
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
              | ExplicitRestriction FrameworkRestriction.HasNoRestriction -> ()
              | AutoDetectFramework -> ()
              | ExplicitRestriction fr -> yield "restriction: " + (fr.ToString())
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
                match getPair "restriction" with
                | Some s ->
                    // TODO: Change to new parser.
                    ExplicitRestriction(parseRestrictionsLegacy true s)
                | _ ->
                    match getPair "framework" with
                    | Some s -> ExplicitRestriction(parseRestrictionsLegacy true s)
                    | _ -> ExplicitRestriction FrameworkRestriction.NoRestriction
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

let addFrameworkRestrictionsToDependencies rawDependencies frameworkGroups =
    let frameworkGroupPaths =
        frameworkGroups
        |> Seq.map (fun fw -> {PlatformMatching.ParsedPlatformPath.Name = fw.ToString(); PlatformMatching.ParsedPlatformPath.Platforms = [fw] })
        |> Seq.toList
    let referenced =
        rawDependencies
        |> List.groupBy (fun (n:PackageName,req,pp:PlatformMatching.ParsedPlatformPath) -> n,req)
        |> List.map (fun ((name, req), group) ->
            // We need to append all the other platforms we support.
            let packageGroups = group |> List.map (fun (_,_,packageGroup) -> packageGroup)
            let restrictions =
                packageGroups
                |> List.map (fun packageGroup ->
                    let packageGroupRestriction =
                        match packageGroup.Platforms with
                        | _ when System.String.IsNullOrEmpty packageGroup.Name -> FrameworkRestriction.NoRestriction
                        | [] -> FrameworkRestriction.NoRestriction
                        | [ pf ] -> FrameworkRestriction.AtLeast pf
                        | _ -> FrameworkRestriction.AtLeastPortable(packageGroup.Name, packageGroup.Platforms)
                    
                    frameworkGroups
                    |> Seq.filter (fun frameworkGroup ->
                        // special casing for portable -> should be removed once portable is a normal FrameworkIdentifier
                        if packageGroup.Platforms.Length < 2 then packageGroup.Platforms |> Seq.contains frameworkGroup |> not else true)
                    // TODO: Check if this is needed (I think the logic below is a general version of this subset logic)
                    |> Seq.filter (fun frameworkGroup ->
                        // filter all restrictions which would render this group to nothing (ie smaller restrictions)
                        // filter out unrelated restrictions
                        packageGroupRestriction.IsSubsetOf (FrameworkRestriction.AtLeast frameworkGroup) |> not)
                    |> Seq.fold (fun curRestr frameworkGroup ->
                        // We start with the restriction inherently given by the current group,
                        // But this is too broad as other groups might "steal" better suited frameworks
                        // So we subtract all "bigger" groups ('frameworkGroup' parameter).
                        // Problem is that this is too strict as there might be an intersection that now is assigned nowhere
                        // Example would be two groups with netstandard13 and net451 which will generate 
                        // (>=net451 && <=netstandard13) for one and (>=netstandard13 && <=net451) for the other group
                        // but now net461 which supports netstandard13 is nowhere -> we need to decide here and add back the intersection

                        let missing = FrameworkRestriction.combineRestrictionsWithAnd curRestr (FrameworkRestriction.AtLeast frameworkGroup)
                        let combined = FrameworkRestriction.combineRestrictionsWithAnd curRestr (FrameworkRestriction.NotAtLeast frameworkGroup)
                        match packageGroup.Platforms, missing.RepresentedFrameworks with
                        | [ packageGroupFw ], firstMissing :: _ ->
                            // the common set goes to the better matching one
                            match PlatformMatching.findBestMatch (frameworkGroupPaths, firstMissing) with
                            | Some { PlatformMatching.ParsedPlatformPath.Platforms = [ cfw ] } when cfw = packageGroupFw -> curRestr
                            | _ -> combined
                        | _ -> combined) packageGroupRestriction)
            let combinedRestrictions = restrictions |> List.fold FrameworkRestriction.combineRestrictionsWithOr FrameworkRestriction.EmptySet
            name, req, combinedRestrictions
        )
        //|> List.append frameworks

    // While this is correct we want a more generic representation, such that future platforms "just work"
    // I'll leave this code for now to better understand that the above is doing the same thing, but resulting in a more generallized restriction.
    //let availablePlatforms = referenced |> List.map (fun (_,_,pp) -> pp)
    //let calculateDistribution = PlatformMatching.getSupportedTargetProfiles availablePlatforms
    //
    //let referenced =
    //    referenced
    //    |> List.map (fun (name, req, pp) ->
    //        let restriction =
    //            calculateDistribution.[pp]
    //            |> Seq.fold (fun state profile ->
    //                FrameworkRestriction.Or(state, FrameworkRestriction.ExactlyProfile profile)) FrameworkRestriction.EmptySet
    //        name, req, restriction)
    //

    optimizeDependencies referenced