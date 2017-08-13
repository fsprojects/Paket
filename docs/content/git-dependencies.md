# Git dependencies

Paket allows you to automatically manage the linking of files from any Git
repository.

> This feature assumes that you have [git](https://git-scm.com/) installed.
> If you don't have git installed then Paket still allows you to
> [reference files from GitHub](github-dependencies.html).

## Referencing a Git repository

You can reference a complete Git repository by specifying the clone URL in the
[`paket.dependencies` file](dependencies-file.html):

```paket
git https://github.com/fsprojects/Paket.git
git git@github.com:fsharp/FAKE.git
```

This will clone the repository, checkout the latest version of the default
branch and put it into your `paket-files` directory.

If you want to restrict Paket to a special branch, tag or a concrete commit then
this is also possible:

```paket
git https://github.com/fsprojects/Paket.git master
git https://github.com/forki/AskMe.git 97ee5ae7074bdb414a3e5dd7d2f2d752547d0542
git https://github.com/forki/AskMe.git 97ee5ae7074b // Short hash.
git https://github.com/forki/FsUnit.git 1.0         // Tag.
git file:///C:\Users\Steffen\AskMe master           // Local git repository.
```

### Referencing Git tags

Paket allows you to specify version ranges for Git tags similar to
[NuGet version ranges](nuget-dependencies.html#Version-constraints):

```paket
git https://github.com/fsprojects/Paket.git >= 1.0 // At least 1.0
git http://github.com/forki/AskMe.git < 3.0        // Lower than version 3.0
git http://github.com/forki/AskMe.git ~> 2.0       // 2.0 <= x < 3.0
git https://github.com/forki/FsUnit.git 1.0        // Exactly 1.0
git file:///C:\Users\Steffen\AskMe >= 1 alpha      // At least 1.0 including alpha versions
git file:///C:\Users\Steffen\AskMe >= 1 prerelease // At least 1.0 including prereleases
```

You can read more about the version range details in the corresponding
[NuGet reference section](nuget-dependencies.html#Version-constraints).

## Running a build in Git repositories

If your referenced Git repository contains a build script then Paket can execute
this script after restore:

```paket
git https://github.com/forki/nupkgtest.git master build: "build.cmd", OS: windows
git https://github.com/forki/nupkgtest.git master build: "build.sh", OS: mono
```

This allows you to execute arbitrary commands after restore.

NOTE: This functionality uses the .NET `Process` API, with `UseShellExecute` set
to `true`. This means that on Windows your command will execute in a `cmd.exe`
context. If your build is not a `.bat` file, you will need to fully qualify the
command with the shell program to run as well, like this:

```paket
git https://uri/to/repo.git master build: "powershell build.ps1", OS: windows
```

## Using Git repositories as NuGet source

If you have NuGet packages inside a git repository you can easily use the
repository as a NuGet source from the [`paket.dependencies`
file](dependencies-file.html):

```paket
git https://github.com/forki/nupkgtest.git master Packages: /source/

nuget Argu
```

The generated [`paket.lock` file](lock-file.html) will look like this:

```paket
NUGET
    remote: paket-files/github.com/forki/nupkgtest/source
    Argu (1.1.3)
GIT
    remote: https://github.com/forki/nupkgtest.git
        (05366e390e7552a569f3f328a0f3094249f3b93b)
```

It's also possible to
[run build scripts](git-dependencies.html#Running-a-build-in-git-repositories)
to create the NuGet packages:

```paket
git https://github.com/forki/nupkgtest.git master build: "build.cmd", Packages: /source/, OS: windows
git https://github.com/forki/nupkgtest.git master build: "build.sh", Packages: /source/, OS: mono

nuget Argu
```

In this sample we have different build scripts for Mono and Windows.
