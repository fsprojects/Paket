module Paket.LoadingScripts

open System
open System.IO
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


    type ScriptContent = {
        Lang : ScriptType         
        Input : ReferenceType seq
        PartialPath : string
    } with
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

        member self.Save (directory:DirectoryInfo) = 
            async {
                directory.Create()
                let scriptFile = FileInfo (directory.FullName </> self.PartialPath)
                let text = self.Render directory
                File.WriteAllText (scriptFile.FullName, text)
            } |> Async.Start


    type PaketContext = {
        Cache : DependencyCache
        Groups : GroupName list
        DefaultFramework : bool * FrameworkIdentifier
        RootDir : DirectoryInfo
        ScriptType : ScriptType
    }


    /// Generate a include scripts for all packages defined in paket.dependencies,
    /// if a package is ordered before its dependencies this function will throw.
    let generateScriptsForRootFolder (context:PaketContext as ctx) =
        
        let scriptType, groups, dependenciesFile, lockFile = 
            ctx.ScriptType, ctx.Groups, ctx.Cache.DependenciesFile, ctx.Cache.LockFile
        let isDefaultFramework, framework = ctx.DefaultFramework

        // -- LOAD SCRIPT FORMATTING POINT --
        let packagesFolder =Constants.PackagesFolderName
        
        // -- LOAD SCRIPT FORMATTING POINT --
        let loadScriptsRootFolder = 
            Constants.PaketFolderName </> "load"

        // -- LOAD SCRIPT FORMATTING POINT --
        let getGroupFile group = 
            let folder = 
                let group = if group = Constants.MainDependencyGroup then String.Empty else (string group)
                let framework = if isDefaultFramework then String.Empty else string framework
                framework </> group
            let fileName = (sprintf "%s.group.%s" (string group) scriptType.Extension).ToLowerInvariant()
            folder </> fileName
              
        let packagesOrGroupFolder groupName =
            if groupName = Constants.MainDependencyGroup then packagesFolder
            else packagesFolder </> groupName 
       

       // -- LOAD SCRIPT FORMATTING POINT --
        let scriptFolder groupName (package: PackageResolver.ResolvedPackage) =
            let group = if groupName = Constants.MainDependencyGroup then String.Empty else (string groupName)
            let framework = if isDefaultFramework then String.Empty else string framework
            loadScriptsRootFolder </> framework </> group </> package.Name

        // -- LOAD SCRIPT FORMATTING POINT --
        let scriptFile (scriptFolder:string) =
            sprintf "%s.%s"  scriptFolder scriptType.Extension
                    

        let scriptContent =
            ctx.Cache.OrderedGroups() |> Map.map (fun groupName packages ->
                if groups = [] || List.exists ((=) groupName) groups then
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

                        let dllFiles = ctx.Cache.GetOrderedPackageReferences groupName package.Name framework
                        let frameworkRefs = ctx.Cache.GetOrderedFrameworkReferences groupName package.Name framework|> List.map (fun ref -> ref.Name)

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
                                } ::scriptFiles
                            (knownScripts, rendered)
                    ) |> fun (_,sfs) -> sfs 
                else []
            ) |> Seq.collect (fun x -> x.Value)

        let isDefaultFramework, framework = ctx.DefaultFramework
        
        let content =
            ctx.Groups |> Seq.map (fun group ->
                let scriptFile = getGroupFile  group
                let pieces = ctx.Cache.GetOrderedReferences group framework
                
                {   PartialPath = scriptFile
                    Lang = ctx.ScriptType 
                    Input = pieces 
                } : ScriptContent
            )
        Seq.append scriptContent content


    let constructScriptsFromData (depCache:DependencyCache) (groups:GroupName list) providedFrameworks providedScriptTypes =
        let dependenciesFile = depCache.DependenciesFile
        let frameworksForDependencyGroups = dependenciesFile.ResolveFrameworksForScriptGeneration()
        let environmentFramework = FrameworkDetection.resolveEnvironmentFramework
        let lockFile = depCache.LockFile
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
                            Cache = depCache
                            ScriptType = scriptType  
                            RootDir = DirectoryInfo lockFile.RootPath
                            Groups = groups 
                            DefaultFramework = isDefaultFramework,framework
                        }
                } |> Seq.concat
            } |> Seq.concat
        scriptData
    


