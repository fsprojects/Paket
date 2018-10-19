namespace Paket

[<RequireQualifiedAccess>]
type SemVerUpdateMode =
    | NoRestriction
    | KeepMajor
    | KeepMinor
    | KeepPatch

// Options for UpdateProcess and InstallProcess.
/// Force             - Force the download and reinstallation of all packages
/// Redirects         - Create binding redirects for the NuGet packages
/// OnlyReferenced    - Only install packages that are referenced in paket.references files.
/// TouchAffectedRefs - Touch projects referencing installed packages even if the project file does not change.
type InstallerOptions =
    { Force : bool
      SemVerUpdateMode : SemVerUpdateMode
      Redirects : Requirements.BindingRedirectsSettings
      AlternativeProjectRoot : string option
      CleanBindingRedirects : bool
      CreateNewBindingFiles : bool
      OnlyReferenced : bool
      GenerateLoadScripts : bool
      ProvidedScriptTypes : string list
      ProvidedFrameworks  : string list
      TouchAffectedRefs : bool }

    static member Default =
        { Force = false
          Redirects = Requirements.BindingRedirectsSettings.Off
          SemVerUpdateMode = SemVerUpdateMode.NoRestriction
          CreateNewBindingFiles = false
          AlternativeProjectRoot = None
          OnlyReferenced = false
          CleanBindingRedirects = false
          GenerateLoadScripts = false
          ProvidedScriptTypes = []
          ProvidedFrameworks = []
          TouchAffectedRefs = false }

    static member CreateLegacyOptions(force, redirects, cleanBindingRedirects, createNewBindingFiles, semVerUpdateMode, touchAffectedRefs, generateLoadScripts, providedFrameworks, providedScriptTypes, alternativeProjectRoot) =
        { InstallerOptions.Default with
            Force = force
            CreateNewBindingFiles = createNewBindingFiles
            CleanBindingRedirects = cleanBindingRedirects
            Redirects = redirects
            SemVerUpdateMode = semVerUpdateMode
            TouchAffectedRefs = touchAffectedRefs
            ProvidedFrameworks = providedFrameworks
            ProvidedScriptTypes = providedScriptTypes
            GenerateLoadScripts = generateLoadScripts
            AlternativeProjectRoot = alternativeProjectRoot}

type UpdaterOptions =
    { Common : InstallerOptions
      NoInstall : bool }

    static member Default =
        { Common = InstallerOptions.Default
          NoInstall = false }
