The paket.local file
===================

The `paket.local` file is used for testing changes before releasing new version of a dependent project.

> Note: you **shouldn't commit** `paket.local`. This file should always be added to `.gitignore`, because it specifies paths accessible only on your machine.

Use case
--------

Among its dependencies, Paket uses [Argu](http://fsprojects.github.io/Argu/) - a utility to manage command line parameters:

`paket.dependencies` ([browse](https://github.com/fsprojects/Paket/blob/master/paket.dependencies))

    [lang=paket]
    source https://nuget.org/api/v2
    
    nuget Argu
    ...

When running [`paket restore`](file:///C:/github/Paket/docs/output/paket-restore.html), Argu package is always restored from the NuGet source that it was installed:

`paket.lock` ([browse](https://github.com/fsprojects/Paket/blob/master/paket.lock))

    [lang=paket]
    NUGET
      remote: https://www.nuget.org/api/v2
      specs:
        Argu (2.1)
    ...

Now let's assume that we want to contribute to Paket by adding a new feature.
The new feature however requires a change to Argu, so we'll need to contribute to Argu project first.

Before we create a Pull Request to Argu, we might want to test the change locally by running Paket with the applied change in Argu package.  
Up until now, there was no convenient mechanism to do so, and e.g. we would manually copy updated Argu libraries to packages folder in Paket project after each rebuild.

With `paket.local` file, we can improve this process, by **temporarily overriding** source for a specific package: 

`paket.local`

    [lang=paket]
    nuget Argu -> git file:///c:\github\Argu feature_branch build:"build.cmd NuGet", Packages: /bin/

The above line basically says: whenever `paket restore` is run, **override** source of Argu package with the source given after `->`.

Now when running `paket restore`, we get the following:

    [lang=bash]
    $ .paket\paket.exe restore
    Paket version 3.0.0.0
    paket.local override: nuget Argu -> file:///c:\github\Argu feature_branch build:"build.cmd NuGet", Packages: /bin/
    Setting C:\github\Paket\paket-files\localfilesystem\Argu to b14ea1a00431335ca3b60d49573b3831cd2deeb4
    Running "C:\github\Paket\paket-files\localfilesystem\Argu\build.cmd NuGet"
    11 seconds - ready.

As a result:

* build command `build.cmd NuGet` is triggered for Argu on `feature_branch` 
* Argu NuGet package is built by this command in `/bin/` directory
* the Argu NuGet package is extracted to `packages/Argu` directory in Paket project
* Paket can be run and tested with the updated Argu library

> Note: Any override specified in `paket.local` will result in a **warning** upon running `restore` as in the above example. 
This is to emphasize that we have a local override, and the build might not be reproducible on any other machine.

Format
------

Each line in `paket.local` means one override - you can have multiple overrides at once:

    [lang=paket] 
    nuget Argu -> git file:///c:\github\Argu feature_branch build:"build.cmd NuGet", Packages: /bin/
    nuget Fake -> source c:\github\FAKE\bin
    

> Note: [groups](groups.html) are not supported yet. Override applies only to "Main" group.

### Git override

    [lang=paket] 
    nuget Argu -> git file:///c:\github\Argu feature_branch build:"build.cmd NuGet", Packages: /bin/

Format of git source is the same as used in `paket.dependencies` for specifying [git repositories](git-dependencies.html#Using-Git-repositories-as-NuGet-source).

> Note: only **git repositories as NuGet source** (with `Packages: ...`) are currently supported

### Source override

    [lang=paket] 
    nuget Fake -> source c:\github\FAKE\bin

Format of source is the same as [path sources](nuget-dependencies.html#Path-sources)

> Note: In case of source override, `paket restore` assumes the NuGet package **already exists** in   pointed directory - no build is going to be triggered.