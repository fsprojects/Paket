namespace Microsoft.FSharp.DependencyManager.Paket

// used as a marker that compiler scans for, although there is no hard dependency, filtered by name
type DependencyManagerAttribute() =
  inherit System.Attribute()

module Attributes =
    [<assembly: DependencyManagerAttribute()>]
    do ()

[<DependencyManager>]
type PaketDependencyManagerProvider(outputDir: string option) =
  member x.Name = "paket"
  member x.Key = "paket"
  member x.ResolveDependencies(scriptDir: string, mainScriptName: string, scriptName: string, packageManagerTextLines: string seq, targetFramework: string) : bool * string list * string list * string list =
    try
      let loadScript, additionalIncludeDirs = 
        ReferenceLoading.PaketHandler.ResolveDependencies(
                 targetFramework,
                 scriptDir,
                 scriptName,
                 packageManagerTextLines)
      true, [], [loadScript], additionalIncludeDirs
    with
      e -> 
        printfn "exception while resolving dependencies: %s" (string e)
        false, [], [], []
