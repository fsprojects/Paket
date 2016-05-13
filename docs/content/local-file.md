The paket.local file
===================

The `paket.local` file is used for testing changes before releasing new version of a dependent project.

> Note: you should not commit `paket.local`. This file should always be added to `.gitignore`, because it specifies paths accessible only on your machine.

Have a look at the following example:

```bash
nuget Argu -> git file:///c:\github\Argu feature_branch build:"build.cmd NuGet", Packages: /bin/
```