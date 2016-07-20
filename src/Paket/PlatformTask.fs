// Keep this file for compatibility issues
namespace MSBuild.Tasks

open System
open System.IO
open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open Paket
open Paket.Domain
open Paket.Requirements

type CopyRuntimeDependencies() =
    inherit Task()

    let mutable outputPath = ""
    let mutable targetFramework = ""
    let mutable projectFile = ""
    let mutable projectsWithRuntimeLibs = ""

    [<Required>]
    member this.OutputPath
        with get() = outputPath
        and set(v) = outputPath <- v

    [<Required>]
    member this.ProjectsWithRuntimeLibs
        with get() = projectsWithRuntimeLibs
        and set(v) = projectsWithRuntimeLibs <- v

    member this.ProjectFile
        with get() = projectFile
        and set(v) = projectFile <- v

    member this.TargetFramework
        with get() = targetFramework
        and set(v) = targetFramework <- v

    override this.Execute() = true
