When enabling auto-restore, Paket will

- create a `.paket` directory in your root directory,
- write `paket.targets` into the `.paket` directory,
- add an `<Import>` statement for `paket.targets` to the projects that don't use
  the .NET SDK and have a [`paket.references` file](references-files.html).

`paket.targets` runs `paket restore` before the build of those projects. It finds
Paket as a local tool when the tool manifest declares it, then installed with
`--tool-path` in the root directory or in `.paket`, or on the `PATH` like a
global tool, as [`Paket.Restore.targets`](installation.html#Global-tool) does.
The projects that use the .NET SDK restore through `Paket.Restore.targets`
instead, which [`paket install`](paket-install.html) adds to them.

With MSBuild 15.8 or later, `paket.targets` skips that restore when
`paket-files/paket.restore.cached` says that the last full restore was of the
current `paket.lock`. `paket install` writes that file, as do `update`, `add` and
`remove` when they install, and `paket restore` without `--group`,
`--target-framework`, `--project`, `--references-file`, `--only-referenced` or a
[`paket.local` file](local-file.html).

`paket.targets` still restores while `paket.local` exists, and when the
`packages` folder is missing, unless `paket.lock` has `storage: none` for all its
groups, the main one included, and packages. Once it has restored a project for another `paket.lock`, as
after a pull or a switch of branch, or for a missing `packages` folder, the
builds restore until the next full restore, as they do once `paket.local` is
gone. In a solution that also has projects with the .NET SDK, their build runs
that full restore. In a solution without them, run `paket restore` after
`paket.lock` changes, or once you delete the `packages` folder or `paket.local`,
so that the builds skip the restore again.

`paket install`, `paket restore` without `--project`, `--references-file` or
`--only-referenced`, and `update`, `add` and `remove` when they install (not with
`--no-install`, nor `add --no-resolve`) rewrite an existing `.paket/paket.targets`,
so that it matches the running Paket, unless `PAKET_SKIP_RESTORE_TARGETS` is
`true`. The restore of one project, which `paket.targets` runs during a build,
doesn't. In a solution that also has projects with the .NET SDK, the full restore
that `Paket.Restore.targets` runs during the build, when `paket.restore.cached`
doesn't say that the projects were last restored for the current `paket.lock`,
rewrites it as it rewrites
`Paket.Restore.targets`.

When disabling auto-restore, Paket will

- remove `paket.targets` from the `.paket` directory,
- remove the `<Import>` statement for `paket.targets` from projects that have a
  [`paket.references` file](references-files.html).
