/// Contains methods for the deps.json process.
module Paket.DepsJsonFile

open System
open Paket
open System.IO
open Paket.Domain
open Paket.Requirements
open Paket.PackageResolver

let formatTransitiveDeps (lockFile:LockFile) (group,package) =
    let resolvedPackage = lockFile.Groups.[group].Resolution.[package]
    
    let formatText = sprintf "          \"%O\": \"%O\""

    resolvedPackage.Dependencies
    |> Seq.map (fun (name,version,_) ->
        let resolvedDep = lockFile.Groups.[group].Resolution.[name]
        formatText name (resolvedDep.Version.Normalize()))
    |> fun xs -> String.Join("," + Environment.NewLine,xs)

let formatRuntimeFiles (lockFile:LockFile) root (completeModel:Map<GroupName*PackageName,ResolvedPackage*InstallModel>) (group,package) (target:FrameworkIdentifier) =    
    let formatText = sprintf """          "%O": {}"""

    let resolvedPackage,model = completeModel.[group,package]
    let path = getTargetFolder root group package resolvedPackage.Version false + string Path.DirectorySeparatorChar // TODO: Version in path?
    model.GetLibReferences(target)
    |> Seq.choose (fun fileName ->
        let trimmedFileName = createRelativePath path fileName |> unixPath
        if trimmedFileName.StartsWith "ref/" then None else
        Some(formatText trimmedFileName))
    |> fun xs -> String.Join("," + Environment.NewLine,xs)
    
let formatDeps (lockFile:LockFile) root  model (usedPackages:Map<GroupName*PackageName,SemVerInfo*InstallSettings>) (target:FrameworkIdentifier) =
    let formatText = sprintf """      "%O/%O": {
%s
      }"""

    let formatRuntime = sprintf """        "runtime": {
%s
        }"""

    let formatDependencies = sprintf """        "dependencies": {
%s
        }"""

    usedPackages
    |> Seq.map (fun kv -> 
        let runtimes = 
            match formatRuntimeFiles lockFile root model (kv.Key) target with
            | "" -> ""
            | text -> formatRuntime text

        let dependencies = 
            match formatTransitiveDeps lockFile kv.Key with
            | "" -> ""
            | text -> formatDependencies text

        [dependencies; runtimes]
        |> List.filter ((<>) "")
        |> fun xs -> String.Join("," + Environment.NewLine,xs)
        |> formatText (snd kv.Key) ((fst kv.Value).Normalize()))
    |> fun xs -> String.Join("," + Environment.NewLine,xs)

let formatLibraries (lockFile:LockFile) root (completeModel:Map<GroupName*PackageName,ResolvedPackage*InstallModel>) (usedPackages:Map<GroupName*PackageName,SemVerInfo*InstallSettings>) (target:FrameworkIdentifier) =
    let formatText = sprintf """    "%O/%O": {
      "type": "package",
      "serviceable": true,
      "sha512": "sha512-%s"
    }"""


    usedPackages
    |> Seq.choose (fun kv ->
        let group,package = kv.Key
        let resolvedPackage,model = completeModel.[group,package]
        let hasRefs = 
            model.GetLibReferences(target)
            |> Seq.exists (fun fileName -> (unixPath fileName).Contains (sprintf "/%O/ref/" package))
        if hasRefs then None else

        let normalizedNupkgName = package.ToString() + "." + (fst kv.Value).Normalize() + ".nupkg"
        let path = getTargetFolder root group package resolvedPackage.Version false + string Path.DirectorySeparatorChar // TODO: Version in path?
        let targetFolder = DirectoryInfo(path).FullName
        let fi = FileInfo(Path.Combine(targetFolder, normalizedNupkgName))

        let hash = Utils.makeHash fi
        Some(formatText (snd kv.Key) ((fst kv.Value).Normalize()) hash))
    |> fun xs -> String.Join("," + Environment.NewLine,xs)

let createFile (lockFile:LockFile) (completeModel:Map<GroupName*PackageName,ResolvedPackage*InstallModel>) (usedPackages:Map<GroupName*PackageName,SemVerInfo*InstallSettings>) fileName target =
    let usedPackages =
        usedPackages
        |> Map.filter (fun k v -> isTargetMatchingRestrictions (getRestrictionList (snd v).FrameworkRestrictions, TargetProfile.SinglePlatform target))

    let root = FileInfo(lockFile.FileName).Directory.FullName
    let targetsText = formatDeps lockFile root completeModel usedPackages target
    let librariesText = formatLibraries lockFile root completeModel usedPackages target

    sprintf """{
  "runtimeTarget": {
    "name": ".NETCoreApp,Version=v1.0",
    "signature": "d348e834abde46e24eccadc6af398c3f36a7dd33"
  },
  "compilationOptions": {},
  "targets": {
    ".NETCoreApp,Version=v1.0": {
%s
    }
  },
  "libraries": {
%s
  }
}""" targetsText librariesText
    |> fun text ->
        let fi = FileInfo(fileName)
        if not fi.Directory.Exists then
            fi.Directory.Create()

        if fi.Exists then
            let oldText = File.ReadAllText(fileName)
            if text <> oldText then
                File.WriteAllText(fileName,text)
        else
            File.WriteAllText(fileName,text)