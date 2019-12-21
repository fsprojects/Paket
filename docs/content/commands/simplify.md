## A note on `strict` mode

`paket simplify` will also affect
[`paket.references` files](references-files.html), unless
[`strict` mode](dependencies-file.html#Strict-references) is used.

**Important:** `paket simplify` is a heuristic approach to dependency
simplification. It often works very well, but there are rare cases where
simplify can result in changes of the package resolution.

## Interactive mode

Sometimes, you may still want to have control over some of the
[transitive dependencies](faq.html#transitive). In this case you can use the
`--interactive` flag, which will ask you to confirm before deleting a dependency
from a file.

## Preventing Simplify

You can use the setting `simplify: never` to prevent a package from being removed as part of the simplify.
Any package with settings will also never be removed.

## Example

When you install `Castle.Windsor` package in NuGet to a project, it will
generate a following `packages.config` file in the project location:

```xml
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <package id="Castle.Core" version="3.3.1" targetFramework="net451" />
  <package id="Castle.Windsor" version="3.3.0" targetFramework="net451" />
</packages>
```

After converting to Paket with
[`paket convert-from-nuget`](paket-convert-from-nuget.html), you should get a
following [`paket.dependencies` file](dependencies-file.html):

```paket
source https://nuget.org/api/v2

nuget Castle.Core 3.3.1
nuget Castle.Windsor 3.3.0
```

The NuGet `packages.config` should be converted to following
[`paket.references` file](references-files.html):

```text
Castle.Core
Castle.Windsor
```

As you have already probably guessed, the `Castle.Windsor` package happens to
have a dependency on the `Castle.Core` package. Paket will by default (without
[`strict`](dependencies-file.html#Strict-references) mode) add references to all
required dependencies of a package that you define for a specific project in
[`paket.references` file](references-files.html). In other words, you still get
the same result if you remove `Castle.Core` from your
[`paket.references` file](references-files.html).

This is exactly what happens after executing `paket simplify` command. After
running it, [`paket.dependencies`](dependencies-file.html) will contain:

```paket
source https://nuget.org/api/v2

nuget Castle.Windsor 3.3.0
```

And [`paket.references` file](references-files.html) contains:

```text
Castle.Windsor
```

Unless you are relying heavily on types from `Castle.Core`, you would not care
about controlling the required version of `Castle.Core` package. Paket will do
the job.

The simplify command will help you maintain your direct dependencies.
