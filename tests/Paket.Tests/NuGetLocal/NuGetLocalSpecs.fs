module Paket.NuGetLocalSpecs

open Paket.Domain
open NUnit.Framework
open FsUnit
open Paket.NuGetLocal
open System.IO
open Pri.LongPath


[<Test>]
let ``package file name split to id and version correctly``() =
    [ 
        "package.1.2.3.nupkg", Some (PackageName "package", SemVer.Parse "1.2.3")
        "package.1.2.3-alpha2.nupkg", Some (PackageName "package", SemVer.Parse "1.2.3-alpha2")
        "package.name.with.dots.1.2.3-alpha2.nupkg", Some (PackageName "package.name.with.dots", SemVer.Parse "1.2.3-alpha2") 
        "package.1.2.3.nupkg.back", None
        "package.name.without.version.nupkg", None
    ]
    |> List.iter 
        (fun (input, expectedResult) ->
            let actualResult = parsePackageInfoFromFileName input 
            shouldEqual expectedResult actualResult)

[<Test>]
let ``package file picked up correctly``() = 
    [   // folder * inputPackageId * inputPackageVersion * expectedFileName
        "./NuGetLocal/case1", PackageName "package.name", SemVer.Parse "0.1.0-alpha2", "package.name.0.1.0-alpha2.nupkg"
        "./NuGetLocal/case2", PackageName "package.name", SemVer.Parse "0.1.0-alpha2", "package.name.0.1.0.0-alpha2.nupkg"
        
        "./NuGetLocal/case1", PackageName "package.name", SemVer.Parse "1.2", "package.name.1.2.0.nupkg"
        "./NuGetLocal/case1", PackageName "package.name", SemVer.Parse "0.1.2", "package.name.0.1.2.nupkg"

        "./NuGetLocal/case2", PackageName "package.name", SemVer.Parse "1.2", "package.name.1.2.0.0.nupkg"
        
        "./NuGetLocal", PackageName "package.in.nested.folder", SemVer.Parse "1.0", "package.in.nested.folder.1.0.0.nupkg"
    ]
    |> List.iter
        (fun (folder, packageId, packageVersion, expectedFileName) ->
            let actualResult = findLocalPackage folder packageId packageVersion
            shouldEqual expectedFileName actualResult.Name)