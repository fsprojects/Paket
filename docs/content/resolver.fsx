(*** hide ***)

type FrameworkRestriction = string
type PackageName = string
type SemVerInfo = string
type FrameworkRestrictions = FrameworkRestriction list
type VersionRequirement = string
type PackageSource = string
type ResolvedPackage = PackageName * SemVerInfo

(**
# Package resolution algorithm

## Overview

Paket uses the [`paket.dependencies` file](dependencies-file.html) to specify
project dependencies. Usually only direct dependencies are specified and often a
broad range of package versions is allowed. During
[`paket install`](paket-install.html) Paket needs to figure out concrete
versions of the specified packages and their transitive dependencies. These
versions are then persisted to the [`paket.lock` file](lock-file.html).

In order to figure out the concrete versions Paket needs to solve the following
constraint satisfaction problem:

* Select the latest version for each of the packages in the
  [`paket.dependencies` file](dependencies-file.html), plus all their transitive
  dependencies, such that all version constraints are satisfied.

**Note:**

* In general, more than one solution to this problem can exist and the solver
  will take the first solution that it finds.
* If you change the
  [resolution strategy](dependencies-file.html#Resolver-strategy-for-transitive-dependencies)
  then Paket needs to find the *oldest matching version*

## Getting data

The
[constraint satisfaction problem](http://en.wikipedia.org/wiki/Constraint_satisfaction_problem)
is covered by many [scientific papers](resolver.html#Further-reading), but a big
challenge for Paket's resolver is that it doesn't have the full constraints
available. The algorithm needs to evaluate the package dependency graph along
the way by retrieving data from the [NuGet](nuget-dependencies.html) source
feeds.

The two important API questions are:

* What versions of a package are available?
* Given a concrete version of a package, what further dependencies are required?

Answering these questions is a very expensive operation since it involves a HTTP
request and therefore the resolver has to minimize these requests and only
access the API when really needed.

## Basic algorithm

Starting from the [`paket.dependencies` file](dependencies-file.html) we have a
set of package requirements. Every requirement specifies a version range and a
resolver strategy for a package:
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

(*** hide ***)

/// Orders the requirement set with a heuristic and selects the next requirement
let selectNextRequirement (xs: Set<PackageRequirement>) = Some(Seq.head xs,xs)

/// Calls the NuGet API and retrieves all versions for a package
let getAllVersionsFromNuget (x:PackageName) :SemVerInfo list = []

/// Checks if the given version is in the specified version range
let isInRange (vr:VersionRequirement) (v:SemVerInfo) : bool = true

// Calls the NuGet API and returns package details for the given package version
let getPackageDetails(name:PackageName,version:SemVerInfo) : ResolvedPackage = Unchecked.defaultof<_>

/// Looks into the cache if the algorithm already selected that package
let getSelectedPackageVersion (name:PackageName) (selectedPackageVersions:Set<ResolvedPackage>) : SemVerInfo option = None

/// Puts all dependencies of the package into the open set
let addDependenciesToOpenSet(packageDetails:ResolvedPackage,closed:Set<PackageRequirement>,stillOpen:Set<PackageRequirement>) : Set<PackageRequirement> = Set.empty

type Resolution =
| Ok of Set<ResolvedPackage>
| Conflict of Set<PackageRequirement>

(**
The algorithm works as a
[Breadth-first search](https://en.wikipedia.org/wiki/Breadth-first_search). In
every step it selects a requirement from the set of *open* requirements and
checks if the requirement can be satisfied. If no conflict arises then a package
version gets selected and all it's dependencies are added to the *open*
requirements. A set of *closed* requirements is maintained in order to prevent
cycles in the search graph.

If the selected requirement results in a conflict then the algorithm backtracks
in the search tree and selects the next version.
*)

let rec step(selectedPackageVersions:Set<ResolvedPackage>,
             closedRequirements:Set<PackageRequirement>,
             openRequirements:Set<PackageRequirement>) =

    match selectNextRequirement openRequirements with
    | Some(currentRequirement,stillOpen) ->
        let availableVersions =
            match getSelectedPackageVersion currentRequirement.Name selectedPackageVersions with
            | Some version ->
                // we already selected a version so we can't pick a different
                [version]
            | None ->
                // we didn't select a version yet so all versions are possible
                getAllVersionsFromNuget currentRequirement.Name

        let compatibleVersions =
            // consider only versions, which match the current requirement
            availableVersions
            |> List.filter (isInRange currentRequirement.VersionRequirement)

        let sortedVersions =
            match currentRequirement.ResolverStrategy with
            | ResolverStrategy.Max -> List.sort compatibleVersions |> List.rev
            | ResolverStrategy.Min -> List.sort compatibleVersions

        let mutable conflictState = Resolution.Conflict(stillOpen)

        for versionToExplore in sortedVersions do
            match conflictState with
            | Resolution.Conflict _ ->
                let packageDetails = getPackageDetails(currentRequirement.Name,versionToExplore)

                conflictState <-
                    step(Set.add packageDetails selectedPackageVersions,
                         Set.add currentRequirement closedRequirements,
                         addDependenciesToOpenSet(packageDetails,closedRequirements,stillOpen))
            | Resolution.Ok _ -> ()

        conflictState
    | None ->
        // we are done - return the selected versions
        Resolution.Ok(selectedPackageVersions)


(**
### Sorting package requirements

In order to make progress in the search tree the algorithm needs to determine
the next package. Paket uses a heuristic, which tries to process packages with
small version ranges and high conflict potential first. Therefore, it orders the
requirements based on:

* Is the
  [version pinned](nuget-dependencies.html#Use-exactly-this-version-constraint)?
* Is it a direct requirement coming from the dependencies file?
* Is the
  [resolution strategy](dependencies-file.html#Resolver-strategy-for-transitive-dependencies)
  `Min` or `Max`?
* How big is the current
  [package specific boost factor](resolver.html#Package-conflict-boost)?
* How big is the specified version range?
* The package name (alphabetically) as a tie breaker.

### Package conflict boost

Whenever Paket encounters a package version conflict in the search tree it
increases a boost factor for the involved packages. This heuristic influences
the [package evaluation order](resolver.html#Sorting-package-requirements) and
forces the resolver to deal with conflicts much earlier in the search tree.

## Branch and bound

Every known resolution conflict is stored in a `HashSet`. At every step Paket
will always check if the current requirement set (union of *open* requirements
and *closed* requirements) is a superset of a known conflict. In this case Paket
can stop evaluating that part of the search tree.

## Caching and lazy evaluation

Since HTTP requests to NuGet are very expensive Paket tries to reduce these
calls as much as possible:

* The function `getAllVersionsFromNuget` will call the NuGet API at most once
  per package and Paket run.
* The function `getPackageDetails` will only call the NuGet API if package
  details are not found in the RAM or on disk.

The second caching improvement means that subsequent runs of `paket update` can
get faster since package details are already stored on disk.

## Error reporting

If the resolver can't find a valid resolution, then it needs to report an error
to the user. Since the search tree can be very large and might contain lots of
different kinds of failures, reporting a good error message is difficult. Paket
will only report the last conflict that it can't resolve and also some
information about the origin of this conflict.

If you need more information you can try the verbose mode by using the
`--verbose` parameter.

## Further reading

* [Modular lazy search for Constraint Satisfaction Problems](http://journals.cambridge.org/action/displayAbstract?fromPage=online&aid=83363&fileId=S0956796801004051)
  by T. Nordin and A. Tolmach ([PDF](http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.34.4704&rep=rep1&type=pdf))
* [On The Forward Checking Algorithm](http://citeseerx.ist.psu.edu/viewdoc/summary?doi=10.1.1.45.2528)
  by F. Bacchus and A. Grove ([PDF](http://www.cs.toronto.edu/~fbacchus/Papers/BGCP95.pdf))
* [Structuring Depth-First Search Algorithms in Haskell](http://citeseerx.ist.psu.edu/viewdoc/summary?doi=10.1.1.52.6526)
  by D. King and J. Launchbury ([PDF](http://galois.com/wp-content/uploads/2014/08/pub_JL_StructuringDFSAlgorithms.pdf))
* [Qualified Goals in the Cabal Solver](http://www.well-typed.com/blog/2015/03/qualified-goals/)
  ([Video](https://vimeo.com/31846783))
*)
