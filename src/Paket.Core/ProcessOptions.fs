namespace Paket

// Options for UpdateProcess and InstallProcess.
/// Force          - Force the download and reinstallation of all packages
/// Hard           - Replace package references within project files even if they are not yet adhering
///                  to the Paket's conventions (and hence considered manually managed)
/// Redirects      - Create binding redirects for the NuGet packages
/// OnlyReferenced - Only install packages that are referenced in paket.references files.
type InstallerOptions =
    { Force : bool
      Hard : bool
      Redirects : bool
      CreateNewBindingFiles : bool
      OnlyReferenced : bool }

    static member Default =
        { Force = false
          Hard = false
          Redirects = false
          CreateNewBindingFiles = false
          OnlyReferenced = false }

    static member CreateLegacyOptions(force, hard, redirects, createNewBindingFiles) =
        { InstallerOptions.Default with
            Force = force
            Hard = hard
            CreateNewBindingFiles = createNewBindingFiles
            Redirects = redirects }

type UpdaterOptions =
    { Common : InstallerOptions
      NoInstall : bool }

    static member Default =
        { Common = InstallerOptions.Default
          NoInstall = false }
