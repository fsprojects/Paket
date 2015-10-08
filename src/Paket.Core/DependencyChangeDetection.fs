module Paket.DependencyChangeDetection

open Paket.Domain
open Paket.Requirements
open Paket.PackageResolver

let findNuGetChangesInDependenciesFile(dependenciesFile:DependenciesFile,lockFile:LockFile) =
    let inline hasChanged (newRequirement:PackageRequirement) (originalPackage:ResolvedPackage) =
        newRequirement.VersionRequirement.IsInRange originalPackage.Version |> not ||
            newRequirement.Settings <> originalPackage.Settings

    let added groupName =
        match dependenciesFile.Groups |> Map.tryFind groupName with
        | None -> Set.empty
        | Some group ->
            let lockFileGroup = lockFile.Groups |> Map.tryFind groupName 
            group.Packages
            |> Seq.map (fun d -> d.Name,d)
            |> Seq.filter (fun (name,pr) ->
                match lockFileGroup with
                | None -> true
                | Some group ->
                    match group.Resolution.TryFind name with
                    | Some p -> hasChanged pr p
                    | _ -> true)
            |> Seq.map (fun (p,_) -> groupName,p)
            |> Set.ofSeq
    
    let modified groupName = 
        let directMap =
            match dependenciesFile.Groups |> Map.tryFind groupName with
            | None -> Map.empty
            | Some group ->
                group.Packages
                |> Seq.map (fun d -> d.Name,d)
                |> Map.ofSeq

        [for t in lockFile.GetTopLevelDependencies(groupName) do
            let name = t.Key
            match directMap.TryFind name with
            | Some pr -> if hasChanged pr t.Value then yield groupName, name // Modified
            | _ -> yield groupName, name // Removed
        ]
        |> List.map lockFile.GetAllNormalizedDependenciesOf
        |> Seq.concat
        |> Set.ofSeq

    let groupNames =
        dependenciesFile.Groups
        |> Seq.map (fun kv -> kv.Key)
        |> Seq.append (lockFile.Groups |> Seq.map (fun kv -> kv.Key))

    groupNames
    |> Seq.map (fun groupName -> 
            let added = added groupName 
            let modified = modified groupName
            Set.union added modified)
    |> Seq.concat
    |> Set.ofSeq

[<CustomEquality;CustomComparison>]
type RemoteFileChange =
    { Owner : string
      Project : string
      Name : string
      Origin : ModuleResolver.SingleSourceFileOrigin
      Commit : string option
      AuthKey : string option }

    override this.Equals(that) = 
        match that with
        | :? RemoteFileChange as that -> 
            this.Owner = that.Owner &&
             this.Project = that.Project &&
             this.Name = that.Name &&
             this.Origin = that.Origin &&
             ((this.Commit = that.Commit) || this.Commit = None || that.Commit = None) &&
             this.AuthKey = that.AuthKey
        | _ -> false

    override this.ToString() = sprintf "%O/%s/%s" this.Origin this.Project this.Name

    override this.GetHashCode() = hash (this.Owner,this.Name,this.AuthKey,this.Project,this.Origin)

    static member Compare(x,y) =
        if x = y then 0 else
        let c1 = compare x.Owner y.Owner
        if c1 <> 0 then c1 else
        let c2 = compare x.Project y.Project
        if c2 <> 0 then c2 else
        let c3 = compare x.Name y.Name
        if c3 <> 0 then c3 else
        let c4 = compare x.Origin y.Origin
        if c4 <> 0 then c4 else
        let c5 = compare x.AuthKey y.AuthKey
        if c5 <> 0 then c5 else
        compare x.Commit y.Commit

    interface System.IComparable with
       member this.CompareTo that = 
          match that with 
          | :? RemoteFileChange as that -> RemoteFileChange.Compare(this,that)
          | _ -> invalidArg "that" "cannot compare value of different types"


let findRemoteFileChangesInDependenciesFile(dependenciesFile:DependenciesFile,lockFile:LockFile) =
    let groupNames =
        dependenciesFile.Groups
        |> Seq.map (fun kv -> kv.Key)
        |> Seq.append (lockFile.Groups |> Seq.map (fun kv -> kv.Key))

    let createUnresolvedVersion (resolvedFile:ModuleResolver.UnresolvedSourceFile) : RemoteFileChange =
          { Owner = resolvedFile.Owner
            Project = resolvedFile.Project
            Name = resolvedFile.Name
            Origin = resolvedFile.Origin
            Commit = resolvedFile.Commit
            AuthKey = resolvedFile.AuthKey }

    let createResolvedVersion (resolvedFile:ModuleResolver.ResolvedSourceFile) : RemoteFileChange =
          { Owner = resolvedFile.Owner
            Project = resolvedFile.Project
            Name = resolvedFile.Name
            Origin = resolvedFile.Origin
            Commit = Some resolvedFile.Commit
            AuthKey = resolvedFile.AuthKey }

    groupNames
    |> Seq.map (fun groupName ->
            match dependenciesFile.Groups |> Map.tryFind groupName with
            | Some dependenciesFileGroup ->
                match lockFile.Groups |> Map.tryFind groupName with
                | Some lockFilegroup ->
                    let lockFileRemoteFiles =
                        lockFilegroup.RemoteFiles
                        |> List.map createResolvedVersion
                        |> Set.ofList

                    let dependenciesFileRemoteFiles =
                        dependenciesFileGroup.RemoteFiles
                        |> List.map createUnresolvedVersion
                        |> Set.ofList

                    let u =
                        dependenciesFileRemoteFiles
                        |> Set.union lockFileRemoteFiles
                    let i =
                        dependenciesFileRemoteFiles
                        |> Set.intersect lockFileRemoteFiles

                    Set.difference u i
                | None -> 
                    // all added
                    dependenciesFileGroup.RemoteFiles 
                    |> List.map createUnresolvedVersion 
                    |> Set.ofList 
            | None -> 
                // all removed
                lockFile.GetGroup(groupName).RemoteFiles
                |> List.map createResolvedVersion
                |> Set.ofList
            |> Set.map (fun x -> groupName,x))
    |> Seq.concat
    |> Set.ofSeq

let GetPreferredNuGetVersions (oldLockFile:LockFile) (changedDependencies:Set<GroupName*PackageName>) =
    oldLockFile.GetGroupedResolution()
    |> Seq.filter (fun kv -> not <| changedDependencies.Contains(kv.Key))
    |> Seq.map (fun kv -> kv.Key, kv.Value.Version)
    |> Map.ofSeq