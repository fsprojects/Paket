# Git dependencies

Paket allows you to automatically manage the linking of files from any git repo.

<blockquote>This feature is only available in Paket 3.0 prereleases.</blockquote>

<blockquote>This feature assumes that you have <a href="https://git-scm.com/">git</a> installed.
If you don't have git installed then Paket still allows you to <a href="github-dependencies.html">reference files from github</a>.</blockquote>

## Referencing a Git repository

You can also reference a complete git repository by specifying the clone url in the [`paket.dependencies` file](dependencies-file.html):

    [lang=paket]
    git https://github.com/fsprojects/Paket.git
    git git@github.com:fsharp/FAKE.git

This will clone the repository, checkout the latest version of the default branch and put it into your `paket-files` folder.

If you want to restrict Paket to a special branch, tag or a concrete commit then this is also possible:

    [lang=paket]
    git https://github.com/fsprojects/Paket.git master
    git http://github.com/forki/AskMe.git 97ee5ae7074bdb414a3e5dd7d2f2d752547d0542
    git http://github.com/forki/AskMe.git 97ee5ae7074b // short hash
    git https://github.com/forki/FsUnit.git 1.0        // tag
    git file:///C:\Users\Steffen\AskMe master          // local git repo

### Referencing Git tags

Paket allows you to specify version ranges for git tags similar to [NuGet version ranges](nuget-dependencies.html#Version-constraints):

    [lang=paket]
    git https://github.com/fsprojects/Paket.git >= 1.0 // at least 1.0
    git http://github.com/forki/AskMe.git < 3.0        // lower than version 3.0
    git http://github.com/forki/AskMe.git ~> 2.0       // 2.0 <= x < 3.0
    git https://github.com/forki/FsUnit.git 1.0        // exactly 1.0
    git file:///C:\Users\Steffen\AskMe >= 1 alpha      // at least 1.0 including alpha versions
    git file:///C:\Users\Steffen\AskMe >= 1 prerelease // at least 1.0 including prereleases

You can read more about the version range details in the corresponding [NuGet reference section](nuget-dependencies.html#Version-constraints).

## Running a build in git repositories

If your referenced git repository contains a build script then Paket can execute this scipt after restore:

    [lang=paket]
    git https://github.com/forki/nupkgtest.git master build: "build.cmd", OS: windows
    git https://github.com/forki/nupkgtest.git master build: "build.sh", OS: mono

This allows you to excute arbitray commands after restore.

## Using Git repositories as NuGet source

If you have NuGet packages inside a git repository you can easily use the repository as a NuGet source from the [`paket.dependencies` file](dependencies-file.html):

    [lang=paket]
    git https://github.com/forki/nupkgtest.git master Packages: /source/

    nuget Argu

The generated [`paket.lock` file](lock-file.html) will look like this:

    [lang=paket]
    NUGET
      remote: paket-files/github.com/forki/nupkgtest/source
      specs:
        Argu (1.1.3)
    GIT
      remote: https://github.com/forki/nupkgtest.git
      specs:
         (05366e390e7552a569f3f328a0f3094249f3b93b)

It's also possible to [run build scripts](git-dependencies.html#Running-a-build-in-git-repositories) to create the NuGet packages:

    [lang=paket]
    git https://github.com/forki/nupkgtest.git master build: "build.cmd", Packages: /source/, OS: windows
    git https://github.com/forki/nupkgtest.git master build: "build.sh", Packages: /source/, OS: mono

    nuget Argu

In this sample we have different build scripts for mono and windows.
