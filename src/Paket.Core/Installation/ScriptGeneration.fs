module Paket.LoadingScripts

open System
open System.IO
open Paket
open Paket.Domain
open Mono.Cecil




module PackageAndAssemblyResolution =

    let getLeafPackagesGeneric getPackageName getDependencies (knownPackages:Set<_>) openList =
        
        let leafPackages =
            openList |> List.filter (fun p ->
                not (knownPackages.Contains(getPackageName p)) &&
                getDependencies p |> Seq.forall (knownPackages.Contains)
            )

        let newKnownPackages =
            (knownPackages,leafPackages)
            ||> Seq.fold (fun state package -> state |> Set.add (getPackageName package)) 

        let newState =
            openList |> List.filter (fun p -> 
                leafPackages |> Seq.forall (fun l -> getPackageName l <> getPackageName p)
            )
        leafPackages, newKnownPackages, newState


    let getPackageOrderGeneric getPackageName getDependencies packages =
        
        let rec step finalList knownPackages currentPackages =
            match currentPackages |> getLeafPackagesGeneric getPackageName getDependencies knownPackages with
            | ([], _, _) -> finalList
            | (leafPackages, newKnownPackages, newState) ->
                step (leafPackages @ finalList) newKnownPackages newState
        
        step [] Set.empty packages
        |> List.rev  


    let getPackageOrderResolvedPackage =
        getPackageOrderGeneric 
            (fun (p:PackageResolver.ResolvedPackage) -> p.Name) 
            (fun p -> p.Dependencies |> Seq.map (fun (n,_,_) -> n))


    let getPackageOrderFromLockFile (lockFile:LockFile) =
        
        let lockFile = LockFileParser.Parse (System.IO.File.ReadAllLines lockFile.FileName)
        lockFile
        |> Seq.map (fun p -> p.GroupName, getPackageOrderResolvedPackage p.Packages)
        |> Map.ofSeq


    let getDllOrder (dllFiles : AssemblyDefinition list) =
        // this check saves looking at assembly metadata when we know this is not needed
        if List.length dllFiles = 1 then dllFiles  else
        // we ignore all unknown references as they are most likely resolved on package level
        let known = dllFiles |> Seq.map (fun a -> a.FullName) |> Set.ofSeq
        getPackageOrderGeneric
            (fun (p:AssemblyDefinition) -> p.FullName)
            (fun p -> p.MainModule.AssemblyReferences |> Seq.map (fun r -> r.FullName) |> Seq.filter (known.Contains))
            dllFiles


    let getDllsWithinPackage (framework: FrameworkIdentifier) (installModel :InstallModel) =
        let dllFiles =
            installModel
            |> InstallModel.getLegacyReferences (SinglePlatform framework)
            |> Seq.map (fun l -> l.Path)
            |> Seq.map (fun path -> AssemblyDefinition.ReadAssembly path, FileInfo(path))
            |> dict

        getDllOrder (dllFiles.Keys |> Seq.toList)
        |> List.map (fun a -> dllFiles.[a])


    let shouldExcludeFrameworkAssemblies =
        // NOTE: apparently for .netcore / .netstandard we should skip framework dependencies
        // https://github.com/fsprojects/Paket/issues/2156
        function
        | FrameworkIdentifier.DotNetCore _ 
        | FrameworkIdentifier.DotNetStandard _ -> true
        | _ -> false


    let getFrameworkReferencesWithinPackage (framework: FrameworkIdentifier) (installModel :InstallModel) =
        if shouldExcludeFrameworkAssemblies framework then List.empty else
        installModel
        |> InstallModel.getAllLegacyFrameworkReferences
        |> Seq.toList


module ScriptGeneration =

    type PathCombine = PathCombine with
        static member (?<-) (_,dir:DirectoryInfo,framework:FrameworkIdentifier) =  Path.Combine (dir.FullName, string framework)
        static member (?<-) (_,dir:DirectoryInfo,group:GroupName) =  Path.Combine (dir.FullName, string group)
        static member (?<-) (_,dir:DirectoryInfo,path:string) =  Path.Combine(dir.FullName,path)
        static member (?<-) (_,dir:DirectoryInfo,file:FileInfo) =  Path.Combine(dir.FullName,file.Name)
        static member (?<-) (_,path:string,framework:FrameworkIdentifier) = Path.Combine(path, string framework)
        static member (?<-) (_,path:string,group:GroupName) =  Path.Combine (path, string group)
        static member (?<-) (_,path1:string,path2:string) =  Path.Combine (path1, path2)
        static member (?<-) (_,path:string,file:FileInfo) =  Path.Combine (path, file.Name)
        static member (?<-) (_,path:string,package:PackageName) =  Path.Combine (path, string package)


    let inline (</>) p1 p2 = (?<-) PathCombine p1 p2

    open PackageAndAssemblyResolution
  
    type ScriptType =
        | CSharp
        | FSharp
        member x.Extension =
            match x with
            | CSharp -> "csx"
            | FSharp -> "fsx"
        static member TryCreate s = 
            match s with
            | "csx" -> Some CSharp
            | "fsx" -> Some FSharp
            | _ -> None


    type Reference =
        | Assembly  of FileInfo
        | Framework of string
        | LoadScript of FileInfo


    type ScriptGenInput = {
        PackageName              : PackageName
        PackagesOrGroupFolder    : DirectoryInfo
        IncludeScriptsRootFolder : DirectoryInfo
        DependentScripts         : FileInfo list
        FrameworkReferences      : string list
        OrderedDllReferences     : FileInfo list
    }


    type ScriptGenResult = 
        | DoNotGenerate
        | Generate of lines : Reference list


    let writeScript (scriptType:ScriptType) (scriptFile:FileInfo) (input:Reference seq) =
        // create a relative path from the directory of the script to the dll or script to load
        let relativePath (scriptFile: FileInfo) (libFile: FileInfo) =
            (Uri scriptFile.FullName).MakeRelativeUri(Uri libFile.FullName).ToString()
        
        // create the approiate load string for the target resource
        let refString (reference:Reference)  = 
            match reference, scriptType with
            | Assembly file, _ ->
                 sprintf """#r "%s" """ <| relativePath scriptFile file
            | LoadScript script, ScriptType.FSharp ->
                 sprintf """#load @"%s" """ <| relativePath scriptFile script
            | LoadScript script, ScriptType.CSharp ->     
                 sprintf """#load "%s" """ <| relativePath scriptFile script
            | Framework name,_ ->
                 sprintf """#r "%s" """ name
        
        let text = input |> Seq.map refString |> String.concat "\n"
        scriptFile.Directory.Create()
        File.WriteAllText(scriptFile.FullName, text)

    let shouldExcludeNugetForFSharpScript nuget =
        match nuget with
        | "FSharp.Core" -> true
        | _ -> false

    /// default implementation of F# include script generator
    let generateScript (scriptType:ScriptType) (input: ScriptGenInput) =
        let packageName = input.PackageName.GetCompareString()

        let depLines =
            input.DependentScripts |> List.map LoadScript
        
        let filterMscorlib assemblies =
            // For F# we never want to reference mscorlib directly (some nuget package state it as a dependency)
            // reason is that having it referenced more than once fails in FSI
            assemblies |> Seq.filter ( function  "mscorlib" -> false | _ -> true)

        let frameworkRefLines =
            match scriptType with 
            | CSharp -> input.FrameworkReferences :> seq<_>
            | FSharp -> input.FrameworkReferences |> filterMscorlib
            |> Seq.map Framework
            |> Seq.toList

        let dllLines =
            match scriptType, packageName.ToLowerInvariant() with
            | FSharp, "fsharp.core" -> []
            | _ -> 
                input.OrderedDllReferences 
                |> List.map Assembly

        let lines = List.concat [depLines; frameworkRefLines; dllLines]
        
        match lines with
        | [] -> DoNotGenerate
        | xs -> Generate xs


    let getScriptFolder (rootFolder: DirectoryInfo) (framework: FrameworkIdentifier) (groupName: GroupName) (folderForDefaultFramework: bool) =
        let group = if groupName = Constants.MainDependencyGroup then String.Empty else (string groupName)
        let framework = if folderForDefaultFramework then String.Empty else string framework
        rootFolder </> framework </> group


    let getScriptFile (rootFolder: DirectoryInfo) (framework: FrameworkIdentifier) (groupName: GroupName)  (folderForDefaultFramework: bool) (package: PackageName) (extension: string) =
        let folder = getScriptFolder rootFolder framework groupName folderForDefaultFramework
        FileInfo <| sprintf "%s.%s"  folder extension


    let generateGroupScript
        (scriptType : ScriptType)
        (groups : GroupName list)
        (lockFile : LockFile)
        (getScriptFile : GroupName -> FileInfo)
        (framework : FrameworkIdentifier) =

        let filterNuget nuget =
            match scriptType, nuget with
            | FSharp, "FSharp.Core" -> true
            | _ ,_ -> false

        let all =
          seq {
            let mainGroupSeen = ref false
            let mainGroupName = Constants.MainDependencyGroup.ToString ()
            let mainGroupKey = Constants.MainDependencyGroup.GetCompareString ()

            for group, nuget, _ in lockFile.InstalledPackages do
                if groups = [] || List.exists ((=) group) groups then
                    if not (filterNuget <|string nuget) then
                        if group.GetCompareString() = mainGroupKey || (string group) = mainGroupName then
                            mainGroupSeen := true

                        let model = lockFile.GetInstalledPackageModel (QualifiedPackageName.FromStrings(Some (string group), string nuget))
                        //let libs = model.GetLibReferences(framework) |> Seq.map (fun f -> FileInfo f.Path)
                        let libs = model.GetCompileReferences (SinglePlatform framework) |> Seq.map (fun f -> FileInfo f.Path)
                        let syslibs = model.GetAllLegacyFrameworkReferences()
                        yield group.GetCompareString(), (libs, syslibs)

            if not !mainGroupSeen && groups = [] then
                  yield mainGroupKey, (Seq.empty, Seq.empty) // Always generate Main group
          }
            |> Seq.groupBy fst
            |> Seq.map (fun (group, items) -> group, items |> Seq.map snd)
      
        for group, libs in all do
            let assemblies, frameworkLibs =
                (libs,  (Seq.empty, Seq.empty))
                ||> Seq.foldBack (fun (l,r) (pl, pr) -> Seq.concat [pl ; l], Seq.concat [pr ; r]) 
                |> fun (l,r) -> Seq.distinct l, Seq.distinct r |> Seq.map (fun ref -> ref.Name) (*|> filterFrameworkAssemblies*)
        
            let assemblies = 
                let assemblyFilePerAssemblyDef = 
                    assemblies
                    |> Seq.map (fun (f:FileInfo) -> AssemblyDefinition.ReadAssembly(f.FullName:string), f)
                    |> dict

                assemblyFilePerAssemblyDef.Keys
                |> Seq.toList
                |> PackageAndAssemblyResolution.getDllOrder
                |> Seq.map (assemblyFilePerAssemblyDef.TryGetValue >> snd)

            let scriptFile = getScriptFile (GroupName group)
        
            [   if not (shouldExcludeFrameworkAssemblies framework) then
                    for a in frameworkLibs do
                        yield Reference.Framework a
                for a in assemblies do
                    yield Reference.Assembly a
            ]
            |> writeScript scriptType scriptFile
  
    /// Generate a include scripts for all packages defined in paket.dependencies,
    /// if a package is ordered before its dependencies this function will throw.
    let generateScriptsForRootFolder
        (scriptType:ScriptType)
        groups
        (framework: FrameworkIdentifier) 
        isDefaultFramework 
        (rootFolder: DirectoryInfo) 
        (dependenciesFile:DependenciesFile)
        (lockFile:LockFile) =
        let fst' (a,_,_) = a

        let dependencies = getPackageOrderFromLockFile lockFile
        let packagesFolder = rootFolder </>  Constants.PackagesFolderName
        let loadScriptsRootFolder = 
            dependenciesFile.DirectoryInfo </> Constants.PaketFolderName </> "load"

        let getScriptFile groupName packageName =
            getScriptFile (DirectoryInfo loadScriptsRootFolder) framework groupName isDefaultFramework packageName scriptType.Extension

        dependencies
        |> Map.map (fun groupName packages ->
            if groups = [] || List.exists ((=) groupName) groups then
                let packagesOrGroupFolder =
                    if groupName = Constants.MainDependencyGroup then packagesFolder
                    else packagesFolder </> groupName
                    
                // fold over a map constructing load scripts to ensure shared packages don't have their scripts duplicated
                (Map.empty,packages)
                ||> Seq.fold (fun (knownIncludeScripts: Map<_,_>) (package: PackageResolver.ResolvedPackage) ->
                        
                    let scriptFile = getScriptFile groupName package.Name
                        
                    let groupName = 
                        if groupName = Constants.MainDependencyGroup then Some (string groupName) else None
                        
                    let dependencies = 
                        package.Dependencies |> Seq.map fst' 
                        |> Seq.choose knownIncludeScripts.TryFind |> List.ofSeq
                        
                    let installModel = 
                        (QualifiedPackageName.FromStrings(groupName, package.Name.ToString()))
                        |> lockFile.GetInstalledPackageModel
                        
                    let dllFiles = 
                        getDllsWithinPackage framework installModel

                    let scriptInfo = {
                        PackageName                  = installModel.PackageName
                        PackagesOrGroupFolder        = DirectoryInfo packagesOrGroupFolder
                        IncludeScriptsRootFolder     = DirectoryInfo loadScriptsRootFolder 
                        FrameworkReferences          = getFrameworkReferencesWithinPackage framework installModel |> List.map (fun ref -> ref.Name)
                        OrderedDllReferences         = dllFiles
                        DependentScripts             = dependencies
                    }

                    match generateScript scriptType scriptInfo with
                    | DoNotGenerate -> knownIncludeScripts
                    | Generate pieces -> 
                    writeScript scriptType scriptFile pieces
                    knownIncludeScripts |> Map.add package.Name scriptFile
                ) |> ignore
        ) |> ignore

        let getGroupFile group = 
            let folder = getScriptFolder (DirectoryInfo loadScriptsRootFolder) framework group isDefaultFramework
            let fileName = (sprintf "%s.group.%s" (string group) scriptType.Extension).ToLowerInvariant()
            FileInfo (folder </> fileName)
        
        generateGroupScript scriptType groups lockFile getGroupFile framework


    let executeCommand groups directory providedFrameworks providedScriptTypes =
        match PaketFiles.LocateFromDirectory directory with
        | PaketFiles.JustDependencies _ -> failwith "paket.lock not found."
        | PaketFiles.DependenciesAndLock (dependenciesFile, lockFile) ->
            let rootFolder = DirectoryInfo dependenciesFile.RootPath
            let frameworksForDependencyGroups = dependenciesFile.ResolveFrameworksForScriptGeneration()
            let environmentFramework = FrameworkDetection.resolveEnvironmentFramework

            let tupleMap f v = (v, f v)
            let failOnMismatch toParse parsed f message =
                if List.length toParse <> List.length parsed then
                    toParse
                    |> Seq.map (tupleMap f)
                    |> Seq.filter (snd >> Option.isNone)
                    |> Seq.map fst
                    |> String.concat ", "
                    |> sprintf "%s: %s. Cannot generate include scripts." message
                    |> failwith

            // prepare list of frameworks to generate, paired with "is default framework"
            // default framework will get generated under root folder rather than framework specific subfolder
            let frameworksToGenerate =
                // specified frameworks are never considered default
                let targetFrameworkList = providedFrameworks |> List.choose FrameworkDetection.Extract |> List.map (fun f -> f, false)

                failOnMismatch providedFrameworks targetFrameworkList FrameworkDetection.Extract "Unrecognized Framework(s)"

                if not (Seq.isEmpty targetFrameworkList) then 
                    targetFrameworkList |> Seq.ofList
                elif not (Seq.isEmpty frameworksForDependencyGroups.Value) then 
                    // if paket.dependencies evaluate to single framework, consider it as default
                    let isDefaultFramework = Seq.length frameworksForDependencyGroups.Value = 1
                    frameworksForDependencyGroups.Value |> Seq.map (fun f -> f, isDefaultFramework)
                  
                else
                    // environment framework is default
                    Seq.singleton (environmentFramework.Value, true)

            let scriptTypesToGenerate =
                let parsedScriptTypes = providedScriptTypes |> List.choose ScriptType.TryCreate

                failOnMismatch providedScriptTypes parsedScriptTypes ScriptType.TryCreate "Unrecognized Script Type(s)"

                match parsedScriptTypes with
                | [] -> [CSharp; FSharp]
                | xs -> xs

            let workaround() = null |> ignore
            for framework, isDefaultFramework in Seq.distinct frameworksToGenerate do
                match groups with
                | []  -> Logging.tracefn "Generating load scripts for framework %O" framework
                | [g] -> Logging.tracefn "Generating load scripts for framework %O in group %O" framework g
                | _   -> Logging.tracefn "Generating load scripts for framework %O in groups: %s" framework (String.Join(", ", groups.ToString()))

                workaround() // https://github.com/Microsoft/visualfsharp/issues/759#issuecomment-162243299
                for scriptType in scriptTypesToGenerate do
                    generateScriptsForRootFolder scriptType  groups framework isDefaultFramework rootFolder dependenciesFile lockFile

