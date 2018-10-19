# The .paket directory

The [`.paket` directory](https://github.com/fsprojects/Paket/tree/master/.paket) is
used the same way a `.nuget` directory is used for the
[NuGet package restore](http://docs.nuget.org/docs/workflows/using-nuget-without-committing-packages).

Place this directory into the root of your repository. It should include the
`paket.targets` and [`paket.bootstrapper.exe`](bootstrapper.html) files which
can be downloaded from
[GitHub](https://github.com/fsprojects/Paket/releases/latest). The
[bootstrapper](bootstrapper.html) will always download the latest version of the
`paket.exe` file and it will be placed into the same directory.

Now, to install all the packages from the
[`paket.dependencies` file](dependencies-file.html), just run the following
command.

```sh
.paket/paket.exe install
```

The location of `.paket` directory and Paket related files is not bound to
location of Visual Studio solution file. Paket does not read or look for any
solution files. If you have multiple solutions in subdirectories of some root
directory, then that root directory is a good place to create `.paket` directory
and put the [`paket.dependencies` file](dependencies-file.html).

The [`.paket/paket.exe install` command](paket-install.html) processes all
directories under the root recursively and touch only those projects which have
a respective [`paket.references` files](references-files.html). When Paket
encounters [`paket.dependencies` files](dependencies-file.html) in
subdirectories it ignores that subdirectory (and everything under it) entirely,
implying that they use an independent [`paket.lock` file](lock-file.html) and
`packages` directory. The `packages` directory will be created at the root level
for all projects under it.
