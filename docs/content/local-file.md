# The paket.local file

The `paket.local` file is used for testing changes before releasing new version
of a dependent project.

> Notes: 
>
> * **!!!** This feature works only for old-style .NET projects. If will not work with .NET SDK based projects. Details [here](https://github.com/fsprojects/Paket/issues/3116).
> * You **should not commit** `paket.local`. This file should always be added to `.gitignore`, because it specifies file paths that are only accessible on your machine.
> * `paket.local` affects only the paket **restore** command, not the install or update command.
> * By default `paket.local` will only substitute packages with an **identical** version to the `paket.lock` file.  See [Nupkg version](#Nupkg-version) for alternatives.

## Example use case

In this tutorial you will learn how `paket.local` can help you to speed up your
development process.

The Paket project uses [Argu](http://fsprojects.github.io/Argu/) as one of its
own dependencies. Argu is a library that allows to manage command line
parameters:

`paket.dependencies`:

```paket
source https://nuget.org/api/v2

nuget Argu
```

When running
[`paket restore`](paket-restore.html), the Argu package is always restored from
the NuGet source that was specified in Paket's own
[`paket.lock` file](lock-file.html):

```paket
NUGET
  remote: https://www.nuget.org/api/v2
  specs:
    Argu (2.1)
```

Let's assume that we want to contribute to Paket by adding a new feature. The
new feature, however, requires a change to Argu, so we'll need to contribute to
the Argu project first.

Before we create a Pull Request to Argu, we might want to test the change
locally by running Paket with the applied change in the Argu package. Up until
now, there was no convenient mechanism to do so, and e.g. we would manually copy
updated Argu libraries to the packages directory in Paket's project after each
rebuild.

With the `paket.local` file, we can improve this process, by **temporarily
overriding** the source for a specific package. We create a `paket.local` file
in the root of own Paket source code copy:

```paket
nuget Argu -> git file:///c:\github\Argu feature_branch build:"build.cmd NuGet", Packages: /bin/
```

The line can be translated to:

> Whenever [`paket restore`](paket-restore.html) is run, **override** the source
> of the Argu package with the source given after `->`.

Now when running [`paket restore`](paket-restore.html), we get the following:

```sh
$ .paket\paket.exe restore
Paket version 3.0.0.0
paket.local override: nuget Argu group main ->
    file:///c:\github\Argu feature_branch build:"build.cmd NuGet", Packages: /bin/
Setting C:\github\Paket\paket-files\localfilesystem\Argu to b14ea1a00431335ca3b60d49573b3831cd2deeb4
Running "C:\github\Paket\paket-files\localfilesystem\Argu\build.cmd NuGet"
11 seconds - ready.
```

The result is:

* The build command `build.cmd NuGet` is triggered for Argu on `feature_branch`.
* A new Argu NuGet package is built by this command in the `/bin/` directory.
* The Argu NuGet package is extracted to `packages/Argu` directory in the Paket
  project directory.
* Paket can be run and tested with the updated Argu library.

**Note:** Any override specified in `paket.local` will result in a **warning**
upon running `restore` as in the above example. This is to emphasize that we
have a local override, and the build might not be reproducible on any other
machine.

## Format

Each line in `paket.local` means one override and you can have multiple
overrides at once:

```paket
nuget Argu -> git file:///c:\github\Argu feature_branch build:"build.cmd NuGet", Packages: /bin/
nuget Fake -> source c:\github\FAKE\bin
```

### Git override

```paket
nuget Argu -> git file:///c:\github\Argu feature_branch build:"build.cmd NuGet", Packages: /bin/
```

The format of a `git` source is the same as used in `paket.dependencies` for
specifying
[git repositories](git-dependencies.html#Using-Git-repositories-as-NuGet-source).

**Note:** Only **git repositories as NuGet source** (with `Packages: ...`) are
currently supported.

### Source override

```paket
nuget Fake -> source c:\github\FAKE\bin

// Argu is searched in specific version.
nuget Argu -> source C:\github\Argu\bin version 0.0.0
```

The format of the source is the same as in
[path sources](nuget-dependencies.html#Path-sources).

**Note:** In the case of source overrides, `paket restore` assumes that the
NuGet package **already exists** in the directory pointed to, no build will be
triggered.

### Nupkg version

If you happen to have a `.nupkg` file in a local path, but with a different
version than in `paket.lock`, you can optionally specify the version which
should be used for this override. One use case might be when the package version
is determined by your build server, and `.nupkg`s created locally have zero
version. This is currently supported only for [source
override](#Source-override).

Another use case is testing an upgraded pre-release package with an incremented 
version, against a consuming project.  In this case you want paket in the consuming 
project to fetch a higher version than the paket.local file indicates.

Example `paket.local` file with version override

```paket
    // Argu is searched in specific version, grab a specific version
    nuget Argu -> source C:\github\Argu\bin version 1.0.0
```

>Notes
>
>* `version` is currently supported only for [Source override](#Source-override).
>* The syntax allows only a specific version, **not** [version constraints](nuget-dependencies.html#Version-constraints) like >= .



### Groups

```paket
// Argu is in Main group
nuget Argu -> git file:///c:\github\Argu feature_branch build:"build.cmd NuGet", Packages: /bin/
// Fake is in Build group
nuget Fake group Build -> source c:\github\FAKE\bin
```

The [dependency group](groups.html) can be specified with `group <group name>`
after `nuget <package ID>`. If not specified, the default group `Main` is used.

## Comments

All lines starting with with `//` or `#` are considered comments.

```paket
// This line is treated as comment.
nuget Fake -> source c:\github\FAKE\bin
```

Comments might prove helpful if you use `paket.local` overrides on a regular
basis instead of typing the override by hand, you can just comment/uncomment
relevant line:

```paket
// Uncomment below to override FAKE package.
// nuget FAKE -> source C:\github\FAKE\bin
```
