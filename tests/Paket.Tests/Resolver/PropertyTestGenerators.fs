module Resolver.PropertyTestGenerators

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.PackageResolver
open FsCheck
open System.Diagnostics

let chooseFromList xs =
  gen {
    let! i = Gen.choose (0, List.length xs-1)
    return (List.item i xs) }

type PackageList = (PackageName*SemVerInfo) list
type Dependency = PackageName * VersionRequirement
type PackageGraph = (PackageName*SemVerInfo*Dependency list) list

type ResolverPuzzle = PackageGraph * Dependency list

type PackageTypes =
    static member PackageName() =
        Arb.generate<NonNegativeInt>
        |> Gen.map (fun x -> PackageName("P" + (int x).ToString()))
        |> Arb.fromGen

    static member Versions() =
         let minor =
             Arb.generate<NonNegativeInt*NonNegativeInt*NonNegativeInt>
             |> Gen.map (fun (major,minor,patch) -> SemVer.Parse(sprintf "%d.%d.%d" (int major) (int minor) (int patch)))

         let build =
             Arb.generate<NonNegativeInt*NonNegativeInt*NonNegativeInt*PositiveInt>
             |> Gen.map (fun (major,minor,patch,build) -> SemVer.Parse(sprintf "%d.%d.%d.%d" (int major) (int minor) (int patch) (int build)))

         Gen.oneof [ minor; build ]
         |> Arb.fromGen

    static member DistinctPackages() =
        Arb.generate<(PackageName * SemVerInfo list) list>
        |> Gen.map (fun packages ->
            packages
            |> List.collect (fun (p,vs) -> vs |> List.map (fun v -> p,v))
            |> List.distinct
            |> List.sort
            : PackageList)
        |> Arb.fromGen

    static member GenerateDependenciesForPackage (p,v) =
        let between =
            Arb.generate<SemVerInfo*SemVerInfo>
            |> Gen.eval 10 (Random.newSeed())
            |> fun (x,y) ->
                if x < y then
                    VersionRequirement(VersionRange.Between(x.ToString(),y.ToString()),PreReleaseStatus.All)
                else
                    VersionRequirement(VersionRange.Between(y.ToString(),y.ToString()),PreReleaseStatus.All)

        [ p,between
          p,VersionRequirement(VersionRange.Specific(v),PreReleaseStatus.All)
          p,VersionRequirement(VersionRange.Minimum(v),PreReleaseStatus.All)
          p,VersionRequirement(VersionRange.Maximum(v),PreReleaseStatus.All)
          p,VersionRequirement(VersionRange.AtLeast("0"),PreReleaseStatus.All)]

    static member GenerateDependencies max (packages:PackageList) =
        if packages = [] then [] else
        let r = System.Random()
        Gen.listOf (chooseFromList packages)
        |> Gen.map (fun dependencies ->
            dependencies
            |> List.map PackageTypes.GenerateDependenciesForPackage
            |> List.concat
            |> List.sortBy (fun _ -> r.Next()))
        |> Gen.eval max (Random.newSeed())          // create deps
        |> List.distinctBy fst
        |> List.sort

    static member ShrinkGraph (g:PackageGraph) : PackageGraph seq =
        seq {
            // remove one package
            for p,v,deps in g do
                yield g |> List.filter (fun (p',v',deps') -> p <> p' || v <> v')

            // remove one dependency
            for p,v,deps in g do
                for d in deps do
                    yield g |> List.map (fun  (p',v',deps') -> if p = p' && v = v' then p,v,deps |> List.filter ((<>) d) else p',v',deps')
        }

    static member FullGraph() : Arbitrary<PackageGraph> =
        let generator =
            Arb.generate<PackageList>
            |> Gen.map (fun packages ->
                    packages
                    |> List.map (fun (p,v) ->
                        let deps = PackageTypes.GenerateDependencies 4 packages
                        p,v,deps))

        Arb.fromGenShrink (generator,PackageTypes.ShrinkGraph)

    static member ShrinkPuzzle ((g,deps):ResolverPuzzle) : ResolverPuzzle seq =
        seq {
            // remove one dependency
            for d in deps do
                yield g, deps |> List.filter ((<>) d)

            // shrink graph
            for g' in PackageTypes.ShrinkGraph g do
                yield g',deps
        }

    static member Puzzle() : Arbitrary<ResolverPuzzle> =

        let generator =
            Arb.generate<PackageGraph>
            |> Gen.map (fun g -> g,g |> List.map (fun (p,v,_) -> p,v) |> PackageTypes.GenerateDependencies 100)

        Arb.fromGenShrink (generator,PackageTypes.ShrinkPuzzle)

[<DebuggerStepThrough>]
let check p =
    Arb.register<PackageTypes>() |> ignore
    Check.QuickThrowOnFailure p

let resolve (g:PackageGraph) (deps:(PackageName*VersionRequirement) list) =
    let deps = deps |> List.map (fun (p,vr) -> p.ToString(),vr.Range)
    let g =
        g
        |> List.map (fun (p,v,deps) -> p.ToString(),v.ToString(),deps |> List.map (fun (d,vr) -> d.ToString(),vr))
        |> OfSimpleGraph
    safeResolve g deps
