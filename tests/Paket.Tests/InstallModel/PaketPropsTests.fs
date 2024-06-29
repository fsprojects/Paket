module Paket.InstallModel.PaketPropsTests

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open System.Xml.Linq

let ns = "http://schemas.microsoft.com/developer/msbuild/2003"
let xname name = XName.Get(name, ns)

let checkTargetFrameworkCondition msbuildCondition (itemGroup: XElement) =
    match itemGroup.Attribute(XName.Get "Condition") with
    | null -> Assert.Fail(sprintf "Expected attribute 'Condition' but element was '%A'" itemGroup)
    | a ->
        let condition = a.Value
        Assert.AreEqual("($(DesignTimeBuild) == true)" + msbuildCondition, condition)

let checkTargetFrameworkNoRestriction itemGroup =
    checkTargetFrameworkCondition "" itemGroup

let checkTargetFrameworkRestriction rc r itemGroup =
    let msbuildCond = r |> Paket.Requirements.getExplicitRestriction |> fun c -> PlatformMatching.getCondition rc c.RepresentedFrameworks
    checkTargetFrameworkCondition (sprintf " AND (%s)" msbuildCond) itemGroup

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
        |> Option.map (fun x -> x.Value = version)
        |> Option.exists id

    let packageRefs = group.Elements(xname "PackageReference") |> Seq.toList
    Assert.AreEqual(pkgRefs |> List.length, packageRefs |> Seq.length, (sprintf "%A" group))
    for pkgName, pkgVersion in pkgRefs do
        let pkg =
            packageRefs
            |> List.filter (isPackageReference pkgName)
            |> List.filter (hasVersion pkgVersion)
            |> List.tryHead
        match pkg with
        | Some p -> ()
        | None ->
            Assert.Fail(sprintf "expected package '%s' with version '%s' not found in '%A' group" pkgName pkgVersion group)


[<Test>]
let ``should create props file for design mode``() = 

    let lockFile = """NUGET
  remote: https://api.nuget.org/v3/index.json
    Argu (4.2.1)
      FSharp.Core (>= 3.1.2)
    FSharp.Core (3.1.2.5)

GROUP Other1
NUGET
  remote: https://api.nuget.org/v3/index.json
    FsCheck (2.8.2)
      FSharp.Core (>= 3.1.2.5)
"""

    let refFileContent = """
FSharp.Core
Argu

group Other1
  FsCheck
"""

    let lockFile = LockFile.Parse("", toLines lockFile)

    let refFile = ReferencesFile.FromLines(toLines refFileContent)

    let packages =
        [ for kv in refFile.Groups do
            let packagesInGroup,_ = lockFile.GetOrderedPackageHull(kv.Key, refFile)
            yield! packagesInGroup ]

    let outPath = System.IO.Path.GetTempFileName()
    Paket.RestoreProcess.createPaketPropsFile lockFile Seq.empty packages (FileInfo outPath)

    let doc = XDocument.Load(outPath, LoadOptions.PreserveWhitespace)

    let itemGroups = doc.Root.Elements (xname "ItemGroup") |> Seq.toList
            
    match itemGroups with
    | [groupMain] ->
        groupMain
        |> checkTargetFrameworkNoRestriction
        groupMain
        |> checkContainsPackageRefs [ "FSharp.Core","3.1.2.5"; "Argu","4.2.1"; "FsCheck","2.8.2" ] 
    | l ->
        Assert.Fail(sprintf "expected one ItemGroup but was '%A'" l)


[<Test>]
let ``should not inherit alias settings``() = 

    let lockFile = """NUGET
  remote: https://api.nuget.org/v3/index.json
    Argu (4.2.1)
      FSharp.Core (>= 3.1.2)
    FSharp.Core (3.1.2.5)
      Newtonsoft.Json (>= 11.0.2) 
    Newtonsoft.Json (11.0.5)
"""

    let refFileContent = """
Argu
  alias Argu.dll Argu_Alias

"""

    let lockFile = LockFile.Parse("", toLines lockFile)

    let refFile = ReferencesFile.FromLines(toLines refFileContent)

    let packages =
        [ for kv in refFile.Groups do
            let packagesInGroup,_ = lockFile.GetOrderedPackageHull(kv.Key, refFile)
            yield! packagesInGroup ]

    let (_, p, _) = packages[0]
    Assert.Zero(p.Settings.Aliases.Count)

    let (_, p, _) = packages[1]
    Assert.Zero(p.Settings.Aliases.Count)

    let (_, p, _) = packages[2]
    Assert.AreEqual(1, p.Settings.Aliases.Count)

[<Test>]
let ``should create props file for design mode with group restrictions``() = 

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
    FSharp.Core (4.3.4)
"""

    let refFileContent = """
FSharp.Core
Argu

group Other1
  FSharp.Core
  FsCheck
"""

    let lockFile = LockFile.Parse("", toLines lockFile)

    let refFile = ReferencesFile.FromLines(toLines refFileContent)

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
        |> checkTargetFrameworkRestriction lockFile.Groups.[Constants.MainDependencyGroup].Options.Settings.ReferenceCondition lockFile.Groups.[Constants.MainDependencyGroup].Options.Settings.FrameworkRestrictions
        groupMain
        |> checkContainsPackageRefs [ "FSharp.Core","3.1.2.5"; "Argu","4.2.1" ] 
        otherGroup
        |> checkTargetFrameworkRestriction lockFile.Groups.[Domain.GroupName "Other1"].Options.Settings.ReferenceCondition lockFile.Groups.[Domain.GroupName "Other1"].Options.Settings.FrameworkRestrictions
        otherGroup
        |> checkContainsPackageRefs [ "FSharp.Core","4.3.4"; "FsCheck","2.8.2" ] 
    | l ->
        Assert.Fail(sprintf "expected two ItemGroup but was '%A'" l)

[<Test>]
let ``should create props file for design mode with group conditions``() = 

    let lockFile = """CONDITION: COND_MAIN
NUGET
  remote: https://api.nuget.org/v3/index.json
    Argu (4.2.1)
      FSharp.Core (>= 3.1.2)
    FSharp.Core (3.1.2.5)

GROUP Other1
CONDITION: COND_OTHER1
NUGET
  remote: https://api.nuget.org/v3/index.json
    FsCheck (2.8.2)
      FSharp.Core (>= 3.1.2.5)
    FSharp.Core (4.3.4)
"""

    let refFileContent = """
FSharp.Core
Argu

group Other1
  FSharp.Core
  FsCheck
"""

    let lockFile = LockFile.Parse("", toLines lockFile)

    let refFile = ReferencesFile.FromLines(toLines refFileContent)

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
        |> checkTargetFrameworkRestriction lockFile.Groups.[Constants.MainDependencyGroup].Options.Settings.ReferenceCondition lockFile.Groups.[Constants.MainDependencyGroup].Options.Settings.FrameworkRestrictions
        groupMain
        |> checkContainsPackageRefs [ "FSharp.Core","3.1.2.5"; "Argu","4.2.1" ] 
        otherGroup
        |> checkTargetFrameworkRestriction lockFile.Groups.[Domain.GroupName "Other1"].Options.Settings.ReferenceCondition lockFile.Groups.[Domain.GroupName "Other1"].Options.Settings.FrameworkRestrictions
        otherGroup
        |> checkContainsPackageRefs [ "FSharp.Core","4.3.4"; "FsCheck","2.8.2" ] 
    | l ->
        Assert.Fail(sprintf "expected two ItemGroup but was '%A'" l)

[<Test>]
let ``should create props file for design mode with group restrictions and conditions``() = 

    let lockFile = """CONDITION: COND_MAIN
RESTRICTION: && (>= net461) (< net47)
NUGET
  remote: https://api.nuget.org/v3/index.json
    Argu (4.2.1)
      FSharp.Core (>= 3.1.2)
    FSharp.Core (3.1.2.5)

GROUP Other1
CONDITION: COND_OTHER1
RESTRICTION: == netstandard2.0
NUGET
  remote: https://api.nuget.org/v3/index.json
    FsCheck (2.8.2)
      FSharp.Core (>= 3.1.2.5)
    FSharp.Core (4.3.4)
"""

    let refFileContent = """
FSharp.Core
Argu

group Other1
  FSharp.Core
  FsCheck
"""

    let lockFile = LockFile.Parse("", toLines lockFile)

    let refFile = ReferencesFile.FromLines(toLines refFileContent)

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
        |> checkTargetFrameworkRestriction lockFile.Groups.[Constants.MainDependencyGroup].Options.Settings.ReferenceCondition lockFile.Groups.[Constants.MainDependencyGroup].Options.Settings.FrameworkRestrictions
        groupMain
        |> checkContainsPackageRefs [ "FSharp.Core","3.1.2.5"; "Argu","4.2.1" ] 
        otherGroup
        |> checkTargetFrameworkRestriction lockFile.Groups.[Domain.GroupName "Other1"].Options.Settings.ReferenceCondition lockFile.Groups.[Domain.GroupName "Other1"].Options.Settings.FrameworkRestrictions
        otherGroup
        |> checkContainsPackageRefs [ "FSharp.Core","4.3.4"; "FsCheck","2.8.2" ] 
    | l ->
        Assert.Fail(sprintf "expected two ItemGroup but was '%A'" l)

[<Test>]
let ``should create props file for design mode with group and package restriction``() = 

    let lockFile = """RESTRICTION: && (>= net461) (< net47)
NUGET
  remote: https://api.nuget.org/v3/index.json
    Argu (4.2.1)
      FSharp.Core (>= 3.1.2)
    FSharp.Core (3.1.2.5)

GROUP Other2
RESTRICTION: >= netcoreapp2.0
NUGET
  remote: https://api.nuget.org/v3/index.json
    FsCheck (2.8.2) - restriction: && (== netcoreapp2.1) (>= netcoreapp2.0)
      FSharp.Core (>= 3.1.2.5)
    FSharp.Core (4.3.4)
"""

    let refFileContent = """
FSharp.Core
Argu

group Other2
  FSharp.Core
  FsCheck
"""

    let lockFile = LockFile.Parse("", toLines lockFile)

    let refFile = ReferencesFile.FromLines(toLines refFileContent)

    let packages =
        [ for kv in refFile.Groups do
            let packagesInGroup,_ = lockFile.GetOrderedPackageHull(kv.Key, refFile)
            yield! packagesInGroup ]

    let outPath = System.IO.Path.GetTempFileName()
    Paket.RestoreProcess.createPaketPropsFile lockFile Seq.empty packages (FileInfo outPath)

    let doc = XDocument.Load(outPath, LoadOptions.PreserveWhitespace)

    let itemGroups = doc.Root.Elements (xname "ItemGroup") |> Seq.toList
            
    match itemGroups with
    | [groupMain; otherGroup20And21; otherGroupOnly21] ->
        groupMain
        |> checkTargetFrameworkRestriction lockFile.Groups.[Constants.MainDependencyGroup].Options.Settings.ReferenceCondition lockFile.Groups.[Constants.MainDependencyGroup].Options.Settings.FrameworkRestrictions
        groupMain
        |> checkContainsPackageRefs [ "FSharp.Core","3.1.2.5"; "Argu","4.2.1" ] 

        otherGroup20And21
        |> checkTargetFrameworkRestriction lockFile.Groups.[Domain.GroupName "Other2"].Options.Settings.ReferenceCondition lockFile.Groups.[Domain.GroupName "Other2"].Options.Settings.FrameworkRestrictions
        otherGroup20And21
        |> checkContainsPackageRefs [ "FSharp.Core","4.3.4" ] 

        otherGroupOnly21
        |> checkTargetFrameworkRestriction lockFile.Groups.[Domain.GroupName "Other2"].Options.Settings.ReferenceCondition lockFile.Groups.[Domain.GroupName "Other2"].Resolution.[Domain.PackageName "FsCheck"].Settings.FrameworkRestrictions
        otherGroupOnly21
        |> checkContainsPackageRefs [ "FsCheck","2.8.2" ] 
    | l ->
        Assert.Fail(sprintf "expected three ItemGroup but was '%A'" l)

[<Test>]
let ``should create props file for design mode package restriction``() = 

    let lockFile = """NUGET
  remote: https://api.nuget.org/v3/index.json
    Newtonsoft.Json (11.0.2) - restriction: == netcoreapp2.1
"""

    let refFileContent = """
Newtonsoft.Json
"""

    let lockFile = LockFile.Parse("", toLines lockFile)

    let refFile = ReferencesFile.FromLines(toLines refFileContent)

    let packages =
        [ for kv in refFile.Groups do
            let packagesInGroup,_ = lockFile.GetOrderedPackageHull(kv.Key, refFile)
            yield! packagesInGroup ]

    let outPath = System.IO.Path.GetTempFileName()
    Paket.RestoreProcess.createPaketPropsFile lockFile Seq.empty packages (FileInfo outPath)

    let doc = XDocument.Load(outPath, LoadOptions.PreserveWhitespace)

    let itemGroups = doc.Root.Elements (xname "ItemGroup") |> Seq.toList

    match itemGroups with
    | [groupMain] ->
        groupMain
        |> checkTargetFrameworkRestriction lockFile.Groups.[Constants.MainDependencyGroup].Options.Settings.ReferenceCondition lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[Domain.PackageName "Newtonsoft.Json"].Settings.FrameworkRestrictions
        groupMain
        |> checkContainsPackageRefs [ "Newtonsoft.Json","11.0.2" ] 
    | l ->
        Assert.Fail(sprintf "expected one ItemGroup but was '%A'" l)
