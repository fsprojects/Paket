Simplify will also affect paket.references files, unless [strict](dependencies-file.html#Strict-references) mode is used.

## Sample

When you install `Castle.Windsor` package in NuGet to a project, it will generate a following `packages.config` file in the project location:

    [lang=xml]
    <?xml version="1.0" encoding="utf-8"?>
    <packages>
      <package id="Castle.Core" version="3.3.1" targetFramework="net451" />
      <package id="Castle.Windsor" version="3.3.0" targetFramework="net451" />
    </packages>

After converting to Paket with [`paket convert-from-nuget command`](paket-convert-from-nuget.html), you should get a following paket.dependencies file:

    source https://nuget.org/api/v2
    
    nuget Castle.Core 3.3.1
    nuget Castle.Windsor 3.3.0

and the NuGet `packages.config` should be converted to following paket.references file:

    Castle.Core
    Castle.Windsor

As you have already probably guessed, the `Castle.Windsor` package happens to have a dependency on the `Castle.Core` package.
Paket by default (without [strict](dependencies-file.html#Strict-references) mode) adds references to all required dependencies of a package that you define for a specific project in paket.references file.
In other words, you still get the same result if you remove `Castle.Core` from your paket.references file.
And this is exactly what happens after executing `paket simplify` command:

    source https://nuget.org/api/v2
    
    nuget Castle.Windsor 3.3.0

will be the content of your paket.dependencies file, and:

    Castle.Windsor

will be the content of your paket.references file.

Unless you are relying heavily on components from `Castle.Core`, you would not care about controlling the required version of `Castle.Core` package. Paket will do the job.

The simplify command will help you maintain your direct dependencies.

## Interactive mode

Sometimes, you may still want to have control over some of the transitive dependencies. In this case you can use the `--interactive` flag,
which will ask you to confirm before deleting a dependency from a file.