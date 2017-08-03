module Paket.LoadingScripts

open System
open System.IO
open Pri.LongPath
open Paket
open Paket.Domain


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

    open System.Collections.Generic
    open Paket.Logging
  
    type ScriptType =
        | CSharp | FSharp
        member x.Extension = x |> function 
            | CSharp -> "csx" 
            | FSharp -> "fsx"
        
        static member TryCreate = function
            | "csx" -> Some CSharp 
            | "fsx" -> Some FSharp 
            | _ -> None


    type ScriptGenInput = {
        PackageName              : PackageName
        IncludeScriptsRootFolder : DirectoryInfo
        DependentScripts         : FileInfo list
        FrameworkReferences      : string list
        OrderedDllReferences     : FileInfo list
    }

    type ScriptGenResult = 
        | DoNotGenerate
        | Generate of lines : ReferenceType list

    // TODO - For FSI we don't want to load FSharp.Core unless '--noframework' has been specified
    //        we currently don't address this situation well
    let filterFSharpRefTypes (scriptType:ScriptType) (refls:ReferenceType list) =
        refls |> List.filter ( fun ref ->
            if scriptType = ScriptType.FSharp then 
                match ref with 
                | Assembly info -> not <| String.containsIgnoreCase "FSharp.Core" info.Name
                | Framework info -> not <|  String.containsIgnoreCase "FSharp.Core" info
                | LoadScript info -> not <| String.containsIgnoreCase "FSharp.Core" info.Name
            else true
        )

    let filterForFSI (scriptType:ScriptType) refls =
        // For F# we never want to reference mscorlib directly (some nuget package state it as a dependency)
        // reason is that having it referenced more than once fails in FSI
        refls |> List.filter ( fun ref ->
            if scriptType = ScriptType.FSharp then
                match ref with
                | Framework info -> info <> "mscorlib"
                | _ -> true
            else true
        )

    let filterReferences scriptType = filterFSharpRefTypes scriptType >> filterForFSI scriptType

    /// default implementation of F# include script generator
    let generateScript (scriptType:ScriptType) (input: ScriptGenInput) =
        let packageName = input.PackageName.CompareString
        
        let lines =
            if String.equalsIgnoreCase packageName "FSharp.Core" then [] else

            let scriptRefs =
                input.DependentScripts |> List.map LoadScript

            let frameworkRefLines =
                input.FrameworkReferences
                |> List.map Framework
        
            let dllLines = input.OrderedDllReferences |> List.map Assembly
        
            List.concat [scriptRefs; frameworkRefLines; dllLines]
            |> filterReferences scriptType
        
        match lines with
        | [] -> DoNotGenerate
        | xs -> Generate xs


    type ScriptContent = {
        Lang : ScriptType         
        Input : ReferenceType seq
        PartialPath : string
    } with
        /// use the provided directory to compute the relative paths for the script's contents
        /// and construct the 
        member self.Render (directory:DirectoryInfo) =
            let scriptFile = FileInfo (directory.FullName </> self.PartialPath)

            // create a relative pathReferenceType directory of the script to the dll or script to load
            let relativePath (scriptFile: FileInfo) (libFile: FileInfo) =
                (Uri scriptFile.FullName).MakeRelativeUri(Uri libFile.FullName).ToString()
                
            // create the approiate load string for the target resource
            let refString (reference:ReferenceType)  = 
                match reference, self.Lang with
                | Assembly file, _ ->
                     sprintf """#r "%s" """ <| relativePath scriptFile file
                | LoadScript script, ScriptType.FSharp ->
                     sprintf """#load @"%s" """ <| relativePath scriptFile script
                | LoadScript script, ScriptType.CSharp ->     
                     sprintf """#load "%s" """ <| relativePath scriptFile script
                | Framework name,_ ->
                     sprintf """#r "%s" """ name
        
            self.Input |> Seq.map refString |> String.concat "\n"
        
        /// Save the script in '<directory>/.paket/load/<script>'
        member self.Save (directory:DirectoryInfo) = 
            directory.Create()
            let scriptFile = FileInfo (directory.FullName </> self.PartialPath)
            if verbose then
                verbosefn "generating script - %s" scriptFile.FullName
            scriptFile.Directory.Create()
            let text = self.Render directory
            File.WriteAllText (scriptFile.FullName, text)


    type PaketContext = {
        Cache : DependencyCache
        Groups : GroupName list
        DefaultFramework : bool * FrameworkIdentifier
        RootDir : DirectoryInfo
        ScriptType : ScriptType
    }


    /// Generate a include scripts for all packages defined in paket.dependencies,
    /// if a package is ordered before its dependencies this function will throw.
    let generateScriptContent (context:PaketContext as ctx) =
        
        let scriptType, groups, (isDefaultFramework, framework) = ctx.ScriptType, ctx.Groups, ctx.DefaultFramework

        // -- LOAD SCRIPT FORMATTING POINT --
        let loadScriptsRootFolder = Constants.PaketFolderName </> "load"

        // -- LOAD SCRIPT FORMATTING POINT --
        /// Create the path for a script that will load all of the packages in the provided Group
        let getGroupFile group = 
            let folder = 
                let group = if group = Constants.MainDependencyGroup then String.Empty else (string group)
                let framework = if isDefaultFramework then String.Empty else string framework
                framework </> group
            let fileName = (sprintf "%s.group.%s" (string group) scriptType.Extension).ToLowerInvariant()
            loadScriptsRootFolder </> folder </> fileName
              
       // -- LOAD SCRIPT FORMATTING POINT --
        let scriptFolder groupName (package: PackageResolver.ResolvedPackage) =
            let group = if groupName = Constants.MainDependencyGroup then String.Empty else (string groupName)
            let framework = if isDefaultFramework then String.Empty else string framework
            loadScriptsRootFolder </> framework </> group </> package.Name

        // -- LOAD SCRIPT FORMATTING POINT --
        let scriptFile (scriptFolder:string) =
            sprintf "%s.%s"  scriptFolder scriptType.Extension
                    
        let cachedGroups =
            if List.isEmpty groups then
                ctx.Cache.OrderedGroups() 
                |> Seq.map (fun kvp -> kvp.Key,kvp.Value)
            else
                groups
                |> Seq.map (fun g -> g,ctx.Cache.OrderedGroups g)

        let scriptContent =
            cachedGroups
            |> Seq.map (fun (groupName,packages) ->
                    // fold over a map constructing load scripts to ensure shared packages don't have their scripts duplicated
                    ((Map.empty,[]),packages)
                    ||> Seq.fold (fun ((knownIncludeScripts,scriptFiles): Map<_,string>*_) (package: PackageResolver.ResolvedPackage) ->
                        
                        let scriptFolder = scriptFolder groupName  package

                        let scriptFile = scriptFile scriptFolder
                    
                        let dependencies = 
                            package.Dependencies 
                            |> Seq.choose (fun (x,_,_) -> knownIncludeScripts.TryFind x)
                            |> List.ofSeq
                            |> List.map FileInfo

                        let dllFiles = 
                            ctx.Cache.GetOrderedPackageReferences groupName package.Name framework

                        let frameworkRefs = 
                            ctx.Cache.GetOrderedFrameworkReferences groupName package.Name framework
                            |> List.map (fun ref -> ref.Name)

                        let scriptInfo = {
                            PackageName                  = package.Name
                            IncludeScriptsRootFolder     = DirectoryInfo loadScriptsRootFolder 
                            FrameworkReferences          = frameworkRefs
                            OrderedDllReferences         = dllFiles
                            DependentScripts             = dependencies
                        }

                        match generateScript scriptType scriptInfo with
                        | DoNotGenerate -> 
                            (knownIncludeScripts,scriptFiles)
                        | Generate pieces -> 
                            let knownScripts = knownIncludeScripts |> Map.add package.Name scriptFile
                            let rendered =  
                                {   PartialPath = scriptFile
                                    Lang = scriptType
                                    Input = pieces 
                                } 
                            (knownScripts, rendered::scriptFiles)) 
                    |> fun (_,sfs) -> groupName, sfs
            ) |> List.ofSeq

        // generate scripts to load all packages within a group
        let groupScriptContent =
            ctx.Groups 
            |> List.map (fun group ->
                let scriptFile = getGroupFile group
                let pieces =
                    ctx.Cache.GetOrderedReferences group framework
                    |> filterReferences ctx.ScriptType

                let scriptContent : ScriptContent =
                    { PartialPath = scriptFile
                      Lang = ctx.ScriptType 
                      Input = pieces } 

                group,[scriptContent]
            ) 
        List.append scriptContent groupScriptContent
        

    let constructScriptsFromData (depCache:DependencyCache) (groups:GroupName list) providedFrameworks providedScriptTypes =
        let dependenciesFile = depCache.DependenciesFile
        let frameworksForDependencyGroups = dependenciesFile.ResolveFrameworksForScriptGeneration()
        let environmentFramework = FrameworkDetection.resolveEnvironmentFramework
        let lockFile = depCache.LockFile

        let groups = 
            if List.isEmpty groups then 
                dependenciesFile.Groups |> Seq.map (fun kvp -> kvp.Key) |> Seq.toList 
            else 
                groups
        
        if verbose then
            verbosefn "Generating load scripts for the following groups: %A" (groups |> List.map (fun g -> g.Name.ToString()))
            verbosefn " - using Paket dependency file: %s" dependenciesFile.FileName
            verbosefn " - using Packe fock file: %s" lockFile.FileName

        let tupleMap f v = (v, f v)
        let failOnMismatch toParse parsed fn message =
            if List.length toParse <> List.length parsed then
                toParse
                |> Seq.map (tupleMap fn)
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
                providedFrameworks 
                |> List.choose FrameworkDetection.Extract 
                |> List.map (fun f -> f, false)

            failOnMismatch providedFrameworks targetFrameworkList FrameworkDetection.Extract "Unrecognized Framework(s)"

            if not (Seq.isEmpty targetFrameworkList) then 
                targetFrameworkList :> seq<_>
            elif not (Seq.isEmpty frameworksForDependencyGroups.Value) then 
                // if paket.dependencies evaluate to single framework, consider it as default
                let isDefaultFramework = Seq.length frameworksForDependencyGroups.Value = 1
                frameworksForDependencyGroups.Value 
                |> Seq.map (fun f -> f, isDefaultFramework)
            else // environment framework is default
                Seq.singleton (environmentFramework.Value, true)
            |> Seq.distinct

        let frameworks = frameworksToGenerate |> Seq.map fst |> List.ofSeq
        let scriptTypesToGenerate =
            let parsedScriptTypes = providedScriptTypes |> List.choose ScriptType.TryCreate

            failOnMismatch providedScriptTypes parsedScriptTypes ScriptType.TryCreate "Unrecognized script type(s)"

            match parsedScriptTypes with
            | [] -> [CSharp; FSharp]
            | xs -> xs

        let workaround () = null |> ignore
        let scriptData =
            [ for framework, isDefaultFramework in frameworksToGenerate ->
                workaround () // https://github.com/Microsoft/visualfsharp/issues/759#issuecomment-162243299
                [ for scriptType in scriptTypesToGenerate ->
                    let content = generateScriptContent {
                        Cache = depCache
                        ScriptType = scriptType  
                        RootDir = DirectoryInfo lockFile.RootPath
                        Groups = groups
                        DefaultFramework = isDefaultFramework,framework
                    }
                    (framework, content)
                ] 
            ] |> List.concat
        
        let allGroupsEmpty (ls:(FrameworkIdentifier *(_ * ScriptContent list)list) list) =
            ls |> List.forall (snd >> List.forall (snd>> List.isEmpty))
            
        // Report that no script generation was possible for the provided frameworks and groups
        if List.isEmpty scriptData || allGroupsEmpty scriptData then 
            let frameworkMsg =  
                sprintf "for %s %s" 
                    (if frameworks.Length = 1 then "framework" else "frameworks") 
                    (frameworks |> List.map (fun x -> x.ToString()) |> String.concat " ")
            let groupMsg = 
                sprintf "in %s %A" 
                    (groups.Length |> function 0|1 -> "group" | _ -> "groups") 
                    (if List.isEmpty groups then 
                        Constants.MainDependencyGroup.Name 
                     else 
                        String.concat " " (groups|>List.map string))
            tracefn "Could not generate any scripts %s %s" frameworkMsg groupMsg
        else // report the scripts that were generated for each framework by each group
            if verbose then
                for framework, grouped in scriptData do
                    tracefn "Generating load scripts for framework - %O" framework
                    for group,scriptContent in grouped do 
                        if List.isEmpty scriptContent then 
                            tracefn "Could not generate any scripts for group '%O'" group
                        else
                            tracefn "[ Group - %O ]" group  
                            scriptContent |> Seq.iter (fun sc -> tracefn " - %O" sc.PartialPath)
        let generated =
            scriptData 
            |> Seq.collect (fun (_fw,groupedContent) -> 
                groupedContent |> Seq.collect snd)
        generated
    


