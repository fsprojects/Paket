# The .paket directory

Paket writes the `.paket` directory into the root of your repository, next to the
[`paket.dependencies` file](dependencies-file.html). It holds:

* `Paket.Restore.targets`, which [`paket install`](paket-install.html) adds to
  the projects that use the .NET SDK, so that they restore their packages.
* `paket.targets`, which [`paket auto-restore on`](paket-auto-restore.html) adds
  to the projects that don't use the .NET SDK, for the same purpose.
* The `load` scripts of
  [`paket generate-load-scripts`](paket-generate-load-scripts.html).
* Paket itself, when you install the .NET tool with
  `dotnet tool install paket --tool-path .paket`. A
  [local tool](installation.html#Local-tool) is usually the better choice.

To install all the packages from the
[`paket.dependencies` file](dependencies-file.html), just run the following
command.

```sh
dotnet paket install
```

The location of `.paket` directory and Paket related files is not bound to
location of Visual Studio solution file. Paket does not read or look for any
solution files. If you have multiple solutions in subdirectories of some root
directory, then that root directory is a good place to create `.paket` directory
and put the [`paket.dependencies` file](dependencies-file.html).

The [`paket install` command](paket-install.html) processes all
directories under the root recursively and touch only those projects which have
a respective [`paket.references` files](references-files.html). When Paket
encounters [`paket.dependencies` files](dependencies-file.html) in
subdirectories it ignores that subdirectory (and everything under it) entirely,
implying that they use an independent [`paket.lock` file](lock-file.html) and
`packages` directory. The `packages` directory will be created at the root level
for all projects under it.
