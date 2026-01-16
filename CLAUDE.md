# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Paket is a dependency manager for .NET with support for NuGet packages and git repositories. It maintains transitive dependency information in `paket.lock` alongside `paket.dependencies`, providing explicit control over dependency resolution that NuGet traditionally lacked.

## Build Commands

```bash
# Full build (restores, builds, runs tests)
build.cmd                    # Windows
./build.sh                   # Linux/Mac

# Build with specific targets
build.cmd Build              # Build only
build.cmd QuickTest          # Run unit tests without full build
build.cmd RunTests           # Run all unit tests
build.cmd QuickIntegrationTests  # Run quick integration tests (scriptgen category)
build.cmd RunIntegrationTestsNet # Run full .NET Framework integration tests
build.cmd RunIntegrationTestsNetCore # Run full .NET Core integration tests

# Skip specific stages
build.cmd SkipTests          # Skip all tests
build.cmd SkipIntegrationTests # Skip integration tests only
```

## Testing

**Unit Tests:**
```bash
dotnet test tests/Paket.Tests/Paket.Tests.fsproj
dotnet test tests/Paket.Tests/Paket.Tests.fsproj --filter "FullyQualifiedName~SemVerSpecs"
```

**Integration Tests:**
```bash
dotnet test integrationtests/Paket.IntegrationTests/Paket.IntegrationTests.fsproj
dotnet test integrationtests/Paket.IntegrationTests/Paket.IntegrationTests.fsproj --filter "TestCategory=scriptgen"
```

Tests use NUnit. Integration tests have scenario folders under `integrationtests/scenarios/` with `before/` directories containing test fixtures.

## Architecture

### Project Structure

- **src/Paket.Core/** - Core library with all dependency management logic (F#)
- **src/Paket/** - CLI executable (F#, references Paket.Core)
- **src/Paket.Bootstrapper/** - Downloads paket.exe on first run (C#)
- **src/FSharp.DependencyManager.Paket/** - F# Interactive integration (`#r "paket:"` support)

### Paket.Core Organization

The core library is organized into these logical layers (see `Paket.Core.fsproj` for exact file order):

1. **Common/** - Shared utilities (logging, async helpers, XML, networking, encryption)
2. **Versioning/** - SemVer parsing, version ranges, framework handling, platform matching, package sources
3. **Dependencies/** - NuGet protocol (V2/V3), Git handling, package resolution algorithm, dependency parsing
4. **PaketConfigFiles/** - File format parsers/writers for paket.dependencies, paket.lock, paket.references, .fsproj/.csproj
5. **Installation/** - Restore, install, update processes; binding redirects; script generation
6. **PackageAnalysis/** - Outdated detection, find-refs, simplifier, `paket why` functionality
7. **PackageManagement/** - Add/remove packages, NuGet conversion
8. **Packaging/** - `paket pack` for creating NuGet packages

### Key Types

- `DependenciesFile` - Represents paket.dependencies
- `LockFile` - Represents paket.lock
- `ReferencesFile` - Represents paket.references
- `ProjectFile` - Represents .fsproj/.csproj with Paket modifications
- `SemVerInfo` - Semantic version parsing
- `FrameworkIdentifier` - Target framework handling
- `PackageResolver` - The dependency resolution algorithm

### Public API

`PublicAPI.fs` exposes the `Dependencies` class for programmatic access, used by F# Interactive and external tooling.

### CLI Commands

Commands are defined in `src/Paket/Commands.fs` using Argu. Each command has corresponding args types (e.g., `AddArgs`, `RestoreArgs`). Command documentation is auto-generated from these definitions.

## Target Frameworks

- **Paket.Core**: `net461` and `netstandard2.0`
- **Paket CLI**: `net461` and `net8`
- **Tests**: `net461` and `net8` (some tests may be framework-specific via `#if` directives)

## Conditional Compilation

Key symbols used:
- `DOTNETCORE` / `NETSTANDARD1_5` / `NETSTANDARD1_6` - .NET Core builds
- `NO_BOOTSTRAPPER` - Exclude bootstrapper code paths
- `USE_WEB_CLIENT_FOR_UPLOAD` - Use WebClient on .NET Framework for uploads

## Integration Test Scenarios

Each scenario in `integrationtests/scenarios/` typically has:
- `before/` - Initial state with paket.dependencies, paket.lock, project files
- Tests copy `before/` to `temp/`, run Paket commands, verify results
- Scenario names often reference GitHub issue numbers (e.g., `i001234-description`)

## Release Process

The build script reads `RELEASE_NOTES.md` for version info. Release builds use ILRepack to merge assemblies into a single `paket.exe`. The merged executable goes to `bin/merged/`.
