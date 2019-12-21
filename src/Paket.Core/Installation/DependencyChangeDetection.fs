module Paket.DependencyChangeDetection

open Paket.Requirements
open Paket.PackageResolver

type DependencyChangeType =
    /// The restrictions changed
    | RestrictionsChanged
    /// The settings of the package changed
    | SettingsChanged
    /// The Version in the LockFile doesn't match the spec in the dependencies file.
    | VersionNotValid
    /// Package from dependencies file was not found in lockfile
    | PackageNotFoundInLockFile
    /// Group from dependencies file was not found in lockfile
    | GroupNotFoundInLockFile
    /// Package from lock file was not found in dependencies file
    | PackageNotFoundInDependenciesFile

let findNuGetChangesInDependenciesFile(dependenciesFile:DependenciesFile,lockFile:LockFile,strict) =
    let allTransitives groupName = lockFile.GetTransitiveDependencies groupName
    let getChanges groupName transitives (newRequirement:PackageRequirement) (originalPackage:PackageInfo) =
        let settingsChanged() =
            if newRequirement.Settings <> originalPackage.Settings then
                if newRequirement.Settings = { originalPackage.Settings with FrameworkRestrictions = AutoDetectFramework } then
                    []
                else
                    let isTransitive = transitives |> Seq.contains originalPackage.Name
                    if isTransitive then
                        []
                    else
                        if newRequirement.Settings.FrameworkRestrictions <> originalPackage.Settings.FrameworkRestrictions then
                            [RestrictionsChanged]
                        else
                            [SettingsChanged]

            else []

        let requirementOk =
            let isInRange =
                if strict then
                    newRequirement.VersionRequirement.IsInRange originalPackage.Version
                else
                    newRequirement.IncludingPrereleases().VersionRequirement.IsInRange originalPackage.Version
            if not isInRange then
                [VersionNotValid]
            else []

        requirementOk @ settingsChanged()

    let added groupName transitives =
        match dependenciesFile.Groups |> Map.tryFind groupName with
        | None -> Set.empty
        | Some depsGroup ->
            let lockFileGroup = lockFile.Groups |> Map.tryFind groupName
            depsGroup.Packages
            |> Seq.map (fun d ->
                let name = d.Name
                name,
                match lockFileGroup with
                | None -> [GroupNotFoundInLockFile]
                | Some group ->
                    match group.TryFind name with
                    | Some lockFilePackage ->
                        let dependenciesFilePackage = { d with Settings = d.Settings + depsGroup.Options.Settings }
                        getChanges groupName transitives
                            { dependenciesFilePackage with Settings = dependenciesFilePackage.Settings + depsGroup.Options.Settings }
                            lockFilePackage
                    | _ -> [PackageNotFoundInLockFile])
            |> Seq.filter (fun (_, changes) -> changes.Length > 0)
            |> Seq.map (fun (p, changes) -> groupName, p, changes)
            |> Set.ofSeq

    let modified groupName transitives =
        let directMap =
            match dependenciesFile.Groups |> Map.tryFind groupName with
            | None -> Map.empty
            | Some group ->
                group.Packages
                |> Seq.map (fun d -> d.Name,{ d with Settings = d.Settings + group.Options.Settings })
                |> Map.ofSeq

        [for t in lockFile.GetTopLevelDependencies(groupName) do
            let name = t.Key
            match directMap.TryFind name with
            | Some pr ->
                let t = t.Value
                yield groupName, name, getChanges groupName transitives pr t // Modified
            | _ -> yield groupName, name, [PackageNotFoundInDependenciesFile] // Removed
        ]
        |> List.filter (fun (_,_, changes) -> changes.Length > 0)
        |> List.map (fun (g,p, changes) ->
            lockFile.GetAllNormalizedDependenciesOf(g,p,lockFile.FileName)
            |> Seq.map (fun (a,b) -> a,b,changes))
        |> Seq.concat
        |> Set.ofSeq

    let groupNames =
        dependenciesFile.Groups
        |> Seq.map (fun kv -> kv.Key)
        |> Seq.append (lockFile.Groups |> Seq.map (fun kv -> kv.Key))

    groupNames
    |> Seq.collect (fun groupName ->
        let transitives = allTransitives groupName
        let added = added groupName transitives
        let modified = modified groupName transitives
        Set.union added modified)
    |> Set.ofSeq

[<CustomEquality;CustomComparison>]
type RemoteFileChange =
    { Owner : string
      Project : string
      Name : string
      Origin : ModuleResolver.Origin
      Commit : string option
      AuthKey : string option }

    override this.Equals(that) =
        match that with
        | :? RemoteFileChange as that ->
            this.FieldsWithoutCommit = that.FieldsWithoutCommit &&
             ((this.Commit = that.Commit) || this.Commit = None || that.Commit = None)
        | _ -> false

    override this.ToString() = sprintf "%O/%s/%s" this.Origin this.Project this.Name

    member private this.FieldsWithoutCommit = this.Owner,this.Name,this.AuthKey,this.Project,this.Origin
    member private this.FieldsWithCommit = this.FieldsWithoutCommit,this.Commit
    override this.GetHashCode() = hash this.FieldsWithCommit

    static member Compare(x:RemoteFileChange,y:RemoteFileChange) =
        if x = y then 0 else
        compare x.FieldsWithCommit y.FieldsWithCommit

    interface System.IComparable with
       member this.CompareTo that =
          match that with
          | :? RemoteFileChange as that -> RemoteFileChange.Compare(this,that)
          | _ -> invalidArg "that" "cannot compare value of different types"

    static member CreateUnresolvedVersion (unresolved:ModuleResolver.UnresolvedSource) : RemoteFileChange =
        { Owner = unresolved.Owner
          Project = unresolved.Project
          Name = unresolved.Name.TrimStart('/')
          Origin = unresolved.Origin
          Commit =
            match unresolved.Version with
            | ModuleResolver.VersionRestriction.NoVersionRestriction -> None
            | ModuleResolver.VersionRestriction.Concrete x -> Some x
            | ModuleResolver.VersionRestriction.VersionRequirement vr -> Some(vr.ToString())

          AuthKey = unresolved.AuthKey }

    static member CreateResolvedVersion (resolved:ModuleResolver.ResolvedSourceFile) : RemoteFileChange =
        { Owner = resolved.Owner
          Project = resolved.Project
          Name = resolved.Name
          Origin = resolved.Origin
          Commit = Some resolved.Commit
          AuthKey = resolved.AuthKey }


let findRemoteFileChangesInDependenciesFile(dependenciesFile:DependenciesFile,lockFile:LockFile) =
    let groupNames =
        dependenciesFile.Groups
        |> Seq.map (fun kv -> kv.Key)
        |> Seq.append (lockFile.Groups |> Seq.map (fun kv -> kv.Key))

    let computeDifference (lockFileGroup:LockFileGroup) (dependenciesFileGroup:DependenciesGroup) =
        let dependenciesFileRemoteFiles =
            dependenciesFileGroup.RemoteFiles
            |> List.map RemoteFileChange.CreateUnresolvedVersion
            |> Set.ofList

        let lockFileRemoteFiles =
            lockFileGroup.RemoteFiles
            |> List.map (fun files ->
                let r = RemoteFileChange.CreateResolvedVersion files
                match dependenciesFileRemoteFiles |> Seq.tryFind (fun d -> d.Name = r.Name && d.Origin = r.Origin) with
                | Some d -> { r with Commit = d.Commit }
                | _ -> { r with Commit = None })
            |> Set.ofList

        let missingRemotes = Set.difference dependenciesFileRemoteFiles lockFileRemoteFiles
        missingRemotes

    groupNames
    |> Seq.collect (fun groupName ->
            match dependenciesFile.Groups |> Map.tryFind groupName with
            | Some dependenciesFileGroup ->
                match lockFile.Groups |> Map.tryFind groupName with
                | Some lockFileGroup -> computeDifference lockFileGroup dependenciesFileGroup
                | None ->
                    // all added
                    dependenciesFileGroup.RemoteFiles
                    |> List.map RemoteFileChange.CreateUnresolvedVersion
                    |> Set.ofList
            | None ->
                // all removed
                lockFile.GetGroup(groupName).RemoteFiles
                |> List.map RemoteFileChange.CreateResolvedVersion
                |> Set.ofList
            |> Set.map (fun x -> groupName,x))
    |> Set.ofSeq

let GetPreferredNuGetVersions (dependenciesFile:DependenciesFile,lockFile:LockFile) =
    lockFile.GetGroupedResolution()
    |> Seq.map (fun kv ->
        let lockFileSource = kv.Value.Source
        match dependenciesFile.Groups |> Map.tryFind (fst kv.Key) with
        | None -> kv.Key, (kv.Value.Version, lockFileSource)
        | Some group ->
            match group.Sources |> List.tryFind (fun s -> s.Url = lockFileSource.Url) with
            | Some s -> kv.Key, (kv.Value.Version, s)
            | None -> kv.Key, (kv.Value.Version, kv.Value.Source))
    |> Map.ofSeq

let GetChanges(dependenciesFile,lockFile,strict) =
    let nuGetChanges = findNuGetChangesInDependenciesFile(dependenciesFile,lockFile,strict)
    let nuGetChangesPerGroup =
        nuGetChanges
        |> Seq.groupBy (fun (f,_,__) -> f)
        |> Map.ofSeq

    let remoteFileChanges = findRemoteFileChangesInDependenciesFile(dependenciesFile,lockFile)
    let remoteFileChangesPerGroup =
        remoteFileChanges
        |> Seq.groupBy fst
        |> Map.ofSeq

    let hasNuGetChanges groupName =
        match nuGetChangesPerGroup |> Map.tryFind groupName with
        | None -> false
        | Some x -> Seq.isEmpty x |> not

    let hasRemoteFileChanges groupName =
        match remoteFileChangesPerGroup |> Map.tryFind groupName with
        | None -> false
        | Some x -> Seq.isEmpty x |> not

    let hasChangedSettings groupName =
        match dependenciesFile.Groups |> Map.tryFind groupName with
        | None -> true
        | Some dependenciesFileGroup ->
            match lockFile.Groups |> Map.tryFind groupName with
            | None -> true
            | Some lockFileGroup ->
                let lockFileGroupOptions =
                    if dependenciesFileGroup.Options.Settings.FrameworkRestrictions = AutoDetectFramework then
                        { lockFileGroup.Options with Settings = { lockFileGroup.Options.Settings with FrameworkRestrictions = AutoDetectFramework } }
                    else
                        lockFileGroup.Options
                dependenciesFileGroup.Options <> lockFileGroupOptions

    let hasChanges groupName _ =
        hasChangedSettings groupName || hasNuGetChanges groupName || hasRemoteFileChanges groupName

    let hasAnyChanges =
        dependenciesFile.Groups
        |> Map.filter hasChanges
        |> Map.isEmpty
        |> not

    hasAnyChanges,nuGetChanges,remoteFileChanges,hasChanges