module Paket.ProjectFile.ReadPropertySpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should process conditions`` () =
    let testWithCondition = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PropertyWithCondition Condition="'string2' == 'string2'">Correct value</PropertyWithCondition>
    <PropertyWithCondition Condition="'string1' == 'string2'">Incorrect value</PropertyWithCondition>
  </PropertyGroup>
</Project>
"""
    testWithCondition
    |> ProjectFile.loadFromString "test"
    |> ProjectFile.getProperty "PropertyWithCondition"
    |> shouldEqual (Some "Correct value")


[<Test>]
let ``should process placeholders`` () =
    let testWithPlaceholder = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Property1>Placeholder value</Property1>
    <PropertyWithPlaceholder>Value $(Property1)</PropertyWithPlaceholder>
  </PropertyGroup>
</Project>
"""
    testWithPlaceholder
    |> ProjectFile.loadFromString "test"
    |> ProjectFile.getProperty "PropertyWithPlaceholder"
    |> shouldEqual (Some "Value Placeholder value")

[<Test>]
let ``should process espaced characters`` () =
    let testWithEscapedCahrs = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PropertyWithEscapedCaharacter>Value %28in parens%29</PropertyWithEscapedCaharacter>
  </PropertyGroup>
</Project>
"""
    testWithEscapedCahrs
    |> ProjectFile.loadFromString "test"
    |> ProjectFile.getProperty "PropertyWithEscapedCaharacter"
    |> shouldEqual (Some "Value (in parens)")

