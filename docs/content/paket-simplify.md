# paket simplify

Simplifies your [`paket.dependencies` file](dependencies-file.html) by removing indirect dependencies.
Does also simplify [`paket.references` files](references-files.html), unless [strict](dependencies-file.html#Strict-references) mode is used.

    [lang=batchfile]
    $ paket simplify [-v] [--interactive] [--dependencies-file FILE]

Options:

  `-v`: Verbose - output the difference in content before and after running simplify command.

  `--interactive`: Asks to confirm to delete every indirect dependency from each of the files. See [Interactive Mode](paket-simplify.html#Interactive-mode).
  
  `--dependencies-file`: Use the specified file instead of [`paket.dependencies`](dependencies-file.html).

## Sample

When you install `Castle.Windsor` package in NuGet to a project, it will generate a following `packages.config` file in the project location:

    [lang=xml]
    <?xml version="1.0" encoding="utf-8"?>
    <packages>
      <package id="Castle.Core" version="3.3.1" targetFramework="net451" />
      <package id="Castle.Windsor" version="3.3.0" targetFramework="net451" />
    </packages>

After converting to Paket with [`paket convert-from-nuget command`](convert-from-nuget.html), you should get a following [`paket.dependencies` file](dependencies-file.html):

    source http://nuget.org/api/v2
    
    nuget Castle.Core 3.3.1
    nuget Castle.Windsor 3.3.0

and the NuGet `packages.config` should be converted to following [`paket.references` file](references-files.html) :

    Castle.Core
    Castle.Windsor

As you have already probably guessed, the `Castle.Windsor` package happens to have a dependency on the `Castle.Core` package.
Paket by default (without [strict](dependencies-file.html#Strict-references) mode) adds references to all required dependencies of a package that you define for a specific project in [`paket.references` file](references-files.html).
In other words, you still get the same result if you remove `Castle.Core` from your [`paket.references` file](references-files.html).
And this is exactly what happens after executing `paket simplify` command:

    source http://nuget.org/api/v2
    
    nuget Castle.Windsor 3.3.0

will be the content of your [`paket.dependencies` file](dependencies-file.html), and:

    Castle.Windsor

will be the content of your [`paket.references` file](references-files.html).

Unless you are relying heavily on components from `Castle.Core`, you would not care about controlling the required version of `Castle.Core` package. Paket will do the job.

The simplify command will help you maintain your direct dependencies.

## Interactive mode

Sometimes, you may still want to have control over some of the indirect dependencies. In this case you can use the `--interactive` flag,
which will ask you to confirm before deleting a dependency from a file.
