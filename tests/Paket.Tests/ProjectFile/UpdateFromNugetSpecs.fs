﻿namespace Paket.Tests.ProjectFile

open NUnit.Framework
open Paket

[<TestFixture(Category=Category.ProjectFile)>]
module UpdateFromNugetSpecs =

    open Paket
    open NUnit.Framework
    open FsUnit
    open System.Xml
    open System.IO

    let convertAndCompare source expectedResult =
        let projectFile = ProjectFile.LoadFromString("Test.csproj", source)
        ProjectFile.removeNuGetPackageImportStamp projectFile
        let actualResult = Utils.normalizeXml projectFile.Document
        let normalizedExpected =
            let doc = XmlDocument()
            doc.LoadXml(expectedResult)
            doc |> Utils.normalizeXml
        actualResult |> shouldEqual normalizedExpected

    [<Test>]
    let ``should remove NuGetPackageImportStamp and empty PropertyGroup``() =
        let projectFile = """<?xml version="1.0" encoding="utf-8"?>
    <Project ToolsVersion="14.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
      <PropertyGroup>
        <NuGetPackageImportStamp>
        </NuGetPackageImportStamp>
      </PropertyGroup>
      <PropertyGroup>
        <WarningLevel>4</WarningLevel>
      </PropertyGroup>
    </Project>"""

        let expectedResult = """<?xml version="1.0" encoding="utf-8"?>
    <Project ToolsVersion="14.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
      <PropertyGroup>
        <WarningLevel>4</WarningLevel>
      </PropertyGroup>
    </Project>"""
    
        convertAndCompare projectFile expectedResult

    [<Test>]
    let ``should remove NuGetPackageImportStamp but not PropertyGroup with items``() =
        let projectFile = """<?xml version="1.0" encoding="utf-8"?>
    <Project ToolsVersion="14.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
      <PropertyGroup>
        <WarningLevel>4</WarningLevel>
        <NuGetPackageImportStamp>
        </NuGetPackageImportStamp>
      </PropertyGroup>
    </Project>"""

        let expectedResult = """<?xml version="1.0" encoding="utf-8"?>
    <Project ToolsVersion="14.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
      <PropertyGroup>
        <WarningLevel>4</WarningLevel>
      </PropertyGroup>
    </Project>"""
    
        convertAndCompare projectFile expectedResult

    let testDataRootPath = Path.Combine(__SOURCE_DIRECTORY__, "TestData")
    let TestData: obj[][] = [|
        for f in Directory.GetFiles testDataRootPath do
            let allText = File.ReadAllText f
            if not (allText.Contains "NuGetPackageImportStamp") then
                yield [| Path.GetFileName f |]
    |]


    [<Test>]
    [<TestCaseSource("TestData")>]
    let ``should not modify projects without NuGetPackageImportStamp`` projectFile =
        let text = File.ReadAllText (Path.Combine(testDataRootPath, projectFile))
        convertAndCompare text text