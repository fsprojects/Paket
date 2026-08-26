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

## Run 2026-08-26 14:44 UTC — 32981752353 (tasks: 2, 8, 1)

### Task 1 - Labels applied (no:label search now reaches #3236)
- #3068 -> enhancement
- #3141 -> question
- #3226 -> enhancement, help wanted
- #3228 -> bug, needs investigation
- #3232 -> bug
- #3236 -> performance, enhancement

### Task 2 - Comment made
- #3228: root-cause analysis - git clone hangs on non-interactive build agents (VSTS) because Paket shells out to git.exe with no credential provider (TODO comment in RemoteDownload.fs) and no default timeout (gitTimeOut = TimeSpan.MaxValue); git falls back to interactive prompt which blocks forever. Suggested GIT_TERMINAL_PROMPT=0 and pre-configured credentials as workarounds.

### Task 8 - Performance/reliability fix implemented and PR'd
Root cause identified while investigating #3228/#3236: git subprocess calls via `Paket.Git.CommandHelper.runGitCommand` (src/Paket.Core/Dependencies/GitCommandHelper.fs) had no GIT_TERMINAL_PROMPT=0 set, so an auth-requiring clone could hang indefinitely on CI waiting for an interactive prompt nobody can answer.
Fix: set `info.EnvironmentVariables.["GIT_TERMINAL_PROMPT"] <- "0"` in `runGitCommand`. Minimal, single-line change (plus comment).
Branch: `repo-assist/perf-git-terminal-prompt-2026-08-26`. Draft PR created.
Validated: `dotnet build src/Paket.Core/Paket.Core.fsproj -f netstandard2.0` succeeded 0 errors. `dotnet test tests/Paket.Tests/Paket.Tests.fsproj -f net9 --filter "FullyQualifiedName~Git"` = 35/36 passed; the 1 failure (GitInfoPlanterSpecs, path-separator/URL-encoding mismatch on Linux) is pre-existing and unrelated (reproduces on main without this patch).
**Network note**: this run, `dotnet restore src/Paket.Core/Paket.Core.fsproj -p:PaketDisableGlobalRestore=true` succeeded fully (paket-files git downloads worked) — unlike a prior run blocked on ci.appveyor.com. Network availability appears to vary between sandbox runs; retry Task 4 dependency-bump work in future runs since restore worked this time.
**Status: PR open, awaiting maintainer review.**

### Task 11 - updated issue #4344 (2026-08 monthly activity), added new PR/comment to suggested actions and run history (reverse-chron). No maintainer comments found on the issue yet.

## Run 2026-08-26 15:58 UTC — 32986402206 (tasks: 1, 2, 3)

### Task 1 - Labels applied (no:label search advanced to #3262)
- #3147 -> enhancement, help wanted
- #3063 -> enhancement
- #3238 -> bug
- #3250 -> bug, good first issue
- #3254 -> enhancement
- #3255 -> enhancement
- #3258 -> bug
- #3262 -> bug

### Task 2 - Comment made
- #3238: root-cause analysis - restore early-exit is purely lock-file-hash based (RestoreProcess.fs `canEarlyExit`), unaware of whether `load/` scripts still exist on disk. Deleting load/ without changing paket.lock causes restore to report "up to date" and skip regeneration. Flagged as needing a cache-invalidation fix; did not attempt PR since it touches the restore cache format.

### Task 3 - Fix implemented and PR'd for #3250
Root cause: `FindAllFiles` in src/Paket.Core/Common/Utils.fs used `GetFiles(pattern, SearchOption.AllDirectories)`, recursing into dot-folders (.git, .vs, .localhistory, etc.) and picking up stray backup files (e.g. .sln backups as reported in the issue).
Fix: rewrote FindAllFiles as a manual recursive walk skipping directories whose name starts with `.`.
Added test `FindAllFiles should not descend into dot folders` in tests/Paket.Tests/UtilsSpecs.fs.
Branch: `repo-assist/fix-issue-3250-skip-dot-folders`. Draft PR created (not yet linked with issue comment - PR body contains `Closes #3250`).
Validated: dotnet build src/Paket.Core/Paket.Core.fsproj -f netstandard2.0 succeeded 0 errors. New test 1/1 passed. Broader UtilsSpecs: 25 passed/2 skipped/4 pre-existing proxy-env-var failures unrelated to this change (reproduce on main).
**Status: PR open, awaiting maintainer review.**

### General
- Network restore worked fine again this run (dotnet restore src/Paket.Core succeeded, no ci.appveyor.com block encountered this time).
- Still 3 open repo-assist PRs to check in a future Task 6 run: fix-issue-3129-source-path-spaces, perf-git-terminal-prompt-2026-08-26, fix-issue-3250-skip-dot-folders (new), plus older ones (webutilities-version-pin, ci-github-actions).

## Run 2026-08-26 16:43 UTC — 32989808243 (tasks: 1, 2, 9)

### Task 1 - Labels applied (no:label search advanced to #3277)
- #3263 -> bug
- #3264 -> bug, needs investigation
- #3266 -> enhancement, help wanted
- #3268 -> question
- #3271 -> enhancement, good first issue
- #3272 -> bug, needs investigation
- #3274 -> question
- #3277 -> enhancement, good first issue

### Task 2 - Comment made
- #3271: implementation pointer for bootstrapper download-progress feature (WebRequestProxy.cs / WebClient.DownloadProgressChanged + IWebRequestProxy interface change needed).

### Task 9 - Testing improvement + bug fix implemented and PR'd
Investigated `createRelativePath` (src/Paket.Core/Common/Utils.fs) while looking for test gaps. Found it only unescapes `%20` (spaces) from the URI-encoded relative path returned by `Uri.MakeRelativeUri`, so paths containing `#`, `%`, `&`, etc. come back with escaped sequences embedded (e.g. `some#file` -> `some%23file`). Verified with a standalone fsi repro.
Fix: replaced the space-only `.Replace("%20"," ")` with `Uri.UnescapeDataString(...)` on the whole relative string, which handles all percent-encoded characters generically.
Added 3 new tests to tests/Paket.Tests/UtilsSpecs.fs (`#`, `&`, `%` characters), alongside the existing "handle spaces" test.
Branch: `repo-assist/fix-createrelativepath-special-chars`. Draft PR created.
Validated: dotnet build src/Paket.Core/Paket.Core.fsproj -f netstandard2.0 succeeded 0 errors. createRelativePath tests 4/4 passed. ParserSpecs+SaveSpecs 172/172 passed (no regressions). Full UtilsSpecs run showed the same 4 pre-existing proxy-env-var failures as prior runs (unrelated, reproduce on main).
**Status: PR open, awaiting maintainer review.**

### General
- Network restore worked fine again this run (dotnet tool restore + dotnet paket restore --group Main succeeded without ci.appveyor.com issues).
- Now 4 open repo-assist fix/improvement PRs to check in a future Task 6 run: fix-issue-3129-source-path-spaces, perf-git-terminal-prompt-2026-08-26, fix-issue-3250-skip-dot-folders, fix-createrelativepath-special-chars (new).
- Task 11: updated issue #4344 (2026-08 monthly activity) via full body replace — refreshed suggested actions (added new PR + #3271 comment) and prepended new run history entry. No maintainer comments found on the issue yet.

## Run 2026-08-26 17:xx UTC — 32997796455 (tasks: 3, 2, 1)

### Task 1 - Labels applied (no:label search advanced past #3285)
- #3278 -> enhancement
- #3281 -> bug
- #3282 -> bug
- #3284 -> bug, breaking change
- #3285 -> question

### Task 3 - Fix implemented and PR'd for #3195 (paket pack ignores PackageId)
Root cause (already identified in a prior run's Task 2 comment): `paket pack` derives the package id solely from the compiled assembly name via `PackageMetaData.readAssemblyFromProjFile`/`getId`, never consulting the MSBuild `PackageId` project property, unlike `dotnet pack`.
Fix: added `PackageProcess.resolveProjectId` (internal, in src/Paket.Core/Packaging/PackageProcess.fs) which resolves id in priority: existing template id > MSBuild `PackageId` property (via `ProjectFile.GetProperty "PackageId"`) > assembly name fallback. Changed `merge`'s call site and visibility (private -> internal, needed for InternalsVisibleTo test access).
Added 3 new tests in tests/Paket.Tests/Packaging/PackageProcessSpecs.fs covering: prefers PackageId, falls back to assembly name, does not override existing template id.
Branch: `repo-assist/fix-issue-3195-packageid`. Draft PR created (Closes #3195). Posted a brief follow-up comment on #3195 linking the PR (did not re-explain root cause, already covered by prior comment).
Validated: dotnet build src/Paket.Core/Paket.Core.fsproj -f netstandard2.0 succeeded 0 errors. New tests 3/3 passed via `--filter "FullyQualifiedName~PackagingProcess"`. Only failure was the known pre-existing `Loading assembly metadata works` test (missing net461 build output in this sandbox — reproduces on main, unrelated).
**Status: PR open, awaiting maintainer review.**

Note: mid-session, an errant `git checkout -- .` accidentally reverted both uncommitted files; edits were reconstructed via the edit tool and re-verified with a fresh build+test before committing. Lesson for future runs: avoid `git checkout -- .`; use `git status`/`git diff` to inspect instead, and commit early.

### General
- Now 5 open repo-assist fix/improvement PRs to check in a future Task 6 run: fix-issue-3129-source-path-spaces, perf-git-terminal-prompt-2026-08-26, fix-issue-3250-skip-dot-folders, fix-createrelativepath-special-chars, fix-issue-3195-packageid (new).
- Backlog cursor (no:label issues) now past #3285.
- Task 11: to be updated this run - issue #4344 (2026-08 monthly activity), adding new PR + labels + comment to suggested actions and run history.

## Run 2026-08-26 18:xx UTC — 33003623805 (tasks: 10, 4, 1)

### Task 1 - Labels applied (no:label search)
- #3287 -> bug, enhancement
- #3289 -> bug
- #3292 -> enhancement, performance
- #3295 -> enhancement

### Task 4 - Dependency bump PR
Bumped Newtonsoft.Json 13.0.1->13.0.4 and Mono.Cecil 0.11.3->0.11.6 via `dotnet paket update <pkg> --group Main --no-install` (one package per invocation - multi-package syntax fails). Both already within paket.dependencies constraints, no dependencies-file edit needed.
Branch: `repo-assist/eng-bump-newtonsoft-cecil-20260826`. Draft PR created.
Validated: restore + build (netstandard2.0) succeeded 0 errors; full test suite 1238 passed/16 skipped/6 failed - all 6 pre-existing (proxy env-var tests, net461-only assembly-metadata test, FSharp.Data.SqlClient XML test), confirmed same on master.
**Status: PR open, awaiting maintainer review.**

### Task 10 - Fix implemented and PR'd for #3238 (restore doesn't regenerate deleted load scripts)
Root cause (identified in a prior run's Task 2 comment): RestoreProcess.fs's `readCache`/`canEarlyExit` only compares a SHA256 hash of paket.lock; it never checks whether `.paket/load/` actually exists on disk. Deleting `load/` without touching paket.lock made restore silently skip regeneration.
Fix: added `loadScriptsMissing` helper (checks `Directory.Exists ".paket/load"` when any group has `GenerateLoadScripts = Some true`) and folded it into the `canEarlyExit` condition in `readCache`.
Branch: `repo-assist/fix-issue-3238-load-script-cache`. Draft PR created (Closes #3238). No new automated test added (documented in PR as a gap - the logic lives inside `Restore`'s private closure and needs a full integration-test scenario with an on-disk load folder, not a simple unit test); flagged for maintainer/future-run follow-up.
Validated: dotnet restore + build src/Paket.Core (netstandard2.0) succeeded 0 errors. Full test suite: 1238 passed/16 skipped/6 failed - same 6 pre-existing failures as always (proxy env vars, net461 assembly metadata, FSharp.Data.SqlClient XML), confirmed no regressions vs master.
**Status: PR open, awaiting maintainer review.**

### Prior issues now resolved
#3129, #3250, #3195 all confirmed CLOSED by maintainer dsyme (merged via #4354, #4358, #4360) - no longer need repo-assist follow-up; removed from Suggested Actions in monthly issue.

### General
- Backlog cursor (no:label issues) now past #3295.
- Now 7 open repo-assist fix/improvement PRs: fix-issue-3129* (merged - remove from tracking next run if confirmed), perf-git-terminal-prompt-2026-08-26, fix-issue-3250-skip-dot-folders, fix-createrelativepath-special-chars, fix-issue-3195-packageid, eng-bump-newtonsoft-cecil-20260826 (new), fix-issue-3238-load-script-cache (new). NOTE: next run's Task 6 should re-verify actual open/closed state via github MCP rather than trusting this list, since some may have merged.
- Task 11: updated issue #4344 (2026-08 monthly activity) via full body replace - added 2 new PRs to suggested actions, removed #3129/#3250/#3195-related entries (now closed/merged), updated future-work section, prepended new run history entry. No maintainer comments found on the issue yet.

## Run 2026-08-26 (latest) — 33008409123 (tasks: 1, 2, 3)

### Task 1 - Labels applied (no:label search, resumed from cursor past #3295)
- #3064 -> enhancement, help wanted
- #3144 -> bug, needs investigation
- #3155 -> bug
- #3162 -> enhancement
- #3166 -> bug, performance
- #3171 -> bug
- #3172 -> bug, needs investigation
- #3193 -> bug
- #3198 -> bug, needs investigation
- #3202 -> bug
- #3203 -> bug
- #3209 -> bug
Backlog cursor now past #3209 (note: overlaps slightly with an earlier oldest-first pass since `no:label` results shift as issues get labelled across runs — always re-query `no:label` fresh rather than trusting a raw issue-number cursor).

### Task 3 - Fix implemented and PR'd for #3209 (Paket.Restore.targets import moved to end of project file)
Root cause: `installForDotnetSDK` (src/Paket.Core/Installation/InstallProcess.fs) unconditionally called `RemoveImportForPaketTargets()` then `AddImportForPaketTargets(relativePath)` on every install/restore. `AddImportForPaketTargets` always appends at the end of the project XML, so any project whose import wasn't already last got reordered on every run.
Fix: added a check (`getDescendants "Import" |> List.exists (withAttributeValue "Project" relativePath)`) before the remove/re-add; skip both calls if an Import with the same path already exists, preserving position. Added `open Paket.Xml` to InstallProcess.fs (needed for `getDescendants`/`withAttributeValue`).
Branch: `repo-assist/fix-issue-3209-import-reorder`. Draft PR created (Closes #3209). Posted a follow-up comment on #3209 linking the PR.
Validated: dotnet build src/Paket.Core (netstandard2.0) succeeded 0 errors. Full ProjectFile test suite: 109/109 passed via `--filter "FullyQualifiedName~ProjectFile"`. No dedicated new unit test added (documented in PR as a trade-off — `installForDotnetSDK`/full ProjectFile fixture scaffolding needed for a targeted test).
**Status: PR open, awaiting maintainer review.**

### Task 2 - Comment on #3202 (self-update fails with Access is denied on Windows)
Investigated GitHubDownloadStrategy.SelfUpdateCore (src/Paket.Bootstrapper/DownloadStrategies/GitHubDownloadStrategy.cs) and NugetDownloadStrategy's equivalent — both do `MoveFile(exePath, renamedPath)` to rename the currently-running exe in place, which fails on Windows when the file is locked (AV scanner, OS loader, etc.). Posted root-cause analysis with workaround (close AV/IDE processes holding a handle) and a suggested more-robust fix direction (retry policy, or `MoveFileEx` with `MOVEFILE_DELAY_UNTIL_REBOOT`). Did not attempt a code fix — Windows-specific file-locking semantics, higher risk, deferred to future run if a maintainer wants it prioritized.

### Task 3 (secondary investigation, not completed) - #3166 (clitool defeats install noop-check)
Explored DependencyChangeDetection.fs, UpdateProcess.fs (SmartInstall/SelectiveUpdate), PackageResolver.fs (DotnetCliTool Kind assignment), RestoreProcess.fs (canEarlyExit — confirmed this is restore-specific, not relevant to install noop). Leading hypothesis: `Settings` for clitool packages as re-derived from the dependencies file at runtime never compares equal to what's stored/round-tripped in the lock file, forcing `SettingsChanged`/full resolve every `paket install` when a clitool package is present. NOT confirmed — need to trace `findNuGetChangesInDependenciesFile`/`hasChangedSettings` comparison logic further with a live repro in a future run before attempting a fix.

### Task 11 - Updated issue #4344 (2026-08 monthly activity)
Full body replace: added #3209 PR to suggested actions, added #3202 comment note, updated backlog labelling count/list, added #3166 investigation note to future work, prepended new run history entry linking run 33008409123. No new maintainer comments found on the issue.

### General
- Now 8 open repo-assist fix/improvement PRs to check in a future Task 6 run: perf-git-terminal-prompt-2026-08-26, fix-issue-3250-skip-dot-folders, fix-createrelativepath-special-chars, fix-issue-3195-packageid, eng-bump-newtonsoft-cecil-20260826, fix-issue-3238-load-script-cache, fix-issue-3209-import-reorder (new). Re-verify actual open/closed/merged state via github MCP in the next Task 6 run rather than trusting this list.
- Network/build: `dotnet tool restore` + `dotnet paket restore --group Main` succeeded fine this run; targeted netstandard2.0 build of Paket.Core succeeded; net9 build of Paket.Core fails in this sandbox with NETSDK1005 (obj/project.assets.json missing net9 target) even after PaketDisableGlobalRestore restore — netstandard2.0 remains the reliable target framework to validate Paket.Core changes against. Test project restore+build+test on net9 works fine though (used successfully for ProjectFile test filter).

## Run 2026-08-26 21:xx UTC — 33014486355 (tasks: 1, 5, 3)

### Task 1 (Labelling)
Labelled 15 previously-unlabelled issues:
- #3081 → bug, needs investigation
- #3110 → bug, needs investigation
- #3160 → bug
- #3198 → bug, needs investigation
- #3220 → bug
- #3221 → performance, needs investigation
- #3222 → enhancement
- #3268 → enhancement
- #3272 → bug
- #3277 → enhancement
- #3281 → bug
- #3282 → bug
- #3285 → question
- #3287 → bug
- #3289 → bug

### Task 3 (Fix)
Fixed #3277: `CheckIfPackageExistsInAnyGroup` in `src/Paket.Core/PaketConfigFiles/LockFile.fs` returned an empty string when a package wasn't found in any lock file group, making the failure message unhelpful ("...but it was not found in the paket.lock file in group X." with nothing after the period). Changed the `None` branch to return an actionable hint: "This usually means paket.lock is out of sync with paket.dependencies/paket.references. Try running 'paket install' or 'paket update' to regenerate paket.lock."
- Added test `should give actionable error message when package is missing from lock file in any group` in `tests/Paket.Tests/Lockfile/QuerySpecs.fs`.
- Branch: `repo-assist/fix-issue-3277-lockfile-error-message`. Build succeeded (netstandard2.0), 5/5 QuerySpecs tests passed (4 pre-existing + 1 new).
- PR created (draft, title auto-prefixed "[repo-assist] Improve error message when package missing from paket.lock", labeled `bug`). Status: open, awaiting maintainer review.
- Commented on #3277 linking the fix.

### Task 5 (Coding Improvement)
Deduplicated the "package not found in paket.lock" `failwithf` error construction, which was copy-pasted verbatim across 3 call sites in `LockFile.fs` (`GetAllNormalizedDependenciesOf`, `GetAllDependenciesOf`, `GetDirectDependenciesOfSafe`). Extracted into a new member `FailPackageNotFoundInLockFile(package, context, groupName)`. Pure refactor, no behavior change.
- Branch: `repo-assist/improve-lockfile-error-dedup` (created fresh off `master`, NOT off the #3277 fix branch — kept independent so it can merge in any order).
- Build succeeded (netstandard2.0), 4/4 pre-existing QuerySpecs tests passed (no new test needed since behavior unchanged).
- PR created (draft, title auto-prefixed "[repo-assist] Reduce duplication in paket.lock 'package not found' error construction", labeled `refactor`). Status: open, awaiting maintainer review.

### Task 11 (Monthly Activity Summary)
Updated issue #4344 "[repo-assist] Monthly Activity 2026-08" (still current month, still open) — replaced body with fresh Suggested Actions (added the 2 new PRs from this run), Future Work, and prepended a new Run History entry for this run. No new maintainer comments/instructions found on the issue at time of update.

### Open repo-assist PRs tracked (not independently re-verified this run — a future Task 6 pass should confirm actual state)
- fix #3209 (Paket.Restore.targets import moved)
- fix #3129 (unquoted local NuGet source paths)
- git subprocess hang prevention (#3228/#3236)
- fix #3250 (skip dot folders in FindAllFiles)
- fix createRelativePath special chars
- fix #3195 (paket pack PackageId fallback)
- eng: bump Newtonsoft.Json/Mono.Cecil
- fix #3238 (restore cache load folder detection)
- fix #3277 (lockfile error message) — NEW this run
- refactor: dedupe lockfile error construction — NEW this run

### Notes for next run
- Backlog cursor for unlabelled issues: continue past #3289 (oldest-first, `no:label` search).
- Consider selecting Task 6 (Maintain Repo Assist PRs) soon to verify/consolidate the growing list of ~10 open repo-assist PRs above (some may already be merged/closed — memory could be stale).

## Run 2026-08-27 (approx) UTC — 33019514507 (tasks: 3, 1, 2)

### Task 1 (Labelling)
Labelled 6 previously-unlabelled issues:
- #3296 → enhancement, help wanted (GAC support request)
- #3297 → bug, needs investigation (credential provider error)
- #3312 → enhancement, performance (unsolvable conflict detection)
- #3313 → question (paket.lock differs by framework restriction)
- #3316 → enhancement, help wanted (paket.local git override)
- #3320 → enhancement, breaking change (paket init should pin itself — maintainer discussion favors making pinning default, described as breaking)

### Task 2 (Comment)
- Commented on #3272: shared root-cause analysis (nuspec glob matching multiple files) ahead of opening the PR.
- Commented on #3281 (credential username with backslash on Linux): explained likely shell-quoting cause, suggested single-quoting workaround, asked for exact error output to confirm whether it's Argu-related.

### Task 3 (Fix)
Fixed #3272 / #3160 (duplicate): `dotnet pack --include-symbols`/`--include-source` produces both a `.nuspec` and `.symbols.nuspec` file; `PaketOverrideNuspec`'s `_NuspecFiles` glob matched both, and `ConvertToAbsolutePath` joined them with `;` into `NuspecFileAbsolutePath`, breaking `PackTask` with a "syntax is incorrect" error.
- Fix: added `_PrimaryNuspecFile` item (excludes `*.symbols.nuspec`, falls back to unfiltered list if empty) in `src/Paket.Core/embedded/Paket.Restore.targets`; `NuspecFileAbsolutePath` now derived from it. `fix-nuspecs` still runs against the full `_NuspecFiles` list.
- Branch: `repo-assist/fix-issue-3272-nuspec-glob`. Build succeeded (netstandard2.0); verified via `strings` on built DLL that the updated targets content is embedded correctly. Could NOT run a live `dotnet pack --include-symbols` repro in sandbox — documented as a trade-off in the PR.
- PR created (draft, title auto-prefixed "[repo-assist] Fix dotnet pack failure with --include-symbols due to multiple nuspec files"). Status: open, awaiting maintainer review/manual verification.
- No separate issue comment needed beyond the analysis comment already posted (which now references the fix direction); PR itself links Closes #3272.

### Task 11 (Monthly Activity Summary)
To be updated this run — issue #4344 still current month (2026-08).

### Open repo-assist PRs tracked (STALE LIST — re-verify via Task 6 in a future run)
- fix #3209 (Paket.Restore.targets import moved)
- fix #3129 (unquoted local NuGet source paths)
- git subprocess hang prevention (#3228/#3236)
- fix #3250 (skip dot folders in FindAllFiles)
- fix createRelativePath special chars
- fix #3195 (paket pack PackageId fallback)
- eng: bump Newtonsoft.Json/Mono.Cecil
- fix #3238 (restore cache load folder detection)
- fix #3277 (lockfile error message)
- refactor: dedupe lockfile error construction
- fix #3272 (nuspec glob / semicolon path) — NEW this run

### Notes for next run
- Backlog cursor for unlabelled issues: continue past #3320 (oldest-first, `no:label` search).
- #3281 comment is awaiting a possible human reply with exact error output — check for new comments before re-engaging.
- Growing list of ~11 open repo-assist PRs — strongly consider Task 6 next available run to verify actual merge/close state, since memory may be stale (#3129/#3250/#3195 previously confirmed merged/closed by dsyme in an earlier run per notes above, but the full list needs re-verification).
- #3320: maintainer (forki/matthid, 2018) leaned toward making version-pinning default behavior in `paket init`, calling it a breaking change. Not implemented — could be a good Task 3/5 candidate in a future run if a maintainer confirms desired behavior, but requires threading `paketVersion` (currently computed in `src/Paket/Program.fs`, reflection-based, but also available internally via `NetUtils.paketVersion` in Paket.Core) through `Dependencies.Init` → `Environment.init`/`initWithContent` to prepend a `version X.Y.Z` line.

---

## Run 33023990997 (2026-08-27, tasks: 1, 2, 3, 11)

### Task 1 (Labelling)
Labelled 14 previously-unlabelled issues (fresh `no:label is:open` search, continuing past #3320 cursor):
#3321 (bug), #3322 (enhancement, help wanted), #3325 (enhancement), #3331 (bug), #3332 (bug),
#3343 (security, enhancement), #3345 (bug), #3346 (question), #3348 (bug), #3351 (bug),
#3353 (bug), #3358 (question), #3360 (bug), #3361 (enhancement).

### Task 2 (Comment)
- Commented on #3332 (`--filter` regex issue): confirmed behavior is by-design/documented (anchored `^...$` regex), cited `docs/content/commands/update.md` lines 30-32 and `Commands.fs` help text; suggested docs clarification, flagged for maintainer to confirm/close.
- Commented on #3351 (Artifactory v3 feed detection): confirmed via git blame/log that `PackageSources.fs` was already fixed by commits e479a53f5/0f2778e79/7b9e061e9 (detection now checks `Contains("/v3/") || EndsWith("index.json")`). Asked reporter to confirm on current version; candidate for closing.

### Task 3 (Fix)
Fixed #3193 (custom build targets not excludable via `exclude` in paket.references).
- Root cause: `InstallModel.filterExcludes` (src/Paket.Core/PaketConfigFiles/InstallModel.fs) only filtered `CompileLibFolders` (assembly refs) and framework references — never `TargetsFileFolders` (the `.targets`/`.props` files). So `exclude <Package>` stopped assembly references but not build-targets import.
- Fix: added a `mapTargetsFiles` filter step matching both `MsBuildFile.Name` and `.Path` against each exclude pattern, same substring-`Contains` semantics as the existing filters.
- Added unit test `should exclude targets files when filtering excludes` in `tests/Paket.Tests/InstallModel/ProcessingSpecs.fs`.
- Branch: `repo-assist/fix-issue-3193-exclude-targets`. Build succeeded (`dotnet build src/Paket.Core/Paket.Core.fsproj -f netstandard2.0`, 0 errors). Tests: `dotnet test ... --filter "FullyQualifiedName~InstallModel.ProcessingSpecs"` → 45/45 passed (net9).
- PR created (draft, title auto-prefixed "[repo-assist] Fix exclude directive not suppressing build targets/props import (#3193)"). Closes #3193.
- Posted linking comment on #3193.

### Task 11 (Monthly Activity Summary)
Updated issue #4344 (still August 2026, same month) — replaced body with new Suggested Actions (added #3193 PR, added #3332/#3351 "consider closing" items, marked #3202 checked-off per maintainer close), Future Work, and prepended new Run History entry for this run (33023990997).
No new maintainer comments/instructions found on #4344 body itself since last update (footer text unchanged aside from run-history additions).

### Open repo-assist PRs tracked (STILL STALE — re-verify via Task 6 next available run; list keeps growing, now ~12)
- fix #3209 (Paket.Restore.targets import moved)
- fix #3129 (unquoted local NuGet source paths)
- git subprocess hang prevention (#3228/#3236)
- fix #3250 (skip dot folders in FindAllFiles)
- fix createRelativePath special chars
- fix #3195 (paket pack PackageId fallback)
- eng: bump Newtonsoft.Json/Mono.Cecil
- fix #3238 (restore cache load folder detection)
- fix #3277 (lockfile error message)
- refactor: dedupe lockfile error construction
- fix #3272 (nuspec glob / semicolon path)
- fix #3193 (exclude directive / build targets) — NEW this run

### Notes for next run
- Backlog cursor for unlabelled issues: continue past #3361 (oldest-first, fresh `no:label` search each time — do not trust a raw number, always re-query).
- #3332 and #3351: both likely closeable/already-fixed; check for reporter/maintainer response before re-engaging.
- #3202 confirmed closed by dsyme (maintainer) — already reflected in monthly issue checkbox.
- The open repo-assist PR list is now ~12 items and has not had a Task 6 verification pass in several runs — this should be strongly prioritized next time Task 6 is selected or as a substitution target.
- #3320 (paket init pin version): still unimplemented, same status as prior notes — awaiting maintainer confirmation of desired behavior (breaking change per 2018 discussion).
- Possible follow-up to #3193 fix: check whether `import_targets: false` handling has an analogous gap for legacy (non-SDK) projects — not investigated this run since #3193's specific `exclude`-directive bug was already confirmed and scoped.
