namespace Paket

[<RequireQualifiedAccess>]
type SemVerUpdateMode =
    | NoRestriction
    | KeepMajor
    | KeepMinor
    | KeepPatch

// Options for UpdateProcess and InstallProcess.
/// Force          - Force the download and reinstallation of all packages
/// Hard           - Replace package references within project files even if they are not yet adhering
///                  to the Paket's conventions (and hence considered manually managed)
/// Redirects      - Create binding redirects for the NuGet packages
/// OnlyReferenced - Only install packages that are referenced in paket.references files.
type InstallerOptions =
    { Force : bool
      Hard : bool
      SemVerUpdateMode : SemVerUpdateMode
      Redirects : bool
      CreateNewBindingFiles : bool
      OnlyReferenced : bool }

    static member Default =
        { Force = false
          Hard = false
          Redirects = false
          SemVerUpdateMode = SemVerUpdateMode.NoRestriction
          CreateNewBindingFiles = false
          OnlyReferenced = false }

    static member CreateLegacyOptions(force, hard, redirects, createNewBindingFiles, semVerUpdateMode) =
        { InstallerOptions.Default with
            Force = force
            Hard = hard
            CreateNewBindingFiles = createNewBindingFiles
            Redirects = redirects 
            SemVerUpdateMode = semVerUpdateMode }

type UpdaterOptions =
    { Common : InstallerOptions
      NoInstall : bool }

    static member Default =
        { Common = InstallerOptions.Default
          NoInstall = false }
