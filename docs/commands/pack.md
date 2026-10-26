## Creating NuGet packages

Consider the following [`paket.dependencies` file](dependencies-file.html) file:

```paket
source https://nuget.org/api/v2

nuget Castle.Windsor ~> 3.2
nuget NUnit
```

One of your projects has a [`paket.references` file][reffile]:

```paket
Castle.Windsor
```

When you run [`paket install`](paket-install.html), your
[`paket.lock` file][lockfile] will look like this:

```paket
NUGET
  remote: https://nuget.org/api/v2
    Castle.Core (3.3.3)
    Castle.Windsor (3.3.0)
      Castle.Core (>= 3.3.0)
    NUnit (2.6.4)
```

When you are done programming and wish to create a NuGet package of your
project, create a [`paket.template` file][templatefile] with `type project` and
run:

```sh
paket pack nugets --version 1.0.0
```

You could also run:

```sh
paket pack nugets --version 1.0.0 --lock-dependencies
```

Depending on which command you issue, Paket creates different version
requirements of the packages you depend on in the resulting `.nuspec` file of
your package:

| Dependency       | Default     | With locked dependencies |
| :--------------- | :---------- | :----------------------- |
| `Castle.Windsor` | `[3.2,4.0)` | `[3.3.0]`                |

The first command (without the `--lock-dependencies` parameter) creates the
version requirements as specified in your [`paket.dependencies` file][depfile].
The second command takes the currently resolved versions from your
[`paket.lock` file][lockfile] and "locks" them to these specific versions.

### Symbol packages

Visual Studio can be configured to download symbol/source versions of installed
packages from a symbol server, allowing the developer to use the debugger to
step into the source (see
[SymbolSource](http://www.symbolsource.org/Public/Home/VisualStudio)). These
symbol/source packages are the same as the regular packages, but contain the
source files (under `src`) and PDBs alongside the DLLs. Paket can build these
symbol/source packages, in addition to the regular packages, using the `symbols`
parameter:

```sh
paket pack nugets --symbols
```

### Including referenced projects

Paket automatically replaces inter-project dependencies with NuGet dependencies
if the dependency has its own [`paket.template`][templatefile]. Version constraints 
for these dependencies can be controlled with the `--interproject-references` 
parameter or the `interproject-references` option in [`paket.template`][templatefile].  

In addition to this the parameter `--include-referenced-projects` instructs Paket to 
add project output to the package for inter-project dependencies that don't have a
[`paket.template` file][templatefile].

1. It recursively iterates referenced projects and adds their project output to
   the package (as long as the working directory contains the other projects).
1. When combined with the [symbols switch](#Symbol-Packages), it
   will also include the source code of the referenced projects.  Also
   recursively.
1. Any projects that are encountered in this search that have their own project
   template are ignored.

### Version ranges

By default Paket uses the specified version ranges from the
[`paket.dependencies` file][depfile] as version ranges for dependencies of the
new NuGet package. The `--minimum-from-lock-file` parameter instructs Paket to
use the version from the [`paket.lock` file][lockfile] and use it as the minimum
version.

### Localized packages

When using a [`paket.template` file][templatefile] with `type project` any
localized satellite assemblies are included in the package created.

The following layout is used:

```text
.
└── lib
    └── net45
        ├── se
        │   └── Foo.resources.dll
        └── Foo.dll
```

  [lockfile]: lock-file.html
  [depfile]: dependencies-file.html
  [reffile]: references-files.html
  [templatefile]: template-files.html
