(*** hide ***)

type FrameworkRestriction = string 
type PackageName = string
type FrameworkRestrictions = FrameworkRestriction list
type VersionRequirement = string
type PackageSource = string
type SemVerInfo = string


(**
# Package resolution

## Overview

Paket uses the [`paket.dependencies` file](dependencies-file.html) to specify project dependencies.
Usually only direct dependencies are specified and often a broad range of package versions is allowed.
During [`paket install`](paket-install.html) Paket needs to figure out which concrete versions of the specified packages and their transisitve dependencies it needs to install.
These versions are then persisted to the [`paket.lock` file](lock-file.html).

In order to figure out the concrete versions it needs to solve the following constraint satisfaction problem:

* Select the highest version for each of the packages in the [`paket.dependencies` file](dependencies-file.html), plus all their transitive dependencies, such that all version constraints are satisfied. 

Note: In general more than one solution to this problem can exist and the solver will take the first solution that it finds.

## Getting data

A big challenge for Paket's resolver is that it doesn't have the full constraints available. 
It needs to figure these out along the way by retrieving data from the [NuGet](nuget-dependencies.html) source feeds.

The two important questions are:

* What versions of a package are available?
* Given a concrete version of a package, what further dependencies are required?

Answering these questions is a very expensive operation since it involves a HTTP request and therefore the resolver needs to minimize these requests.

## Basic algorithm

Starting from the [`paket.dependencies` file](dependencies-file.html) we have a list of package requirements. 
Every requirement specifies a valid version range and a resolver strategy for a package:

*)

type PackageRequirementSource =
| DependenciesFile of string
| Package of PackageName * SemVerInfo 

type ResolverStrategy = Max | Min

type PackageRequirement =
    { Name : PackageName
      VersionRequirement : VersionRequirement
      ResolverStrategy : ResolverStrategy
      Parent: PackageRequirementSource      
      Sources : PackageSource list }

(**
ttt

*)

type DependencySet = Set<PackageName * VersionRequirement * FrameworkRestrictions>

(**
ttt

*)