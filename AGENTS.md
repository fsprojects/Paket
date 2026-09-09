# Paket Agent Guidelines

## Purpose

Paket is a dependency manager for .NET projects, implemented primarily in F#. Prefer stability, correctness, compatibility, and minimal dependencies. Make small, focused changes and avoid unrelated refactoring.

Read `README.md` for product usage and `DEV_GUIDE.md` for additional development notes.

## Repository Layout

- `src/Paket.Core/`: dependency resolution, file formats, installation, package management, and packaging logic.
- `src/Paket/`: command-line interface and command definitions.
- `src/Paket.Bootstrapper/`: bootstrapper implemented in C#.
- `src/FSharp.DependencyManager.Paket/`: F# Interactive integration.
- `tests/Paket.Tests/`: NUnit unit tests.
- `integrationtests/Paket.IntegrationTests/`: NUnit integration test code.
- `integrationtests/scenarios/`: integration fixtures, usually with input under a `before/` directory.
- `docs/content/`: user documentation.

The exact F# compile order is declared in each `.fsproj`. When adding or moving an F# file, add it to the project file in dependency order.

## Working Conventions

- Follow the style and abstractions in the surrounding F# or C# code.
- Fix root causes and preserve existing public APIs unless the task explicitly requires an API change.
- Add or update focused tests for behavior changes and bug fixes when feasible.
- Do not introduce a dependency unless its value clearly outweighs the compatibility and maintenance cost.
- Keep changes compatible with all target frameworks declared by the touched project. Some areas still require .NET Framework or Mono tooling.
- Treat warnings as errors. Do not suppress a warning globally to accommodate a local change.
- Do not edit generated artifacts or integration-test `temp/` output.
- Keep documentation examples synchronized with actual CLI behavior and file formats.

## Build and Test

Use the narrowest validation that covers the change:

```bash
dotnet test tests/Paket.Tests/Paket.Tests.fsproj
dotnet test tests/Paket.Tests/Paket.Tests.fsproj --filter "FullyQualifiedName~SemVerSpecs"
dotnet test integrationtests/Paket.IntegrationTests/Paket.IntegrationTests.fsproj
dotnet test integrationtests/Paket.IntegrationTests/Paket.IntegrationTests.fsproj --filter "TestCategory=scriptgen"
```

Repository build targets:

```bash
./build.sh Build
./build.sh QuickTest
./build.sh QuickIntegrationTests
./build.sh RunIntegrationTestsNetCore
./build.sh
```

On Windows, use the corresponding `build.cmd` commands. Both scripts run `build.fsx` with `dotnet fsi`, so the build script itself only needs the .NET SDK declared in `global.json`. The full build restores tools and dependencies, builds all projects, and runs broad test suites; on Linux it still depends on Mono for the targets that run .NET Framework binaries, namely the `net461` test passes and `PublishNuGet`. If those prerequisites are unavailable, run the relevant `dotnet test` command and report what was not validated.

## Tests

- Unit and integration tests use NUnit.
- Place unit tests near the existing specification module for the affected behavior.
- For command-level or filesystem behavior, prefer an existing integration scenario pattern over constructing a new test harness.
- Keep fixtures minimal and deterministic. Do not depend on mutable external state when a local or existing fixture can cover the behavior.
- Run the smallest relevant test first, then broaden validation according to the change's risk and scope.

## Change Boundaries

- Resolver, lock-file, framework-restriction, restore, and installation changes have broad behavioral impact; test representative transitive and integration cases.
- CLI argument changes belong in `src/Paket/Commands.fs` and may affect generated command documentation or shell completion.
- Public programmatic APIs are exposed through `src/Paket.Core/PublicAPI.fs`; preserve compatibility deliberately.
- New source files must be included explicitly in the appropriate project file.