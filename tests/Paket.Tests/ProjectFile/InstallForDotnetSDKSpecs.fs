module Paket.ProjectFile.InstallForDotnetSDKSpecs

open Paket
open NUnit.Framework
open FsUnit
open System
open System.IO
open TestHelpers

/// Returns the ordered list of child element names directly under the <Project> node.
let private childNodeNames (project: ProjectFile) =
    [ for node in project.ProjectNode.ChildNodes do
        if node.NodeType = System.Xml.XmlNodeType.Element then
            yield node.Name ]

[<Test>]
let ``installForDotnetSDK should not move an existing Paket.Restore.targets import``() =
    ensureDir()

    // Skip actually extracting the embedded Paket.Restore.targets resource to disk - we only need
    // the deterministic path that installForDotnetSDK computes and compares against.
    Environment.SetEnvironmentVariable("PAKET_SKIP_RESTORE_TARGETS", "true")
    try
        let root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))
        let projectFileName = Path.Combine(root, "Project.fsproj")
        let targetsPath = Path.Combine(root, ".paket", "Paket.Restore.targets")
        let relativePath = Utils.createRelativePath projectFileName targetsPath

        let projectContent =
            sprintf """<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="%s" />
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>
</Project>""" relativePath

        let project = ProjectFile.LoadFromString(projectFileName, projectContent)

        // The import is already the first child node - installForDotnetSDK must leave it there
        // rather than removing and re-appending it at the end (see #3209).
        childNodeNames project |> List.head |> shouldEqual "Import"

        InstallProcess.installForDotnetSDK root project

        let names = childNodeNames project
        names |> List.head |> shouldEqual "Import"

        // Only a single Import for the targets file should remain - it must not be duplicated.
        project.Document
        |> Xml.getDescendants "Import"
        |> List.filter (Xml.withAttributeValue "Project" relativePath)
        |> List.length
        |> shouldEqual 1
    finally
        Environment.SetEnvironmentVariable("PAKET_SKIP_RESTORE_TARGETS", null)

[<Test>]
let ``installForDotnetSDK should add the Paket.Restore.targets import when missing``() =
    ensureDir()

    Environment.SetEnvironmentVariable("PAKET_SKIP_RESTORE_TARGETS", "true")
    try
        let root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))
        let projectFileName = Path.Combine(root, "Project.fsproj")
        let targetsPath = Path.Combine(root, ".paket", "Paket.Restore.targets")
        let relativePath = Utils.createRelativePath projectFileName targetsPath

        let projectContent = """<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>
</Project>"""

        let project = ProjectFile.LoadFromString(projectFileName, projectContent)

        InstallProcess.installForDotnetSDK root project

        project.Document
        |> Xml.getDescendants "Import"
        |> List.filter (Xml.withAttributeValue "Project" relativePath)
        |> List.length
        |> shouldEqual 1
    finally
        Environment.SetEnvironmentVariable("PAKET_SKIP_RESTORE_TARGETS", null)
