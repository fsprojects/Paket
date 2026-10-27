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

When disabling auto-restore, Paket will

- remove `paket.targets` from the `.paket` directory,
- remove the `<Import>` statement for `paket.targets` from projects that have a
  [`paket.references` file](references-files.html).
