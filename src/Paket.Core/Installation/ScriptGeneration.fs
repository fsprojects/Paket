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
        static member (?<-) (_,dir:DirectoryInfo,framework:FrameworkIdentifier) =  Path.Combine (dir.Name, string framework)
        static member (?<-) (_,dir:DirectoryInfo,group:GroupName) =  Path.Combine (dir.Name, string group)
        static member (?<-) (_,dir:DirectoryInfo,path:string) =  Path.Combine(dir.Name,path)
        static member (?<-) (_,dir:DirectoryInfo,file:FileInfo) =  Path.Combine(dir.Name,file.Name)
        static member (?<-) (_,path:string,framework:FrameworkIdentifier) = Path.Combine(path, string framework)
        static member (?<-) (_,path:string,group:GroupName) =  Path.Combine (path, string group)
        static member (?<-) (_,path1:string,path2:string) =  Path.Combine (path1, path2)
        static member (?<-) (_,path:string,file:FileInfo) =  Path.Combine (path, file.Name)
        static member (?<-) (_,path:string,package:PackageName) =  Path.Combine (path, string package)
        static member (?<-) (_,path:string,dir:DirectoryInfo) =  Path.Combine (path, dir.Name)

    let inline (</>) p1 p2 = (?<-) PathCombine p1 p2

    open PackageAndAssemblyResolution
    open System.Collections.Generic
    open System.Text.RegularExpressions
  
    type ScriptType =
        | CSharp | FSharp
        member x.Extension = x |> function 
            | CSharp -> "csx" 
            | FSharp -> "fsx"
        
        static member TryCreate = function
            | "csx" -> Some CSharp 
            | "fsx" -> Some FSharp 
            | _ -> None


    type ReferenceType =
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
        | Generate of lines : ReferenceType list

    type ScriptContent = {
        Lang : ScriptType 
        Path : FileInfo 
        Text : string
    } with
        member self.Save () = 
            async {
                self.Path.Directory.Create ()
                File.WriteAllText (self.Path.FullName, self.Text)
            } |> Async.Start


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


    let renderScript (scriptType:ScriptType) (scriptFile:FileInfo) (input:ReferenceType seq) =
        // create a relative pathReferenceType directory of the script to the dll or script to load
        let relativePath (scriptFile: FileInfo) (libFile: FileInfo) =
            (Uri scriptFile.FullName).MakeRelativeUri(Uri libFile.FullName).ToString()
        
        // create the approiate load string for the target resource
        let refString (reference:ReferenceType)  = 
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
        {   Lang = scriptType
            Path = scriptFile
            Text = text
        }

    type RawScript = {
        ReferenceLocations : FileInfo seq
        FrameworkReferences : FrameworkReference seq 
    }

    type GenPrep () =
        let cache = Dictionary<GroupName,RawScript ResizeArray> ()
        let assemblies = Dictionary<GroupName,FileInfo HashSet> ()
        let frameworkLibs = Dictionary<GroupName,FrameworkReference HashSet> ()
        // helper function to accumulate a distinct set of framework libraries
        // and assembly references during lockfile exploration
        let accum (dict:Dictionary<_,HashSet<_>>) groupName elms =  
            elms |> Seq.iter (fun elm ->
            if dict.ContainsKey groupName then 
                dict.[groupName].Add elm |> ignore
            else 
                let set = HashSet() in set.Add elm |> ignore
                dict.[groupName] <- set
            )
        let retrieve (dict:Dictionary<GroupName,'a>) key =
            if dict.ContainsKey key 
            then dict.[key] :> seq<_>
            else Seq.empty
        
        member __.Item 
            with get idx = 
                if cache.ContainsKey idx 
                then cache.[idx] :> seq<_>
                else Seq.empty

            and set groupName (reflocs:FileInfo seq, framelocs:FrameworkReference seq) =
                accum  assemblies  groupName reflocs
                accum frameworkLibs groupName framelocs
                let data = { 
                    ReferenceLocations = reflocs
                    FrameworkReferences = framelocs 
                } 
                if cache.ContainsKey groupName then 
                    cache.[groupName].Add data
                else 
                let arr = ResizeArray() in arr.Add data 
                cache.Add (groupName, arr)
        
        member __.Assemblies
            with get groupName = retrieve assemblies groupName

        member __.FrameworkLibs 
            with get groupName = retrieve frameworkLibs groupName

        member __.Groups = cache |> Seq.map (fun x -> x.Key) 
    

    type PaketContext = {
        Dependencies : DependenciesFile
        LockFile : LockFile
        Groups : GroupName list
        DefaultFramework : bool * FrameworkIdentifier
        RootDir : DirectoryInfo
        ScriptType : ScriptType
    }

    let generateGroupScript (context:PaketContext as ctx) (getScriptFile: GroupName -> FileInfo)  =
        
        let scriptType, groups, rootFolder, dependenciesFile, lockFile = 
            ctx.ScriptType, ctx.Groups, ctx.RootDir, ctx.Dependencies, ctx.LockFile

        let isDefaultFramework, framework = ctx.DefaultFramework

        let filterNuget nuget =
            match scriptType, nuget with
            | FSharp, "FSharp.Core" -> true | _ ,_ -> false

        let mainGroupName = Constants.MainDependencyGroup.Name
        let mainGroupKey = Constants.MainDependencyGroup.CompareString

        let exploreLock installedPackages =
            let rec loop mainGroupSeen  (genPrep:GenPrep) (installedPackages:(GroupName * PackageName * SemVerInfo) list ) =
                match installedPackages with 
                |(group,nuget,_ver)::pkgs  
                    when List.exists ((=) group) context.Groups || groups = []  ->
                    
                    let mainGroupSeen = 
                        (not (filterNuget nuget.Name)) && 
                        (group.CompareString = mainGroupKey || group.Name = mainGroupName)

                    let qualifiedPackage = QualifiedPackageName (group,nuget)
                    let model = lockFile.GetInstalledPackageModel  qualifiedPackage
                    let libs = model.GetLibReferenceFiles framework

                    let syslibs = model.GetAllLegacyFrameworkReferences ()
                    genPrep.[group] <- (libs,syslibs)
                    loop mainGroupSeen genPrep  pkgs
                | _hd::pkgs -> 
                    loop mainGroupSeen genPrep  pkgs
                | [] -> 
                    if mainGroupSeen then genPrep else 
                    // If we haven't explored the main group add it to ensure generation 
                    genPrep.[Constants.MainDependencyGroup]  <- (Seq.empty, Seq.empty)
                    genPrep
            loop false  (GenPrep ()) installedPackages
   
        let genprep = exploreLock lockFile.InstalledPackages

        genprep.Groups |> Seq.map (fun group ->

            let assemblies = 
                let assemblyFilePerAssemblyDef = 
                    genprep.Assemblies group
                    |> Seq.map (fun (f:FileInfo) -> 
                        AssemblyDefinition.ReadAssembly(f.FullName:string), f)
                    |> dict

                assemblyFilePerAssemblyDef.Keys
                |> Seq.toList
                |> PackageAndAssemblyResolution.getDllOrder
                |> Seq.map (assemblyFilePerAssemblyDef.TryGetValue >> snd)

            let scriptFile = getScriptFile group

            let assemblyRefs = 
                assemblies |> Seq.map ReferenceType.Assembly 

            let frameworkRefs = 
                genprep.FrameworkLibs group 
                |> Seq.map (fun x ->  ReferenceType.Framework x.Name)

            Seq.append assemblyRefs  frameworkRefs 
            |> renderScript scriptType scriptFile
        )

    /// Generate a include scripts for all packages defined in paket.dependencies,
    /// if a package is ordered before its dependencies this function will throw.
    let generateScriptsForRootFolder (context:PaketContext as ctx) =
        
        let scriptType, groups, rootFolder, dependenciesFile, lockFile = 
            ctx.ScriptType, ctx.Groups, ctx.RootDir, ctx.Dependencies, ctx.LockFile
        let isDefaultFramework, framework = ctx.DefaultFramework

        // -- LOAD SCRIPT FORMATTING POINT --
        let packagesFolder = rootFolder </>  Constants.PackagesFolderName
        
        // -- LOAD SCRIPT FORMATTING POINT --
        let loadScriptsRootFolder = 
            DirectoryInfo (dependenciesFile.DirectoryInfo </> Constants.PaketFolderName </> "load")

        // -- LOAD SCRIPT FORMATTING POINT --
        let getGroupFile group = 
            let folder = 
                let group = if group = Constants.MainDependencyGroup then String.Empty else (string group)
                let framework = if isDefaultFramework then String.Empty else string framework
                DirectoryInfo (rootFolder </> framework </> group)
            let fileName = (sprintf "%s.group.%s" (string group) scriptType.Extension).ToLowerInvariant()
            FileInfo (folder </> fileName)
        
        // -- LOAD SCRIPT FORMATTING POINT --
        let packagesOrGroupFolder groupName =
            if groupName = Constants.MainDependencyGroup then DirectoryInfo packagesFolder
            else let x = packagesFolder </> groupName in DirectoryInfo x
       
       // -- LOAD SCRIPT FORMATTING POINT --
        let scriptFolder groupName (package: PackageResolver.ResolvedPackage) =
            let group = if groupName = Constants.MainDependencyGroup then String.Empty else (string groupName)
            let framework = if isDefaultFramework then String.Empty else string framework
            DirectoryInfo (rootFolder </> framework </> packagesOrGroupFolder groupName </> package.Name)

        // -- LOAD SCRIPT FORMATTING POINT --
        let scriptFile (scriptFolder:DirectoryInfo) =
            FileInfo <| sprintf "%s.%s"  scriptFolder.FullName ScriptType.FSharp.Extension
                    

        let dependencies = getPackageOrderFromLockFile lockFile

        let scriptContent =
            dependencies |> Map.map (fun groupName packages ->
                if groups = [] || List.exists ((=) groupName) groups then

                    let packagesOrGroupFolder = packagesOrGroupFolder groupName
                    
                    // fold over a map constructing load scripts to ensure shared packages don't have their scripts duplicated
                    ((Map.empty,[]),packages)
                    ||> Seq.fold (fun ((knownIncludeScripts,scriptFiles): Map<_,_>*_) (package: PackageResolver.ResolvedPackage) ->
                        
                        let scriptFolder = scriptFolder groupName  package

                        let scriptFile = scriptFile scriptFolder
                    
                        let dependencies = 
                            package.Dependencies 
                            |> Seq.choose (fun (x,_,_) -> knownIncludeScripts.TryFind x)
                            |> List.ofSeq

                        let installModel = 
                            (QualifiedPackageName.FromStrings(Some groupName.Name, package.Name.ToString()))
                            |> lockFile.GetInstalledPackageModel
            
                        let dllFiles =
                            installModel
                            |> InstallModel.getLegacyReferences (SinglePlatform framework)
                            |> Seq.map (fun l -> FileInfo l.Path) |> List.ofSeq

                        let scriptInfo = {
                            PackageName                  = package.Name
                            PackagesOrGroupFolder        = packagesOrGroupFolder
                            IncludeScriptsRootFolder     = loadScriptsRootFolder 
                            FrameworkReferences          = getFrameworkReferencesWithinPackage framework installModel |> List.map (fun ref -> ref.Name)
                            OrderedDllReferences         = dllFiles
                            DependentScripts             = dependencies
                        }

                        match generateScript scriptType scriptInfo with
                        | DoNotGenerate -> 
                            (knownIncludeScripts,scriptFiles)
                        | Generate pieces -> 
                            let knownScripts = knownIncludeScripts |> Map.add package.Name scriptFile
                            let rendered = (renderScript scriptType scriptFile pieces)::scriptFiles
                            (knownScripts, rendered)
                    ) |> fun (_,sfs) -> sfs 
                else []
            ) |> Seq.collect (fun x -> x.Value)

        generateGroupScript context getGroupFile 


    let constructScriptsFromData groups (dependenciesFile:DependenciesFile) (lockFile:LockFile) providedFrameworks providedScriptTypes =
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
            let targetFrameworkList = 
                providedFrameworks |> List.choose FrameworkDetection.Extract 
                |> List.map (fun f -> f, false)

            failOnMismatch providedFrameworks targetFrameworkList FrameworkDetection.Extract "Unrecognized Framework(s)"

            if not (Seq.isEmpty targetFrameworkList) then 
                targetFrameworkList :> seq<_>
            elif not (Seq.isEmpty frameworksForDependencyGroups.Value) then 
                // if paket.dependencies evaluate to single framework, consider it as default
                let isDefaultFramework = Seq.length frameworksForDependencyGroups.Value = 1
                frameworksForDependencyGroups.Value |> Seq.map (fun f -> f, isDefaultFramework)
                  
            else // environment framework is default
                Seq.singleton (environmentFramework.Value, true)

        let scriptTypesToGenerate =
            let parsedScriptTypes = providedScriptTypes |> List.choose ScriptType.TryCreate

            failOnMismatch providedScriptTypes parsedScriptTypes ScriptType.TryCreate "Unrecognized Script Type(s)"

            match parsedScriptTypes with
            | [] -> [CSharp; FSharp]
            | xs -> xs

        let workaround () = null |> ignore
        let scriptData =
            seq{  
                for framework, isDefaultFramework in Seq.distinct frameworksToGenerate ->
                let msg =
                    match groups with
                    | []  -> framework.ToString ()
                    | [g] -> sprintf "%O in group %O" framework g
                    | _   -> sprintf "%O in groups: %s" framework (String.Join(", ", groups.ToString()))

                Logging.tracefn "Generating load scripts for framework %s" msg

                workaround () // https://github.com/Microsoft/visualfsharp/issues/759#issuecomment-162243299
                seq{ 
                    for scriptType in scriptTypesToGenerate ->
                    generateScriptsForRootFolder {
                        ScriptType = scriptType  
                        RootDir = rootFolder 
                        Groups = groups 
                        DefaultFramework = isDefaultFramework,framework
                        Dependencies = dependenciesFile 
                        LockFile = lockFile
                    }
                } |> Seq.concat
            } |> Seq.concat
        scriptData
    

    let constructScriptsFromDisk groups directory providedFrameworks providedScriptTypes =
        match PaketFiles.LocateFromDirectory directory with
        | PaketFiles.JustDependencies _ -> failwith "paket.lock not found."
        | PaketFiles.DependenciesAndLock (dependenciesFile, lockFile) ->
            let rootFolder = DirectoryInfo dependenciesFile.RootPath
            let frameworksForDependencyGroups = dependenciesFile.ResolveFrameworksForScriptGeneration()
            let environmentFramework = FrameworkDetection.resolveEnvironmentFramework

            constructScriptsFromData groups dependenciesFile lockFile providedFrameworks providedScriptTypes

