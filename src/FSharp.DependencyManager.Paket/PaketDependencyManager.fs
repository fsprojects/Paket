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
    let scriptDir =
        if scriptDir = System.String.Empty then
            System.Environment.CurrentDirectory
        else
            scriptDir

    try
      let loadScript, additionalIncludeDirs = 
        ReferenceLoading.PaketHandler.ResolveDependencies(
                 targetFramework,
                 scriptDir,
                 scriptName,
                 packageManagerTextLines)
        
      let resolutions =
          // https://github.com/dotnet/fsharp/pull/10224#issue-498147879
          // if load script causes problem
          // consider changing this to be the list of all assemblies to load rather than passing through a load script
          []
      ResolveDependenciesResult(true, [||], [||], resolutions, [loadScript], additionalIncludeDirs)
    with
      e -> 
        printfn "exception while resolving dependencies: %s" (string e)
        ResolveDependenciesResult(false, [||], [||], [||], [||], [||])
