/// Finds and matches files with glob patterns, like `**/*.dll`.
module internal Paket.Globbing

open System
open System.IO
open Fake.IO.Globbing

/// Checks whether the path matches the glob pattern
let isMatch (pattern : string) (path : string) = Glob.isMatch pattern path

/// Finds the files and folders that match the pattern, relative to the base directory when the pattern is relative
let search (baseDirectory : string) (pattern : string) =
    { LazyGlobbingPattern.BaseDirectory = baseDirectory; Includes = [ pattern ]; Excludes = [] }
    |> Seq.toList

/// Finds the files of a template: a pattern that starts with a separator is relative to the base directory,
/// unless it is under it or a UNC path
let searchTemplateFiles (baseDirectory : string) (pattern : string) =
    let separator = Path.DirectorySeparatorChar
    let pattern = Glob.normalizePath pattern
    let underBaseDirectory = (Glob.normalizePath baseDirectory).TrimEnd separator + string separator
    let isUnderBaseDirectory = (pattern.TrimEnd separator + string separator).StartsWith(underBaseDirectory, StringComparison.Ordinal)
    let isUncPath = separator = '\\' && pattern.StartsWith(@"\\", StringComparison.Ordinal)
    if pattern.Length > 0 && pattern.[0] = separator && not isUncPath && not isUnderBaseDirectory then
        search baseDirectory (pattern.TrimStart separator)
    else
        search baseDirectory pattern
