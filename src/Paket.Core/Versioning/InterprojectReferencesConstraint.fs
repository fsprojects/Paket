module Paket.InterprojectReferencesConstraint
open Paket

/// Constraints template for interproject references
type InterprojectReferencesConstraint =
    | Min
    | Fix
    | KeepMajor
    | KeepMinor
    | KeepPatch
with
    static member Parse = function
        | "min" -> Some Min
        | "fix" -> Some Fix
        | "keep-major" -> Some KeepMajor
        | "keep-minor" -> Some KeepMinor
        | "keep-patch" -> Some KeepPatch
        | _ -> None
    member this.CreateVersionRequirements v =
        match this with
        | Min -> Minimum v
        | Fix -> Specific v
        | KeepMajor ->
            let nextMajor = {
                Major = v.Major + 1u
                Minor = 0u
                Patch = 0u
                Build = 0I
                PreRelease = None
                BuildMetaData = ""
                Original = None
            }
            VersionRange.Between(v, nextMajor)
        | KeepMinor ->
            let nextMinor = {
                Major = v.Major
                Minor = v.Minor + 1u
                Patch = 0u
                Build = 0I
                PreRelease = None
                BuildMetaData = ""
                Original = None
            }
            VersionRange.Between(v, nextMinor)

        | KeepPatch ->
            let nextPatch = {
                Major = v.Major
                Minor = v.Minor
                Patch = v.Patch + 1u
                Build = 0I
                PreRelease = None
                BuildMetaData = ""
                Original = None
            }
            VersionRange.Between(v, nextPatch)