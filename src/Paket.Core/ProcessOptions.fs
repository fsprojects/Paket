namespace Paket

[<RequireQualifiedAccess>]
type SemVerUpdateMode =
    | NoRestriction
    | KeepMajor
    | KeepMinor
    | KeepPatch

// Options for UpdateProcess and InstallProcess.
/// Force          - Force the download and reinstallation of all packages
///                  to the Paket's conventions (and hence considered manually managed)
/// Redirects      - Create binding redirects for the NuGet packages
/// OnlyReferenced - Only install packages that are referenced in paket.references files.
type InstallerOptions =
    { Force : bool
      SemVerUpdateMode : SemVerUpdateMode
      Redirects : bool
      CreateNewBindingFiles : bool
      OnlyReferenced : bool }

    static member Default =
        { Force = false
          Redirects = false
          SemVerUpdateMode = SemVerUpdateMode.NoRestriction
          CreateNewBindingFiles = false
          OnlyReferenced = false }

    static member CreateLegacyOptions(force, redirects, createNewBindingFiles, semVerUpdateMode) =
        { InstallerOptions.Default with
            Force = force
            CreateNewBindingFiles = createNewBindingFiles
            Redirects = redirects 
            SemVerUpdateMode = semVerUpdateMode }

type UpdaterOptions =
    { Common : InstallerOptions
      NoInstall : bool }

    static member Default =
        { Common = InstallerOptions.Default
          NoInstall = false }
