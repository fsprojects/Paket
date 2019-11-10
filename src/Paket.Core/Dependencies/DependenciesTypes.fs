namespace Paket

open System
open Paket.Domain
open Paket.Requirements
open Paket.ModuleResolver
open Paket.PackageSources


/// [omit]
type InstallOptions =
    { Strict : bool
      Redirects : BindingRedirectsSettings option
      ResolverStrategyForDirectDependencies : ResolverStrategy option
      ResolverStrategyForTransitives : ResolverStrategy option
      Settings : InstallSettings }

    static member Default = {
        Strict = false
        Redirects = None
        ResolverStrategyForTransitives = None
        ResolverStrategyForDirectDependencies = None
        Settings = InstallSettings.Default }

type VersionStrategy = {
    VersionRequirement : VersionRequirement
    ResolverStrategy : ResolverStrategy option }

type DependenciesGroup = {
    Name: GroupName
    Sources: PackageSource list
    Caches: Cache list
    Options: InstallOptions
    Packages : PackageRequirement list
    ExternalLocks : string list
    RemoteFiles : UnresolvedSource list
}
    with
        static member New(groupName) =
            { Name = groupName
              Options = InstallOptions.Default
              Sources = []
              Caches = []
              Packages = []
              ExternalLocks = []
              RemoteFiles = [] }

        member this.CombineWith (other:DependenciesGroup) =
            { Name = this.Name
              Options =
                { Redirects = this.Options.Redirects ++ other.Options.Redirects
                  Settings = this.Options.Settings + other.Options.Settings
                  Strict = this.Options.Strict || other.Options.Strict
                  ResolverStrategyForDirectDependencies = this.Options.ResolverStrategyForDirectDependencies ++ other.Options.ResolverStrategyForDirectDependencies
                  ResolverStrategyForTransitives = this.Options.ResolverStrategyForTransitives ++ other.Options.ResolverStrategyForTransitives }
              Sources = this.Sources @ other.Sources |> List.distinct
              Caches = this.Caches @ other.Caches |> List.distinct
              Packages = this.Packages @ other.Packages
              ExternalLocks = this.ExternalLocks @ other.ExternalLocks
              RemoteFiles = this.RemoteFiles @ other.RemoteFiles }
