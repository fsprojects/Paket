module Paket.Requirements

open System
open Paket
open Paket.Domain
open Paket.PackageSources
open Paket.Logging
open Paket.PlatformMatching

[<RequireQualifiedAccess>]
// To make reasoning and writing tests easier.
// Ideally we would "simplify" the trees to a "normal" form internally
[<CustomEquality; CustomComparison>]
[<System.Diagnostics.DebuggerDisplay("{InfixNotation}")>]
type FrameworkRestrictionP =
    private
    | ExactlyP of TargetProfile
    | AtLeastP of TargetProfile
    // Means: Take all frameworks NOT given by the restriction
    | NotP of FrameworkRestrictionP
    | OrP of FrameworkRestrictionP list
    | AndP of FrameworkRestrictionP list
    member x.InfixNotation =
        match x with
        | FrameworkRestrictionP.ExactlyP r -> "== " + r.ToString()
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
        | FrameworkRestrictionP.ExactlyP r -> "== " + r.ToString()
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
    // NOTE: All critical paths test only if this set is empty, so we use lazy seq here
    member x.RepresentedFrameworks =
        match x with
        | FrameworkRestrictionP.ExactlyP r -> [ r ] |> Set.ofList
        | FrameworkRestrictionP.AtLeastP r -> r.PlatformsSupporting
            //PlatformMatching.get r
            //KnownTargetProfiles.AllProfiles
            //|> Set.filter (fun plat -> r.IsSupportedBy plat)
        | FrameworkRestrictionP.NotP(fr) ->
            let notTaken = fr.RepresentedFrameworks
            Set.difference KnownTargetProfiles.AllProfiles notTaken
        | FrameworkRestrictionP.OrP (frl) ->
            frl
            |> Seq.map (fun fr -> fr.RepresentedFrameworks)
            |> Set.unionMany
        | FrameworkRestrictionP.AndP (frl) ->
            match frl with
            | h :: _ ->
                frl
                |> Seq.map (fun fr -> fr.RepresentedFrameworks)
                |> Set.intersectMany
            | [] -> 
                KnownTargetProfiles.AllProfiles

    member x.IsMatch (tp:TargetProfile) =
        match x with
        | FrameworkRestrictionP.ExactlyP r -> r = tp
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
    
        // better ~ 5 Mins, but below recursive logic should be even better.
        let inline fallBack doAssert (x:FrameworkRestrictionP) (y:FrameworkRestrictionP) =
#if DEBUG
            if doAssert then
                assert (false)// make sure the fallback is never needed
#endif
            let superset = y.RepresentedFrameworks
            let subset = x.RepresentedFrameworks
            Set.isSubset subset superset

        // Because the formula simplifier needs it this is a quite HOT PATH
        let inline isSubsetOfCalculation x y =
            match x with
            | FrameworkRestrictionP.ExactlyP x' ->
                match y with
                | FrameworkRestrictionP.ExactlyP y' -> x' = y'
                | FrameworkRestrictionP.AtLeastP y' ->
                    // =x' is a subset of >=y' when 'y is smaller than 'x
                    y'.IsSmallerThanOrEqual x'
                // these are or 'common' forms, others are not allowed
                | FrameworkRestrictionP.NotP(FrameworkRestrictionP.AtLeastP y') ->
                    // =x is only a subset of <y when its not a subset of >= y
                    y'.IsSmallerThanOrEqual x'
                    |> not
                | FrameworkRestrictionP.NotP(FrameworkRestrictionP.ExactlyP y') ->
                    x' <> y'
                // This one should never actually hit.
                | FrameworkRestrictionP.NotP(y') -> fallBack true x y
                | FrameworkRestrictionP.OrP (ys) ->
                    ys
                    |> Seq.exists (fun y' -> x.IsSubsetOf y')
                | FrameworkRestrictionP.AndP (ys) ->
                    ys
                    |> Seq.forall (fun y' -> x.IsSubsetOf y')
            | FrameworkRestrictionP.AtLeastP x' ->
                match y with
                | FrameworkRestrictionP.ExactlyP y' ->
                    // >=x can only be a subset of =y when it is already the max
                    x' = y' && x'.SupportedPlatforms.IsEmpty
                | FrameworkRestrictionP.AtLeastP y' ->
                    // >=x is only a subset of >=y when y is 'smaller" than x
                    y'.IsSmallerThanOrEqual x'
                // these are or 'common' forms, others are not allowed
                | FrameworkRestrictionP.NotP(FrameworkRestrictionP.AtLeastP y') ->
                    // >= x' is only a subset of < y' when their intersection is empty
                    Set.intersect (x'.PlatformsSupporting) (y'.PlatformsSupporting)
                    |> Set.isEmpty
                | FrameworkRestrictionP.NotP(FrameworkRestrictionP.ExactlyP y') ->
                    // >= x' is only a subset of <> y' when y' is not part of >=x'
                    x'.PlatformsSupporting
                    |> Set.contains y'
                    |> not
                // This one should never actually hit.
                | FrameworkRestrictionP.NotP(y') -> fallBack true x y
                | FrameworkRestrictionP.OrP (ys) ->
                    ys
                    |> Seq.exists (fun y' -> x.IsSubsetOf y')
                | FrameworkRestrictionP.AndP (ys) ->
                    ys
                    |> Seq.forall (fun y' -> x.IsSubsetOf y')
                    
            // these are or 'common' forms, others are not allowed
            | FrameworkRestrictionP.NotP(FrameworkRestrictionP.AtLeastP x' as notX) ->
                match y with
                | FrameworkRestrictionP.ExactlyP y' ->
                    // < x is a subset of ='y when?
#if DEBUG
                    assert (not (fallBack false x y))// TODO: can this happen?
#endif
                    false 
                | FrameworkRestrictionP.AtLeastP y' ->
                    // < x is a subset of >= y when there are no smaller things than y and
#if DEBUG
                    assert (not (fallBack false x y))// TODO: can this happen?
#endif
                    false 
                // these are or 'common' forms, others are not allowed
                | FrameworkRestrictionP.NotP(FrameworkRestrictionP.AtLeastP y' as notY) ->
                    // < 'x is a subset of < y when >=y is a subset of >=x
                    notY.IsSubsetOf notX
                | FrameworkRestrictionP.NotP(FrameworkRestrictionP.ExactlyP y' as notY) ->
                    // < 'x is a subset of <> y when =y is a subset of >=x
                    notY.IsSubsetOf notX
                // This one should never actually hit.
                | FrameworkRestrictionP.NotP(y') -> fallBack true x y
                | FrameworkRestrictionP.OrP (ys) ->
                    ys
                    |> Seq.exists (fun y' -> x.IsSubsetOf y')
                | FrameworkRestrictionP.AndP (ys) ->
                    ys
                    |> Seq.forall (fun y' -> x.IsSubsetOf y')
            | FrameworkRestrictionP.NotP(FrameworkRestrictionP.ExactlyP x' as notX) ->
                match y with
                | FrameworkRestrictionP.ExactlyP y' ->
                    // <> x is a subset of =y ?
#if DEBUG
                    assert (not (fallBack false x y))// TODO: can this happen?
#endif
                    false
                | FrameworkRestrictionP.AtLeastP y' ->
                    // <> x is a subset of >= y
#if DEBUG
                    assert (not (fallBack false x y))// TODO: can this happen?
#endif
                    false
                    //fallBack()
                // these are or 'common' forms, others are not allowed
                | FrameworkRestrictionP.NotP(FrameworkRestrictionP.AtLeastP y' as notY) ->
                    notY.IsSubsetOf notX
                | FrameworkRestrictionP.NotP(FrameworkRestrictionP.ExactlyP y' as notY) ->
                    notY.IsSubsetOf notX
                // This one should never actually hit.
                | FrameworkRestrictionP.NotP(y') -> fallBack true x y
                | FrameworkRestrictionP.OrP (ys) ->
                    ys
                    |> Seq.exists (fun y' -> x.IsSubsetOf y')
                | FrameworkRestrictionP.AndP (ys) ->
                    ys
                    |> Seq.forall (fun y' -> x.IsSubsetOf y')
            // This one should never actually hit.
            | FrameworkRestrictionP.NotP(x') -> fallBack true x y
            | FrameworkRestrictionP.OrP (xs) ->
                xs
                |> Seq.forall (fun x' -> x'.IsSubsetOf y)
            | FrameworkRestrictionP.AndP (xs) ->
                xs
                |> Seq.exists (fun x' -> x'.IsSubsetOf y)

        isSubsetOfCalculation x y

        // Bad ~ 10 mins
        //|> Seq.forall (fun inner -> superset |> Seq.contains inner)
    static member ExactlyFramework (tf: FrameworkIdentifier) =
        ExactlyP (SinglePlatform tf)

    override x.Equals(y) = 
        match y with 
        | :? FrameworkRestrictionP as r ->
            if System.Object.ReferenceEquals(x, r) then true
            elif (x.ToString() = r.ToString()) then true
            else r.RepresentedFrameworks = x.RepresentedFrameworks 
        | _ -> false
    override x.GetHashCode() = x.RepresentedFrameworks.GetHashCode()
    interface System.IComparable with
        member x.CompareTo(y) =
            match y with
            | :? FrameworkRestrictionP as r ->
                if System.Object.ReferenceEquals(x, r) then 0
                elif (x.ToString() = r.ToString()) then 0
                else compare x.RepresentedFrameworks r.RepresentedFrameworks
            | _ -> failwith "wrong type"

type FrameworkRestrictionLiteralI =
    | ExactlyL of TargetProfile
    | AtLeastL of TargetProfile
    member internal x.RawFormular =
        match x with
        | ExactlyL id -> FrameworkRestrictionP.ExactlyP id
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

[<CustomEquality; CustomComparison>]
type FrameworkRestriction =
    private { OrFormulas : FrameworkRestrictionAndList list
              mutable PrivateRawFormula : FrameworkRestrictionP option ref
              mutable PrivateRepresentedFrameworks : TargetProfile Set option ref }
    static member FromOrList l = { OrFormulas = l; PrivateRepresentedFrameworks = ref None; PrivateRawFormula = ref None }
    static member internal WithOrListInternal orList l = { l with OrFormulas = orList }
    member internal x.RawFormular =
        match !x.PrivateRawFormula with
        | Some f -> f
        | None ->
            let raw = FrameworkRestrictionP.OrP (x.OrFormulas |> List.map (fun andList -> andList.RawFormular))
            x.PrivateRawFormula := Some raw
            raw
    override x.ToString() =
        x.RawFormular.ToString()
    member x.IsSubsetOf (y:FrameworkRestriction) =
        x.RawFormular.IsSubsetOf y.RawFormular
    member x.RepresentedFrameworks =
        match !x.PrivateRepresentedFrameworks with
        | Some s -> s
        | None ->
            let set = x.RawFormular.RepresentedFrameworks
            x.PrivateRepresentedFrameworks := Some set
            set
    member x.IsMatch tp =
        x.RawFormular.IsMatch tp
    override x.Equals(y) =
        match y with 
        | :? FrameworkRestriction as r ->
            // Cannot delegate because we cache RepresentedFrameworks -> optimization
            //x.RawFormular.Equals(r.RawFormular)
            if System.Object.ReferenceEquals(x, y) then true
            elif (x.ToString() = y.ToString()) then true
            else r.RepresentedFrameworks = x.RepresentedFrameworks
        | _ -> false
    override x.GetHashCode() = x.RepresentedFrameworks.GetHashCode()
    interface System.IComparable with
        member x.CompareTo(y) = 
            match y with 
            | :? FrameworkRestriction as r ->
                // Cannot delegate because we cache RepresentedFrameworks -> optimization
                //compare x.RawFormular r.RawFormular
                if System.Object.ReferenceEquals(x, y) then 0
                elif (x.ToString() = y.ToString()) then 0
                else compare x.RepresentedFrameworks r.RepresentedFrameworks
            | _ -> failwith "wrong type"

module FrameworkRestriction =
    let EmptySet = FrameworkRestriction.FromOrList [] // false
    let NoRestriction = FrameworkRestriction.FromOrList [ { Literals = [] } ] // true
    let FromLiteral lit = FrameworkRestriction.FromOrList [ { Literals = [ lit ] } ]
    let AtLeastPlatform pf = FromLiteral (FrameworkRestrictionLiteral.FromLiteral (AtLeastL pf))
    let ExactlyPlatform pf = FromLiteral (FrameworkRestrictionLiteral.FromLiteral (ExactlyL pf))
    let Exactly id = ExactlyPlatform (SinglePlatform id)
    let AtLeastPortable (name, fws) = AtLeastPlatform (TargetProfile.FindPortable false fws)
    let AtLeast id = AtLeastPlatform (SinglePlatform id)
    let NotAtLeastPlatform pf = FromLiteral (FrameworkRestrictionLiteral.FromNegatedLiteral (AtLeastL pf))
    let NotAtLeast id = NotAtLeastPlatform (SinglePlatform id)

    let private simplify (fr:FrameworkRestriction) =
        /// When we have a restriction like (>=net35 && <net45) || >=net45
        /// then we can "optimize" / simplify to (>=net35 || >= net45)
        /// because we don't need to "pseudo" restrict the set with the first restriction 
        /// when we add back later all the things we removed.
        /// Generally: We can remove all negated literals in all clauses when a positive literal exists as a standalone Or clause
        let rec removeNegatedLiteralsWhichOccurSinglePositive (fr:FrameworkRestriction) =
            let positiveSingles =
                fr.OrFormulas
                |> List.choose (fun andFormular -> match andFormular.Literals with [ { IsNegated = false } as h ] -> Some h | _ -> None)
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
            if workDone then removeNegatedLiteralsWhichOccurSinglePositive (FrameworkRestriction.WithOrListInternal reworked fr)
            else fr
        /// (>= net40-full) && (< net46) && (>= net20) can be simplified to (< net46) && (>= net40-full) because (>= net40-full) is a subset of (>= net20)
        // NOTE: This optimization is kind of dangerous as future frameworks might make it invalid
        // However a lot of tests expect this simplification... We maybe want to remove it (or at least test) after we know the new framework restriction works.
        let removeSubsetLiteralsInAndClause (fr:FrameworkRestriction) =
            let simplifyAndClause (andClause:FrameworkRestrictionAndList) =
                let literals = andClause.Literals
                let newLiterals =
                    literals
                    |> List.filter (fun literal ->
                        // we filter out literals, for which another literal exists which is a subset
                        literals
                        |> Seq.filter (fun l -> l <> literal)
                        |> Seq.exists (fun otherLiteral ->
                            otherLiteral.RawFormular.IsSubsetOf literal.RawFormular)
                        |> not)
                if newLiterals.Length <> literals.Length
                then true, {Literals = newLiterals}
                else false, andClause
                //andClause
            let wasChanged, newOrList =
                fr.OrFormulas
                |> List.fold (fun (oldWasChanged, newList) andList ->
                    let wasChanged, newAndList = simplifyAndClause andList
                    oldWasChanged || wasChanged, newAndList :: newList) (false, [])
            if wasChanged then
                FrameworkRestriction.WithOrListInternal newOrList fr
            else fr
        
        /// (>= net40-full) || (< net46) || (>= net20) can be simplified to (< net46) || (>= net20) because (>= net40-full) is a subset of (>= net20)
        // NOTE: This optimization is kind of dangerous as future frameworks might make it invalid
        // However a lot of tests expect this simplification... We maybe want to remove it (or at least test) after we know the new framework restriction works.
        let removeSubsetLiteralsInOrClause (fr:FrameworkRestriction) =
            let simpleOrLiterals =
                fr.OrFormulas
                |> List.choose (function { Literals = [h] } -> Some h | _ -> None)
            let newOrList =
                fr.OrFormulas
                |> List.filter (function
                    | { Literals = [h] } ->
                        simpleOrLiterals
                        |> Seq.filter (fun l -> l <> h)
                        |> Seq.exists (fun otherLiteral ->
                            h.RawFormular.IsSubsetOf otherLiteral.RawFormular)
                        |> not
                    | _ -> true)
            if newOrList.Length < fr.OrFormulas.Length then
                FrameworkRestriction.WithOrListInternal newOrList fr
            else fr

        /// ((>= net20) && (>= net40)) || (>= net20) can be simplified to (>= net20) because any AND clause with (>= net20) can be removed.
        let removeUneccessaryOrClauses (fr:FrameworkRestriction) =
            let orClauses =
                fr.OrFormulas
            let isContained (andList:FrameworkRestrictionAndList) (item:FrameworkRestrictionAndList) =
                if item.Literals.Length >= andList.Literals.Length then false
                else
                    item.Literals
                    |> Seq.forall (fun lit -> andList.Literals |> Seq.contains lit)

            let newOrList =
                fr.OrFormulas
                |> List.filter (fun orClause ->
                    orClauses |> Seq.exists (isContained orClause) |> not)

            if newOrList.Length < fr.OrFormulas.Length then
                FrameworkRestriction.WithOrListInternal newOrList fr
            else fr

        /// clauses with ((>= net20) && (< net20) && ...) can be removed because they contains a literal and its negation.
        let removeUneccessaryAndClauses (fr:FrameworkRestriction) =
            let newOrList =
                fr.OrFormulas
                |> List.filter (fun andList ->
                    let normalizeLiterals =
                        andList.Literals
                        |> List.map (function
                            | { LiteraL = FrameworkRestrictionLiteralI.ExactlyL l; IsNegated = n } ->
                                { LiteraL = FrameworkRestrictionLiteralI.AtLeastL l; IsNegated = n }
                            | lit -> lit)
                    let foundLiteralAndNegation =
                        normalizeLiterals
                        |> Seq.exists (fun l ->
                            normalizeLiterals |> Seq.contains { l with IsNegated = not l.IsNegated})
                    not foundLiteralAndNegation)
                    
            if newOrList.Length < fr.OrFormulas.Length then
                FrameworkRestriction.WithOrListInternal newOrList fr
            else fr


        /// When we optmized a clause away completely we can replace the hole formula with "NoRestriction"
        /// This happens for example with ( <net45 || >=net45) and the removeNegatedLiteralsWhichOccurSinglePositive
        /// optimization
        let replaceWithNoRestrictionIfAnyLiteralListIsEmpty (fr:FrameworkRestriction) =
            let containsEmptyAnd =
                fr.OrFormulas
                |> Seq.exists (fun andFormular -> andFormular.Literals |> Seq.isEmpty)
            if containsEmptyAnd then NoRestriction else fr
        
        let sortClauses (fr:FrameworkRestriction) =
            fr.OrFormulas
            |> List.map (fun andFormula -> { Literals = andFormula.Literals |> List.distinct |> List.sort })
            |> List.distinct
            |> List.sort 
            |> fun newOrList -> FrameworkRestriction.WithOrListInternal newOrList fr
        let optimize fr =
            fr
            |> removeNegatedLiteralsWhichOccurSinglePositive
            |> removeSubsetLiteralsInAndClause
            |> removeSubsetLiteralsInOrClause
            |> removeUneccessaryAndClauses
            |> removeUneccessaryOrClauses
            |> replaceWithNoRestrictionIfAnyLiteralListIsEmpty
            |> sortClauses
        let mutable hasChanged = true
        let mutable newFormula = fr
        while hasChanged do
            let old = newFormula
            newFormula <- optimize newFormula
            if old = newFormula then hasChanged <- false
        newFormula
            

    let rec private And2 (left : FrameworkRestriction) (right : FrameworkRestriction) =
        match left.OrFormulas, right.OrFormulas with
        // false -> stay false
        | [], _ -> true, left
        | _, [] -> true, right
        // true -> use the other
        | _, [ { Literals = [] }] -> true, left
        | [{ Literals = [] }], _ -> true, right
        | otherFormulas, [h]
        | [h], otherFormulas ->
            false,
            otherFormulas
            |> List.map (fun andFormula -> { Literals = andFormula.Literals @ h.Literals } )
            |> FrameworkRestriction.FromOrList
        | h :: t, _ ->
            false,
            ((And2 (FrameworkRestriction.FromOrList [h]) right) |> snd).OrFormulas @ ((And2 (FrameworkRestriction.FromOrList t) right) |> snd).OrFormulas
            |> FrameworkRestriction.FromOrList

    let And (rst:FrameworkRestriction list) =
        let isSimple, result =
            List.fold
                (fun (isSimplified, current) next -> let wasSimple, result = And2 current next in wasSimple && isSimplified, result)
                (true, NoRestriction)
                rst
        if isSimple then result
        else simplify result

    let private Or2 (left : FrameworkRestriction) (right : FrameworkRestriction) =
        match left.OrFormulas, right.OrFormulas with
        // false -> use the other
        | [], _ -> true, right
        | _, [] -> true, left
        // true -> become true
        | _, [ { Literals = [] }] -> true, right
        | [{ Literals = [] }], _ -> true, left
        | leftFormumas, rightFormulas ->
            false,
            leftFormumas @ rightFormulas
            |> FrameworkRestriction.FromOrList

    let Or (rst:FrameworkRestriction list) =
        let isSimple, result =
            List.fold
                (fun (isSimplified, current) next -> let wasSimple, result = Or2 current next in wasSimple && isSimplified, result)
                (true, EmptySet)
                rst
        if isSimple then result
        else simplify result

    //[<Obsolete ("Method is provided for completeness sake. But I don't think its needed")>]
    //let Not (rst:FrameworkRestriction) =
    //    Unchecked.defaultof<_>

    let Between (x, y) =
        let isSimple, result = And2 (AtLeast x) (NotAtLeast y)
        if isSimple then result else simplify result

    let combineRestrictionsWithOr (x : FrameworkRestriction) y =
        let isSimple, result = Or2 x y
        if isSimple then result else simplify result

    let (|HasNoRestriction|_|) x =
        if x = NoRestriction then Some () else None

    let combineRestrictionsWithAnd (x : FrameworkRestriction) y =
        let isSimple, result = And2 x y
        if isSimple then result else simplify result

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

type RestrictionParseProblem =
    | ParseFramework of string
    | ParseSecondOperator of string
    | UnsupportedPortable of string
    member x.AsMessage =
        match x with
        | ParseFramework framework ->  sprintf "Could not parse framework '%s'. Try to update or install again or report a paket bug." framework
        | ParseSecondOperator item -> sprintf "Could not parse second framework of between operator '%s'. Try to update or install again or report a paket bug." item
        | UnsupportedPortable item -> sprintf "Profile '%s' is not a supported portable profile" item
    member x.Framework =
        match x with
        | RestrictionParseProblem.UnsupportedPortable fm
        | RestrictionParseProblem.ParseSecondOperator fm
        | RestrictionParseProblem.ParseFramework fm -> fm
    member x.IsCritical =
        match x with
        | RestrictionParseProblem.UnsupportedPortable _ -> false
        | RestrictionParseProblem.ParseSecondOperator _
        | RestrictionParseProblem.ParseFramework _ -> true
let parseRestrictionsLegacy failImmediatly (text:string) =
    // older lockfiles to the new "restriction" semantics
    let problems = ResizeArray<_>()
    let handleError (p:RestrictionParseProblem) =
        if failImmediatly && p.IsCritical then
            failwith p.AsMessage
        else
            problems.Add p
    let text =
        // workaround missing spaces
        text.Replace("<=","<= ").Replace(">=",">= ").Replace("=","= ")
    if text.StartsWith("||") || text.StartsWith("&&") then
        raise <| InvalidOperationException("&& and || are not allowed in a legacy 'framework' section.")

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
            match PlatformMatching.extractPlatforms false framework |> Option.bind (fun pp -> 
                let prof = pp.ToTargetProfile false
                if prof.IsSome && prof.Value.IsUnsupportedPortable then
                    handleError <| (RestrictionParseProblem.UnsupportedPortable framework)
                prof) with
            | Some profile ->
                yield FrameworkRestriction.AtLeastPlatform profile
            | None ->
                handleError <| (RestrictionParseProblem.ParseFramework framework)
        | Some x -> 
            if operatorSplit.[0] = ">=" then
                if operatorSplit.Length < 4 then
                    yield FrameworkRestriction.AtLeast x
                else
                    let item = operatorSplit.[3]
                    match FrameworkDetection.Extract(item) with
                    | None ->
                        handleError <| (RestrictionParseProblem.ParseSecondOperator item)
                    | Some y -> yield FrameworkRestriction.Between(x, y)
            else
                yield FrameworkRestriction.Exactly x]
    |> List.fold (fun state item -> FrameworkRestriction.combineRestrictionsWithOr state item) FrameworkRestriction.EmptySet,
    problems.ToArray()

let private parseRestrictionsRaw (text:string) =
    let problems = ResizeArray<_>()
    let handleError (p:RestrictionParseProblem) =
        problems.Add p

    let rec parseOperator (text:string) =
        match text.Trim() with
        | t when String.IsNullOrEmpty t -> failwithf "trying to parse an otherator but got no content"
        | h when h.StartsWith ">=" || h.StartsWith "==" || h.StartsWith "<" ->
            // parse >=
            let smallerThan = h.StartsWith "<"
            let isEquals = h.StartsWith "=="
            let rest = h.Substring 2
            let splitted = rest.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
            if splitted.Length < 1 then failwithf "No parameter after >= or < in '%s'" text
            let rawOperator = splitted.[0]
            let operator = rawOperator.TrimEnd([|')'|])
            match PlatformMatching.extractPlatforms false operator |> Option.bind (fun (pp:PlatformMatching.ParsedPlatformPath) ->
                let prof = pp.ToTargetProfile false
                if prof.IsSome && prof.Value.IsUnsupportedPortable then
                    handleError <| (RestrictionParseProblem.UnsupportedPortable operator)
                prof) with
            | None -> failwithf "invalid parameter '%s' after >= or < in '%s'" operator text
            | Some plat ->
                let f =
                    if smallerThan then FrameworkRestriction.NotAtLeastPlatform
                    elif isEquals then FrameworkRestriction.ExactlyPlatform
                    else FrameworkRestriction.AtLeastPlatform
                let operatorIndex = text.IndexOf operator
                f plat, text.Substring(operatorIndex + operator.Length)
        | h when h.StartsWith "&&" || h.StartsWith "||" ->
            let isAnd = h.StartsWith "&&"
            let next = h.Substring 2
            let rec parseOperand cur (next:string) =
                let trimmed = next.TrimStart()
                if trimmed.StartsWith "(" then
                    let operand, remaining = parseOperator (trimmed.Substring 1)
                    let remaining = remaining.TrimStart()
                    if remaining.StartsWith ")" |> not then failwithf "expected ')' after operand, '%s'" text
                    parseOperand (operand::cur) (remaining.Substring 1)
                else
                    cur, next

            let operands, next = parseOperand [] next
            if operands.Length = 0 then failwithf "Operand '%s' without argument is invalid in '%s'" (h.Substring (0, 2)) text
            let f, def = if isAnd then FrameworkRestriction.And, FrameworkRestriction.NoRestriction else FrameworkRestriction.Or, FrameworkRestriction.EmptySet
            operands |> f, next
        | h when h.StartsWith "NOT" ->
            let next = h.Substring 2

            if next.TrimStart().StartsWith "(" then
                let operand, remaining = parseOperator (next.Substring 1)
                let remaining = remaining.TrimStart()
                if remaining.StartsWith ")" |> not then failwithf "expected ')' after operand, '%s'" text
                let next = remaining.Substring 1

                let negated =
                    match operand with
                    | { OrFormulas = [ {Literals = [ lit] } ] } ->
                        [ {Literals = [ { lit with IsNegated = not lit.IsNegated } ] } ]
                        |> FrameworkRestriction.FromOrList
                    |  _ -> failwithf "a general NOT is not implemted jet (and shouldn't be emitted for now)"
                negated, next
            else
                failwithf "Expected operand after NOT, '%s'" text
        | h when h.StartsWith "true" ->
            let rest = (h.Substring 4).TrimStart()
            FrameworkRestriction.NoRestriction, rest
        | h when h.StartsWith "false" ->
            let rest = (h.Substring 5).TrimStart()
            FrameworkRestriction.EmptySet, rest
        | _ ->
            failwithf "Expected operator, but got '%s'" text

    let result, next = parseOperator text
    if String.IsNullOrEmpty next |> not then
        failwithf "Successfully parsed '%O' but got additional text '%s'" result next
    result,
    problems.ToArray()

let parseRestrictions = memoize parseRestrictionsRaw

let filterRestrictions (list1:FrameworkRestrictions) (list2:FrameworkRestrictions) =
    match list1,list2 with 
    | ExplicitRestriction FrameworkRestriction.HasNoRestriction, AutoDetectFramework -> AutoDetectFramework
    | AutoDetectFramework, ExplicitRestriction FrameworkRestriction.HasNoRestriction -> AutoDetectFramework
    | AutoDetectFramework, AutoDetectFramework -> AutoDetectFramework
    | ExplicitRestriction fr1 , ExplicitRestriction fr2 -> ExplicitRestriction (FrameworkRestriction.combineRestrictionsWithAnd fr1 fr2)
    | _ -> failwithf "The framework restriction %O and %O could not be combined." list1 list2

/// Get if a target should be considered with the specified restrictions
let isTargetMatchingRestrictions (restriction:FrameworkRestriction, target)=
    restriction.IsMatch target

/// Get all targets that should be considered with the specified restrictions
let applyRestrictionsToTargets (restriction:FrameworkRestriction) (targets: TargetProfile Set) =
    Set.intersect targets restriction.RepresentedFrameworks

type ContentCopySettings =
| Omit
| Overwrite
| OmitIfExisting

type CopyToOutputDirectorySettings =
| Never
| Always
| PreserveNewest

[<RequireQualifiedAccess>]
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
      SpecificVersion : bool option
      StorageConfig : PackagesFolderGroupConfig option
      Excludes : string list
      Aliases : Map<string,string>
      CopyContentToOutputDirectory : CopyToOutputDirectorySettings option 
      GenerateLoadScripts : bool option }

    static member Default =
        { CopyLocal = None
          SpecificVersion = None
          StorageConfig = None
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
              match this.SpecificVersion with
              | Some x -> yield "specific_version: " + x.ToString().ToLower()
              | None -> ()
              match this.StorageConfig with
              | Some (PackagesFolderGroupConfig.NoPackagesFolder) -> yield "storage: none"
              | Some (PackagesFolderGroupConfig.GivenPackagesFolder s) -> failwithf "Not implemented yet."
              | Some (PackagesFolderGroupConfig.DefaultPackagesFolder) -> yield "storage: packages"
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
              | Some BindingRedirectsSettings.On -> yield "redirects: on"
              | Some BindingRedirectsSettings.Off -> yield "redirects: off"
              | Some BindingRedirectsSettings.Force -> yield "redirects: force"
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
                StorageConfig = self.StorageConfig ++ other.StorageConfig
                FrameworkRestrictions = filterRestrictions self.FrameworkRestrictions other.FrameworkRestrictions
                OmitContent = self.OmitContent ++ other.OmitContent
                CopyLocal = self.CopyLocal ++ other.CopyLocal
                SpecificVersion = self.SpecificVersion ++ other.SpecificVersion
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
              StorageConfig =
                match getPair "storage" with
                | Some "packages" -> Some (PackagesFolderGroupConfig.DefaultPackagesFolder)
                | Some "none" -> Some (PackagesFolderGroupConfig.NoPackagesFolder)
                | _ -> None
              FrameworkRestrictions =
                match getPair "restriction" with
                | Some s -> ExplicitRestriction(parseRestrictions s |> fst)
                | _ ->
                    match getPair "framework" with
                    | Some s ->
                        let parsed, _ = parseRestrictionsLegacy true s
                        ExplicitRestriction(parsed)
                    | _ -> ExplicitRestriction FrameworkRestriction.NoRestriction
              OmitContent =
                match getPair "content" with
                | Some "none" -> Some ContentCopySettings.Omit 
                | Some "once" -> Some ContentCopySettings.OmitIfExisting
                | Some "true" -> Some ContentCopySettings.Overwrite
                | _ ->  None
              CreateBindingRedirects =
                match getPair "redirects" with
                | Some "on" -> Some BindingRedirectsSettings.On 
                | Some "off" -> Some BindingRedirectsSettings.Off
                | Some "force" -> Some BindingRedirectsSettings.Force
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
              SpecificVersion =
                match getPair "specific_version" with
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
      Graph: PackageRequirement Set
      Sources: PackageSource list
      TransitivePrereleases: bool 
      IsCliTool : bool
      Settings: InstallSettings }

    override this.Equals(that) = 
        match that with
        | :? PackageRequirement as that -> 
            this.Name = that.Name && 
            this.VersionRequirement = that.VersionRequirement && 
            this.ResolverStrategyForTransitives = that.ResolverStrategyForTransitives && 
            this.ResolverStrategyForDirectDependencies = that.ResolverStrategyForDirectDependencies && 
            this.Settings.FrameworkRestrictions = that.Settings.FrameworkRestrictions &&
            this.TransitivePrereleases = that.TransitivePrereleases &&
            this.IsCliTool = that.IsCliTool &&
            this.Parent = that.Parent
        | _ -> false

    override this.ToString() =
        sprintf "%O %O (from %O)" this.Name this.VersionRequirement this.Parent


    override this.GetHashCode() = hash (this.Name,this.VersionRequirement)
    
    member this.IncludingPrereleases(releaseStatus) = 
        { this with VersionRequirement = VersionRequirement(this.VersionRequirement.Range,releaseStatus) }

    member this.IncludingPrereleases() = this.IncludingPrereleases(PreReleaseStatus.All)    

    member this.Depth = this.Graph.Count

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
        let c = compare x.Parent y.Parent
        if c <> 0 then c else
        let c = compare x.Name y.Name
        if c <> 0 then c else
        let c = compare x.TransitivePrereleases y.TransitivePrereleases
        if c <> 0 then c else
        let c = compare x.Settings.FrameworkRestrictions y.Settings.FrameworkRestrictions
        if c <> 0 then c else 0

    interface System.IComparable with
       member this.CompareTo that = 
          match that with 
          | :? PackageRequirement as that ->
                PackageRequirement.Compare(this,that,None,0,0)
          | _ -> invalidArg "that" "cannot compare value of different types"

type AddFrameworkRestrictionWarnings =
    | UnknownPortableProfile of TargetProfile
    member x.Format name version =
        match x with
        | UnknownPortableProfile p ->
            sprintf "Profile %O is not a supported portable profile, please tell the package authors of %O %O" p name version

let addFrameworkRestrictionsToDependencies rawDependencies (frameworkGroups:ParsedPlatformPath list) =
    let problems = ResizeArray<_>()
    let handleProblem (p:AddFrameworkRestrictionWarnings) =
        problems.Add p
    let referenced =
        rawDependencies
        |> List.groupBy (fun (n:PackageName,req,pp:ParsedPlatformPath) -> n,req)
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
                    |> Seq.choose (fun g ->
                        let prof = g.ToTargetProfile false
                        if prof.IsSome && prof.Value.IsUnsupportedPortable then
                            handleProblem <| UnknownPortableProfile prof.Value
                        prof)
                    // TODO: Check if this is needed (I think the logic below is a general version of this subset logic)
                    |> Seq.filter (fun frameworkGroup ->
                        // filter all restrictions which would render this group to nothing (ie smaller restrictions)
                        // filter out unrelated restrictions
                        packageGroupRestriction.IsSubsetOf (FrameworkRestriction.AtLeastPlatform frameworkGroup) |> not)
                    |> Seq.fold (fun curRestr frameworkGroup ->
                        // We start with the restriction inherently given by the current group,
                        // But this is too broad as other groups might "steal" better suited frameworks
                        // So we subtract all "bigger" groups ('frameworkGroup' parameter).
                        // Problem is that this is too strict as there might be an intersection that now is assigned nowhere
                        // Example would be two groups with netstandard13 and net451 which will generate 
                        // (>=net451 && <=netstandard13) for one and (>=netstandard13 && <=net451) for the other group
                        // but now net461 which supports netstandard13 is nowhere -> we need to decide here and add back the intersection

                        let missing = FrameworkRestriction.combineRestrictionsWithAnd curRestr (FrameworkRestriction.AtLeastPlatform frameworkGroup)
                        let combined = lazy FrameworkRestriction.combineRestrictionsWithAnd curRestr (FrameworkRestriction.NotAtLeastPlatform frameworkGroup)
                        match packageGroup.Platforms, missing.RepresentedFrameworks.IsEmpty with
                        | [ packageGroupFw ], false ->
                            // the common set goes to the better matching one
                            match PlatformMatching.findBestMatch (frameworkGroups, missing.RepresentedFrameworks.MinimumElement) with
                            | Some { PlatformMatching.ParsedPlatformPath.Platforms = [ cfw ] } when cfw = packageGroupFw -> curRestr
                            | _ -> combined.Value
                        | _ -> combined.Value) packageGroupRestriction)
            let combinedRestrictions = restrictions |> List.fold FrameworkRestriction.combineRestrictionsWithOr FrameworkRestriction.EmptySet
            name, req, combinedRestrictions
        )

    referenced
    |> List.map (fun (a,b,c) -> a,b, ExplicitRestriction c),
    problems.ToArray()