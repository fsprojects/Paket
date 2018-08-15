module Paket.InstallModel.PaketPropsTests

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open System.Xml.Linq

let ns = "http://schemas.microsoft.com/developer/msbuild/2003"
let xname name = XName.Get(name, ns)

let checkContainsPackageRefs pkgRefs (group: XElement) =

    let isPackageReference name (x: XElement) =
        if x.Name = (xname "PackageReference") then
            match x.Attribute(XName.Get "Include") with
            | null -> false
            | v -> v.Value = name
        else
            false

    let hasVersion version (x: XElement) =
        x.Elements(xname "Version")
        |> Seq.tryHead
        |> Option.map (fun x -> x.ToString() = version)
        |> function Some true -> true | _ -> false

    let packageRefs = group.Elements(xname "PackageReference") |> Seq.toList
    Assert.AreEqual(pkgRefs |> List.length, packageRefs |> Seq.length, (sprintf "%A" group))
    for (pkgName, pkgVersion) in pkgRefs do
        let pkg =
            packageRefs
            |> List.filter (isPackageReference pkgName)
            |> List.filter (hasVersion pkgVersion)
            |> List.tryHead
        match pkg with
        | Some p -> ()
        | None ->
            Assert.Fail("expected package '%s' with version '%s' not found in '%A' group")


[<Test>]
let ``should create props file for design mode``() = 

    let lockFile = """RESTRICTION: && (>= net461) (< net47)
NUGET
  remote: https://api.nuget.org/v3/index.json
    Argu (4.2.1)
      FSharp.Core (>= 3.1.2)
    FSharp.Core (3.1.2.5)

GROUP Other1
RESTRICTION: == netstandard2.0
NUGET
  remote: https://api.nuget.org/v3/index.json
    FsCheck (2.8.2)
      FSharp.Core (>= 3.1.2.5)
    FSharp.Core (4.3.4)"""

    let refFileContent = """
FSharp.Core
Argu

group Other1
  FSharp.Core
  FsCheck
"""

    let lockFile = LockFile.Parse("", toLines lockFile)

    let refFile = ReferencesFile.FromLines(toLines refFileContent) // .Groups.[Constants.MainDependencyGroup]

    let packages =
        [ for kv in refFile.Groups do
            let packagesInGroup,_ = lockFile.GetOrderedPackageHull(kv.Key, refFile)
            yield! packagesInGroup ]

    let outPath = System.IO.Path.GetTempFileName()
    Paket.RestoreProcess.createPaketPropsFile lockFile Seq.empty packages (FileInfo outPath)

    let doc = XDocument.Load(outPath, LoadOptions.PreserveWhitespace)

    let itemGroups = doc.Root.Elements (xname "ItemGroup") |> Seq.toList
            
    match itemGroups with
    | [groupMain; otherGroup] ->
        groupMain
        |> checkContainsPackageRefs [ "FSharp.Core","1"; "Argu","2" ] 
        otherGroup
        |> checkContainsPackageRefs [ "FSharp.Core","1"; "Argu","2" ] 
    | l ->
        Assert.Fail(sprintf "expected two ItemGroup but was '%A'" l)
