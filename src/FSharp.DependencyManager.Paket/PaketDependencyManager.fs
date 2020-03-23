namespace Microsoft.FSharp.DependencyManager.Paket

// used as a marker that compiler scans for, although there is no hard dependency, filtered by name
type DependencyManagerAttribute() =
  inherit System.Attribute()

module Attributes =
    [<assembly: DependencyManagerAttribute()>]
    do ()

#if PREVIEW5
// dotnet 5.0.100-preview.1.20155.7
type ReturnType = bool * string list * string list * string list 
#else
// VS 16.6.0 Preview 1
type ReturnType = bool * string seq * string seq * string seq
#endif
[<DependencyManager>]
type PaketDependencyManagerProvider(outputDir: string option) =
  member x.Name = "paket"
  member x.Key = "paket"
  member x.ResolveDependencies(scriptDir: string, mainScriptName: string, scriptName: string, packageManagerTextLines: string seq, targetFramework: string) : ReturnType =
    try
      let loadScript, additionalIncludeDirs = 
        ReferenceLoading.PaketHandler.ResolveDependencies(
                 targetFramework,
                 scriptDir,
                 scriptName,
                 packageManagerTextLines)
      true, [] :> _ , [loadScript] :> _ , additionalIncludeDirs:> _ 
    with
      e -> 
        printfn "exception while resolving dependencies: %s" (string e)
        false, [] :> _ , [] :> _ , [] :> _ 
