When enabling auto-restore, Paket will

- create a `.paket` directory in your root directory,
- download `paket.targets` and [`paket.bootstrapper.exe`](bootstrapper.html)
  into the `.paket` directory,
- add an `<Import>` statement for `paket.targets` to projects that have a
  [`paket.references` file](references-files.html).

When disabling auto-restore, Paket will

- remove `paket.targets` from the `.paket` directory,
- remove the `<Import>` statement for `paket.targets` from projects that have a
  [`paket.references` file](references-files.html).
