module Paket.NuGetLocal

open System
open System.IO
open System.IO.Compression
open Pri.LongPath
open System.Text.RegularExpressions
open Paket.Domain
open Paket.NuGetCache

/// Gets versions of the given package from local NuGet feed.
let getAllVersionsFromLocalPath (isCache, localNugetPath, package:PackageName, alternativeProjectRoot, root) =
    let localNugetPath = Utils.normalizeLocalPath localNugetPath
    let di = getDirectoryInfoForLocalNuGetFeed localNugetPath alternativeProjectRoot root
    NuGetCache.NuGetRequestGetVersions.ofSimpleFunc di.FullName (fun _ ->
        async {
            if not di.Exists then
                if isCache then
                    di.Create()
                else
                    failwithf "The directory %s doesn't exist.%sPlease check the NuGet source feed definition in your paket.dependencies file." di.FullName Environment.NewLine

            let versions =
                Directory.EnumerateFiles(di.FullName,"*.nupkg",SearchOption.AllDirectories)
                |> Seq.filter (fun fi -> fi.EndsWith ".symbols.nupkg" |> not)
                |> Seq.choose (fun fileName ->
                                let fi = FileInfo(fileName)
                                let _match = Regex(sprintf @"^%O\.(\d.*)\.nupkg" package, RegexOptions.IgnoreCase).Match(fi.Name)
                                if _match.Groups.Count > 1 then Some _match.Groups.[1].Value else None)
                |> Seq.toArray
            return SuccessResponse(versions)
        })

/// Reads packageName and version from .nupkg file name
let parsePackageInfoFromFileName fileName : (PackageName * SemVerInfo) option =
    let regex = Regex ("^(?<name>.*?)\.(?<version>\d.*)\.nupkg$", RegexOptions.IgnoreCase)
    match regex.Match fileName with
    | matchResult when matchResult.Success && matchResult.Groups.Count = 3 -> 
        try
            let semVer = SemVer.Parse matchResult.Groups.["version"].Value
            let packageName = PackageName matchResult.Groups.["name"].Value 
            Some (packageName, semVer)
        with
        | _ -> None        
    | _ -> None

let findLocalPackage directory (packageName:PackageName) (version:SemVerInfo) =
    if not <| Directory.Exists directory then
        failwithf "The package %O %O can't be found in %s. (The directory doesn't exist.)%sPlease check the feed definition in your paket.dependencies file." packageName version directory Environment.NewLine
    let v1 = FileInfo(Path.Combine(directory, sprintf "%O.%O.nupkg" packageName version))
    if v1.Exists then v1 else
    let normalizedVersion = version.Normalize()
    let v2 = FileInfo(Path.Combine(directory, sprintf "%O.%s.nupkg" packageName normalizedVersion))
    if v2.Exists then v2 else

    let condition x = 
        match parsePackageInfoFromFileName x with
        | Some (name, ver) -> packageName = name && version = ver
        | None -> false 
        
    let v3 =
        Directory.EnumerateFiles(directory,"*.nupkg",SearchOption.AllDirectories)
        |> Seq.tryFind (Path.GetFileName >> condition)

    match v3 with
    | None -> failwithf "The package %O %O can't be found in %s.%sPlease check the feed definition in your paket.dependencies file." packageName version directory Environment.NewLine
    | Some x -> FileInfo x

/// Reads nuspec from nupkg
let getNuSpecFromNupgk fileName =
    use __ = Profile.startCategory Profile.Category.FileIO
    fixArchive fileName
    use zipToCreate = new FileStream(fileName, FileMode.Open, FileAccess.Read)
    use zip = new ZipArchive(zipToCreate, ZipArchiveMode.Read)
    let zippedNuspec = zip.Entries |> Seq.find (fun f -> f.FullName.EndsWith ".nuspec")
    use stream = zippedNuspec.Open()
    Nuspec.Load(Path.Combine(fileName, Path.GetFileName zippedNuspec.FullName), stream)

/// Reads package name from a nupkg file
let getPackageNameFromLocalFile fileName =
    let nuspec = getNuSpecFromNupgk fileName
    nuspec.OfficialName

/// Reads direct dependencies from a nupkg file
let getDetailsFromLocalNuGetPackage isCache alternativeProjectRoot root localNuGetPath (packageName:PackageName) (version:SemVerInfo) =
    async {
        let localNugetPath = Utils.normalizeLocalPath localNuGetPath
        let di = getDirectoryInfoForLocalNuGetFeed localNugetPath alternativeProjectRoot root
        let nupkg = findLocalPackage di.FullName packageName version

        let nuspec = getNuSpecFromNupgk nupkg.FullName

        return
            { PackageName = nuspec.OfficialName
              DownloadUrl = packageName.ToString()
              SerializedDependencies = []
              SourceUrl = di.FullName
              CacheVersion = NuGetPackageCache.CurrentCacheVersion
              LicenseUrl = nuspec.LicenseUrl
              Version = version.Normalize()
              Unlisted = isCache }
            |> NuGetPackageCache.withDependencies nuspec.Dependencies
            |> ODataSearchResult.Match
    }