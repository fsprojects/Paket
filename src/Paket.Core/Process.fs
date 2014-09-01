module Paket.Process

open System.IO

let ExtractPackages force (packages : Package seq) = 
    packages |> Seq.map (fun package -> 
                    let version = 
                        match package.VersionRange with
                        | Specific v -> v
                        | v -> failwithf "Version error in lockfile for %s %A" package.Name v
                    match package.SourceType with
                    | "nuget" -> 
                        async {
                            let! packageFile = Nuget.DownloadPackage(package.Source, package.Name, version.ToString(), force)
                            return! Nuget.ExtractPackage(packageFile,package.Name, version.ToString(), force) }
                    | _ -> failwithf "Can't download from source type %s" package.SourceType)

let Install regenerate force packageFile =
    let lockfile =
        let fi = FileInfo(packageFile)
        FileInfo(fi.Directory.FullName + Path.DirectorySeparatorChar.ToString() + fi.Name.Replace(fi.Extension,".lock"))

    if regenerate || (not lockfile.Exists) then
        LockFile.Update packageFile lockfile.FullName

    File.ReadAllLines lockfile.FullName
    |> LockFile.Parse
    |> ExtractPackages force
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
