# Git dependencies

[This feature is only available in Paket 3.0 prereleases]

Paket allows you to automatically manage the linking of files from any git repo.


This feature assumes that you have [git](https://git-scm.com/) installed.
If you don't have git installed then Paket still allows you to [reference files from github](github-dependencies.html).

## Referencing a Git repository

You can also reference a complete git repository by specifying the clone url in the [`paket.dependencies` file](dependencies-file.html):

    git https://github.com/fsprojects/Paket.git
    git git@github.com:fsharp/FAKE.git

This will download the latest version of the default branch of given repositories and put it into your `paket-files` folder.

If you want to restrict Paket to a special branch, tag or a concrete commit then this is also possible:

    git https://github.com/fsprojects/Paket.git master
    git http://github.com/forki/AskMe.git 97ee5ae7074bdb414a3e5dd7d2f2d752547d0542
    git http://github.com/forki/AskMe.git 97ee5ae7074b // short hash
    git https://github.com/forki/FsUnit.git 1.0        // tag

## Running a build in git repositories

If your referenced git repository contains a build script then Paket can excute this scipt after restore:

    git https://github.com/forki/nupkgtest.git build build:"build.cmd"

This allows you to excute arbitray commands after restore.

## Using Git repositories as NuGet source

If you have NuGet packages inside a git repository you can easily use the repository as a NuGet source from the [`paket.dependencies` file](dependencies-file.html):

    git https://github.com/forki/nupkgtest.git master Packages: /source/
 
    nuget Argu

The generated [`paket.lock` file](lock-file.html) will look like this:

    NUGET
      remote: paket-files/github.com/nupkgtest/source
      specs:
        Argu (1.1.3)
    GIT
      remote: https://github.com/forki/nupkgtest.git
      specs:
         (05366e390e7552a569f3f328a0f3094249f3b93b)

It's also possible to [run build scripts](git-dependencies.html#Running-a-build-in-git-repositories) to create the NuGet packages:

    git https://github.com/forki/nupkgtest.git build build:"build.cmd", Packages: /source/
    
    nuget Argu