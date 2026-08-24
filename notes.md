# Repo Assist Memory — fsprojects/Paket

## Backlog cursor (Task 1 - Issue Labelling)
Last processed unlabelled issue (oldest-first, `no:label`): #3174 (Feb 2018 era).
Next run should resume search with `no:label` sorted created asc, starting after #3174 (many more from 2018-2024 remain; ~476 unlabelled issues left as of 2026-08-24).

## Labels applied this run (2026-08-24, run 32789431246)
- #3055 -> documentation, good first issue
- #3063 -> enhancement, help wanted
- #3081 -> bug, needs investigation
- #3110 -> bug, needs investigation
- #3116 -> bug
- #3126 -> performance
- #3160 -> bug, needs investigation
- #3174 -> refactor

## Comments made (Task 2)
- #3055: posted root-cause analysis of misleading credentials error message (LockFile.Parse generic exception wrapping vs. NuGetCache 401 handling). Awaiting maintainer/reporter response before further engagement.

## Issues reviewed but NOT commented on (already well-covered / no new value to add)
- #3116: dotnet SDK limitation with paket.local + SDK-style csproj — already exhaustively discussed by @forki/@matthid 2018-2023, nothing new to add.

## Task 4 - Engineering Investments findings (not yet actioned)
Candidate dependency bumps identified via NuGet.org version checks:
- Newtonsoft.Json 13.0.1 -> 13.0.4
- Mono.Cecil 0.11.3 -> 0.11.6 (compatible with existing ~> 0.11.1 constraint)
- NUnit 3.12 -> 3.13.x (NOTE: issue #3174 "Get rid of NUnit" is open; check with maintainers before bumping NUnit itself)
- NUnit3TestAdapter 3.13 -> 3.17.0
- Microsoft.NET.Test.Sdk 16.2 -> 16.11.0
- Moq 4.16.1 -> 4.20.72 (large jump, needs more care/testing)

**BLOCKER discovered 2026-08-24**: `dotnet paket install` fails in this sandbox because the `Build` dependency group (used for docs, e.g. FSharp.Formatting 3.0.0-beta09) resolves from `https://ci.appveyor.com/nuget/fsharp-formatting`, and the sandbox's outbound proxy returns 403 for that host. This blocks *any* paket.lock regeneration, not just the dependency bump attempted. Reverted the attempted paket.dependencies edit (NUnit/NUnit3TestAdapter/Microsoft.NET.Test.Sdk bump) and abandoned branch `repo-assist/eng-bump-test-deps-2026-08-24` (deleted locally, never pushed/PR'd).
Future runs: if network access allows ci.appveyor.com, retry the bump above. Otherwise consider an approach that doesn't require full `paket install` (e.g., manually patching paket.lock hashes/versions is unsafe - do not do this) or investigate why appveyor source is still needed / whether it can be removed from paket.dependencies Build group.

## Task 11 - Monthly Activity Summary
Created new issue "[repo-assist] Monthly Activity 2026-08" this run (no prior monthly issue existed in repo). Update this same issue in future runs within 2026-08; create a new one when the month rolls over.

## General notes
- 500 open issues total as of 2026-08-24, only ~16 labelled at start of run.
- Open PRs snapshot at start of run: #4334, #4331, #4330, #4323, #4322, #4321, #4292, #4271 — none from Repo Assist.
- gh CLI unauthenticated in this sandbox; all GitHub writes go through safeoutputs tools; all GitHub reads go through github MCP tools.
