namespace Microsoft.FSharp.DependencyManager.Paket

// used as a marker that compiler scans for, although there is no hard dependency, filtered by name
type DependencyManagerAttribute() =
  inherit System.Attribute()

module Attributes =
    [<assembly: DependencyManagerAttribute()>]
    do ()

/// The results of ResolveDependencies
type ResolveDependenciesResult (success: bool, stdOut: string array, stdError: string array, resolutions: string seq, sourceFiles: string seq, roots: string seq) =

    /// Succeded?
    member _.Success = success

    /// The resolution output log
    member _.StdOut = stdOut

    /// The resolution error log (* process stderror *)
    member _.StdError = stdError

    /// The resolution paths
    member _.Resolutions = resolutions

    /// The source code file paths
    member _.SourceFiles = sourceFiles

    /// The roots to package directories
    member _.Roots = roots

[<DependencyManager>]
type PaketDependencyManagerProvider(outputDir: string option) =
  member x.Name = "paket"
  member x.Key = "paket"
  member x.ResolveDependencies(scriptDir: string, mainScriptName: string, scriptName: string, packageManagerTextLines: string seq, targetFramework: string) : ResolveDependenciesResult =
    try
      let loadScript, additionalIncludeDirs = 
        ReferenceLoading.PaketHandler.ResolveDependencies(
                 targetFramework,
                 scriptDir,
                 scriptName,
                 packageManagerTextLines)
      ResolveDependenciesResult(true, [|"ok paket"|], [|"err paket"|], additionalIncludeDirs, [loadScript], [])
    with
      e -> 
        printfn "exception while resolving dependencies: %s" (string e)
        ResolveDependenciesResult(false, [||], [||], [||], [||], [||])
