namespace Paket

// Options for UpdateProcess and InstallProcess.
/// Force     - Force the download and reinstallation of all packages
/// Hard      - Replace package references within project files even if they are not yet adhering
///             to the Paket's conventions (and hence considered manually managed)
/// Redirects - Create binding redirects for the NuGet packages
type CommonOptions =
    { Force : bool
      Hard : bool
      Redirects : bool }

    static member Default =
        { Force = false
          Hard = false
          Redirects = false }

    static member createLegacyOptions(force, hard, redirects) =
        { CommonOptions.Default with
            Force = force
            Hard = hard
            Redirects = redirects }
