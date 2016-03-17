module Resovler.PropertyTests

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.PackageResolver
open FsCheck
open System.Diagnostics

let chooseFromList xs = 
  gen { let! i = Gen.choose (0, List.length xs-1) 
        return (List.item i xs) }

type PackageList = (PackageName*SemVerInfo) list
type PackageGraph = (PackageName*SemVerInfo*((PackageName * VersionRequirement) list)) list

type PackageTypes =
    static member PackageName() = 
        Arb.from<int>
        |> Arb.convert (fun x -> PackageName("P" + x.ToString())) (fun x -> x.ToString().Replace("P","") |> int)

    static member GroupName() = 
        Arb.from<int>
        |> Arb.convert (fun x -> PackageName("G" + x.ToString())) (fun x -> x.ToString().Replace("G","") |> int)

    static member Versions() = 
         Arb.generate<NonNegativeInt*NonNegativeInt*NonNegativeInt>
         |> Gen.map (fun (major,minor,patch) -> SemVer.Parse(sprintf "%d.%d.%d" (int major) (int minor) (int patch)))
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

    static member FullGraph() : Arbitrary<PackageGraph> =
        let nestedGenerator =
             Arb.generate<PackageList>
             |> Gen.map (fun packages ->
                        let depsGenerator = 
                            Gen.listOf (chooseFromList packages)
                            |> Gen.map (List.map (fun (p,v) -> p,VersionRequirement(VersionRange.Specific(v),PreReleaseStatus.All)))
                    

                        packages 
                        |> List.map (fun (p,vs) -> 
                            let deps =
                                depsGenerator
                                |> Gen.eval 10 (Random.newSeed())     // create deps
                                |> List.filter (fun (d,vr) -> d <> p) // not to same package
                                |> List.distinctBy fst
                                |> List.sort

                            p,vs,deps))
        
        Arb.fromGen nestedGenerator

[<DebuggerStepThrough>]
let check p =
    Arb.register<PackageTypes>() |> ignore
    Check.QuickThrowOnFailure p


[<Test>]
let ``should only resolve packages that are available`` () =
    check (fun (g:PackageGraph) -> 
        g
        |> List.forall (fun (p,v,d) -> d |> List.length < 3))