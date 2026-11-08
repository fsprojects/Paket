When enabling auto-restore, Paket will

- create a `.paket` directory in your root directory,
- write `paket.targets` into the `.paket` directory,
- add an `<Import>` statement for `paket.targets` to the projects that don't use
  the .NET SDK.

`paket.targets` runs `paket restore` before the build of those projects. It finds
Paket installed with `--tool-path` in `.paket` or in the root directory, as a
local tool, or on the `PATH` like a global tool. The projects that use the .NET
SDK restore through `Paket.Restore.targets` instead, which
[`paket install`](paket-install.html) adds to them.

`paket install`, `paket restore` without `--project`, `--references-file` or
`--only-referenced`, and `update`, `add` and `remove` when they install (not with
`--no-install`, nor `add --no-resolve`) rewrite an existing `.paket/paket.targets`,
so that it matches the running Paket, unless `PAKET_SKIP_RESTORE_TARGETS` is
`true`. The restore of one project, which `paket.targets` runs during a build,
doesn't. In a solution that also has projects with the .NET SDK, the full restore
that `Paket.Restore.targets` runs during the build, when the last full restore
isn't of the current `paket.lock`, rewrites it as it rewrites
`Paket.Restore.targets`.

When disabling auto-restore, Paket will

- remove `paket.targets` from the `.paket` directory,
- remove the `<Import>` statement for `paket.targets` from projects that have a
  [`paket.references` file](references-files.html).
