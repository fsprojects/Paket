module Paket.Process

open System.IO

let DownloadPackages(lockFile : Package seq) = 
    lockFile |> Seq.map (fun package -> 
                    let version = 
                        match package.VersionRange with
                        | Exactly v -> v
                        | v -> failwithf "Version error in lockfile for %s %A" package.Name v
                    match package.SourceType with
                    | "nuget" -> 
                        let targetFile =
                            Path.Combine(
                                Nuget.CacheFolder,
                                package.Name + "." + version + ".nupkg")

                        Nuget.DownloadPackage(package.Source, package.Name, version, targetFile)
                    | _ -> failwithf "Can't download from source type %s" package.SourceType)

let Install regenerate packageFile =
    let lockfile =
        let fi = FileInfo(packageFile)
        FileInfo(fi.Directory.FullName + Path.DirectorySeparatorChar.ToString() + fi.Name.Replace(fi.Extension,".lock"))

    if regenerate || (not lockfile.Exists) then
        LockFile.Update packageFile lockfile.FullName

    File.ReadAllLines lockfile.FullName
    |> LockFile.Parse
    |> DownloadPackages
    |> Async.Parallel
    |> Async.RunSynchronously
