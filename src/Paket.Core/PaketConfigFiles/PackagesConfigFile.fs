module Paket.PackagesConfigFile

open Paket
open System
open System.IO
open System.Xml
open Paket.Xml
open Paket.PackageSources

let Read fileName = 
    let file = FileInfo fileName
    if file.Exists |> not then [] else
    let doc = XmlDocument()
    ( use f = File.OpenRead(file.FullName)
      doc.Load f)
    
    [for node in doc.SelectNodes("//package") ->
        let v = node.Attributes.["version"].Value
        { NugetPackage.Id = node.Attributes.["id"].Value
          VersionRequirement = VersionRequirement.VersionRequirement (VersionRange.Specific (SemVer.Parse v), PreReleaseStatus.All)
          Kind = NugetPackageKind.Package
          TargetFramework = 
            node 
            |> getAttribute "targetFramework" 
            |> Option.map (fun t -> ">= " + t) } ]

let Serialize (packages: NugetPackage seq) =
    if Seq.isEmpty packages then "" else
    let packages = 
        packages 
        |> Seq.choose (fun p -> 
            match p.VersionRequirement.Range with
            | VersionRange.Specific v
            | VersionRange.Minimum v ->
                let framework = 
                    match p.TargetFramework with
                    | Some tf -> sprintf "targetFramework=\"%s\" " (tf.Replace(">= ",""))
                    | _ -> ""

                Some (sprintf """  <package id="%s" version="%O" %s/>""" p.Id v framework)
            | _ -> None)

    sprintf """<?xml version="1.0" encoding="utf-8"?>
<packages>
%s
</packages>""" (String.Join(Environment.NewLine,packages))

let Save fileName packages =
    let original = 
        if File.Exists fileName then
            File.ReadAllText fileName |> normalizeLineEndings
        else ""

    let newFile = Serialize packages |> normalizeLineEndings

    if newFile = "" then
        if File.Exists fileName then File.Delete fileName
    else
        if newFile <> original then
            File.WriteAllText(fileName,newFile)
