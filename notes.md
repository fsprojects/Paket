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

## Run 2026-08-24 (later) — 32973902060

### Task 1 - Labels applied (backlog cursor now past #3147)
- #2672 -> help wanted, question
- #3064 -> bug
- #3068 -> enhancement, help wanted
- #3117 -> bug
- #3129 -> bug
- #3130 -> bug
- #3140 -> bug
- #3141 -> question
- #3142 -> bug
- #3144 -> bug, needs investigation
- #3147 -> bug

### Task 3 - Fix completed for #3129
Root cause: `PackageSource.Parse` in `src/Paket.Core/Versioning/PackageSources.fs` used `line.Split(' ').[1]` for unquoted source lines, truncating paths with spaces (e.g. `source C:\Program Files\...` -> `C:\Program`). Quoted paths already worked correctly (regex-based).
Fix: for unquoted lines, take everything after `source` up to any trailing `username:`/`password:`/`authtype:` attribute, instead of splitting on first space.
Added 3 new tests in `tests/Paket.Tests/Versioning/PackageSourceSpecs.fs`.
Branch: `repo-assist/fix-issue-3129-source-path-spaces`. Draft PR created and linked from issue #3129 via comment.
Validated: build 0 errors, PackageSourceSpecs 14/14 pass, ParserSpecs+SaveSpecs 172/172 pass (no regressions).
**Status: PR open, awaiting maintainer review — do not re-attempt this fix in future runs unless PR is closed without merge.**

### Build/test workaround discovered (IMPORTANT for future runs)
Full `dotnet paket install`/`restore` and any `dotnet build`/`test` that triggers Paket's MSBuild restore target fail in this sandbox because the `Build` dependency group needs `ci.appveyor.com` (blocked, 403). Workaround:
1. `dotnet paket restore --group Main` (Main group only, standalone) works.
2. For building/testing a project: `dotnet restore <proj> -p:PaketDisableGlobalRestore=true` THEN `dotnet build/test <proj> -f net9 --no-restore -p:PaketDisableGlobalRestore=true`. Both steps need the flag; skipping the restore step and only using it on build fails with NETSDK1004.
This should be used for validating any future Task 3/4/5/8/9 fixes without needing full paket install.

### Task 4 - not attempted this run (time prioritized on Task 3 fix). Candidate bumps from prior run still pending (see above), still blocked pending either network access or Main-group-only bump strategy validation.

### Task 11 - updated existing issue #4344 (2026-08 monthly activity) with this run's history entry, refreshed backlog/suggested-actions sections.

## Run 2026-08-26 13:44 UTC — 32975801244 (tasks: 1, 3, 2)

### Task 1 - Labels applied (backlog cursor now past #3221)
- #3149 -> enhancement, good first issue
- #3152 -> enhancement
- #3155 -> bug, needs investigation
- #3162 -> bug
- #3163 -> enhancement
- #3166 -> bug, performance
- #3167 -> enhancement
- #3171 -> bug
- #3172 -> bug, needs investigation
- #3182 -> enhancement, help wanted
- #3193 -> bug
- #3195 -> bug
- #3198 -> bug, needs investigation
- #3202 -> bug
- #3203 -> bug
- #3207 -> bug
- #3209 -> bug
- #3220 -> bug, needs investigation
- #3221 -> performance, needs investigation

### Task 2 - Comments made
- #3149: noted `paket init` already defaults to NuGet v3 (`DefaultNuGetV3Source`) since v5.231.0 per RELEASE_NOTES.md; suggested closing if confirmed by reporter. Confirmed via `git log` on PackageAnalysis/Environment.fs.
- #3195: suggested `paket pack` should read MSBuild `PackageId` property (via `ProjectFile.GetProperty "PackageId"`) as fallback before assembly name, matching `dotnet pack` behavior.

### Task 3 - not completed this run
Reviewed #3081 (stale integration test issue from 2018, low value), #3110 (cache staleness, hard to repro), #3117 (duplicate System.Net.Http refs, complex resolver/binding-redirect interaction), #3140 (large output not fully read), #3142 (local-source-priority resolver bug, complex), #3160 (dotnet pack path error, needs live repro). None were confident, low-risk, verifiable fixes without a live repro environment. Deferred — do not re-review #3081/#3110/#3117/#3142/#3160 unless new info emerges; consider deeper dive on #3195 (PackageId) as an actual implementable fix in a future run since root cause is now understood.

### Task 11 - updated issue #4344 (2026-08 monthly activity), refreshed suggested actions and run history (reverse-chron).

### General
- Open PR backlog still includes prior repo-assist fix branches: repo-assist/fix-issue-3129-source-path-spaces-*, repo-assist/fix-issue-4346-webutilities-version-pin-*, repo-assist/ci-github-actions-* — check status/merge in a future Task 6 run.
