# Learn how to use paket

Once you've [installed Paket](get-started.html), it's time to learn how to use it!

You can refer to a [minimal sample codebase](https://github.com/cartermp/MinimalPaketAndFakeSample) that shows how Paket works in a codebase.

## Core concepts

Paket manages your dependencies with three core file types:

* [`paket.dependencies`](dependencies.file.html), where you specify your dependencies and their versions for your entire codebase.
* [`paket.references`](references-files.html), a file that specifies a subset of your dependencies for every project in a solution.
* [`paket.lock`](lock-file.html), a lock file that Paket generates when it runs. When you check it into source control, you get reproducible builds.

You edit the `paket.dependencies` and `paket.references` files by hand as needed. When you run a paket command, it will generate the `paket.lock` file.

All three file types must be committed to source control.

## Important paket commands

The most frequently used Paket commands are:

* [`paket install`](paket-install.html) - Run this after adding or removing packages from the `paket.dependencies` file. It will update any affected parts of the lock file that were affected by the changes in the `paket.dependencies` file, and then refresh all projects in your codebase that specify paket dependencies to import references.

* [`paket update`](paket-update.html) - Run this to update your codebase to the latest versions of *all* dependent packages. It will update the `paket.lock` file to reference the most recent versions permitted by the restrictions in `paket.dependencies`, then apply these changes to all projects in your codebase.

* [`paket restore`](paket-restore.html) - Run this after cloning the repository or switching branches. It will take the current `paket.lock` file and update all projects in your codebase so that they are referencing the correct versions of NuGet packages. It should be called by your build script in your codebase, so you should not need to run it manually.

Refer to the [minimal sample codebase](https://github.com/cartermp/MinimalPaketAndFakeSample) that shows how these commands are used in a codebase.

There is a reference to all paket commands in the table of contents on the right-hand side of this page.

## Walkthrough

Create a [`paket.dependencies` file](dependencies-file.html) in your solution's root with the .NET CLI:

```sh
dotnet paket init
```

You can also create it by hand.

Make the dependencies file look like this to continue:

```paket
source https://api.nuget.org/v3/index.json

nuget Newtonsoft.Json
nuget Colorful.Console
```

### Installing dependencies

For every project in your codebase, create a `paket.references` file that specifies the dependencies you want to pull in for that project.

In the [minimal sample codebase](https://github.com/cartermp/MinimalPaketAndFakeSample), the library project has `Newtonsoft.Json` and the console project has `Colorful.Console`.

Once you have a `paket.references` file alongside every project in your codebase, install all dependencies with this command:

```sh
dotnet paket install
```

Or if you're not using .NET Core,

```sh
.paket/paket.exe install
```

The [`paket install` command](paket-install.html) will analyze your dependencies and automatically generate the [`paket.lock` file](lock-file.html). It's often quite large!

This file shows all direct and [transitive dependencies](faq.html#transitive) and pins every dependency to a concrete version. You'll want to commit this file to your version control system ([read why](faq.html#Why-should-I-commit-the-lock-file)).

### Updating packages

If you want to check if your dependencies have updates you can run the [`paket outdated` command](paket-outdated.html):

```sh
dotnet paket outdated
```

Or if you're not using .NET Core:

```sh
.paket/paket.exe outdated
```

If you want to update all packages you can use the [`paket update` command](paket-update.html):

```sh
dotnet paket update
```

Or if you're not using .NET Core:

```sh
.paket/paket.exe update
```

This command will analyze your [`paket.dependencies` file](dependencies-file.html) and update the [`paket.lock` file](lock-file.html) to specify all of the updated dependencies.
