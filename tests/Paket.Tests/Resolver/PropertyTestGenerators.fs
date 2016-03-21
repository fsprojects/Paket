module Resovler.PropertyTestGenerators

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
type PackageGraph = (PackageName*SemVerInfo*(Dependency list)) list

type ResolverPuzzle = PackageGraph * (Dependency list)

type PackageTypes =
    static member PackageName() = 
        Arb.from<NonNegativeInt>
        |> Arb.convert 
            (fun x -> PackageName("P" + (int x).ToString())) 
            (fun x -> x.ToString().Replace("P","") |> int |> NonNegativeInt)

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
        Arb.generate<(PackageName * (SemVerInfo list)) list>
        |> Gen.map (fun packages ->
            packages
            |> List.map (fun (p,vs) -> vs |> List.map (fun v -> p,v))
            |> List.concat
            |> List.distinct
            |> List.sort
            : PackageList)
        |> Arb.fromGen

    static member Dependencies packages =
        if packages = [] then [] else
        let r = System.Random()
        Gen.listOf (chooseFromList packages)
        |> Gen.map (fun dependencies ->
            dependencies
            |> List.map (fun (p,v) -> 
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
                  p,VersionRequirement(VersionRange.AtLeast("0"),PreReleaseStatus.All)])
            |> List.concat
            |> List.sortBy (fun _ -> r.Next()))
        |> Gen.eval 100 (Random.newSeed())          // create deps
        |> List.distinctBy fst
        |> List.sort

    static member DependenciesForPackage packages package =
        PackageTypes.Dependencies packages
        |> List.filter (fun (d,vr) -> d <> package)

    static member ShrinkGraph (g:PackageGraph) : PackageGraph seq = 
        seq {
            // remove one dependency
            for (p,v,deps) in g do
                for d in deps do
                    yield g |> List.map (fun  (p',v',deps') -> if p = p' && v = v' then p,v,deps |> List.filter ((<>) d) else p',v',deps')


            // remove one package
            for (p,v,deps) in g do
                yield g |> List.filter (fun (p',v',deps') -> p <> p' || v <> v')
        }

    static member FullGraph() : Arbitrary<PackageGraph> =
        let generator =
            Arb.generate<PackageList>
            |> Gen.map (fun packages ->
                    packages 
                    |> List.map (fun (p,vs) -> p,vs, PackageTypes.DependenciesForPackage packages p))
                    
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
            |> Gen.map (fun g -> g,g |> List.map (fun (p,v,_) -> p,v) |> PackageTypes.Dependencies)

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
    safeResolve g deps    
