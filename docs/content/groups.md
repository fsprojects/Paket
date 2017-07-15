# Dependency groups

Groups allow for better organization of dependencies and also enable [easier
conflict resolution](groups.html#Conflict-resolution-with-groups).

Let's consider a small example:

```paket
source https://nuget.org/api/v2

nuget Newtonsoft.Json
nuget UnionArgParser
nuget FSharp.Core

github forki/FsUnit FsUnit.fs
github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs

group Build

  source https://nuget.org/api/v2

  nuget FAKE
  nuget FSharp.Formatting
  nuget ILRepack

  github fsharp/FAKE modules/Octokit/Octokit.fsx

group Test

  source https://nuget.org/api/v2

  nuget NUnit.Runners
  nuget NUnit
```

As you can see we have 3 different groups in the
[`paket.dependencies` file](dependencies-file.html). The first one is the
default group (internally called `Main`) and the other two groups contain
dependencies that are used for `Build` and `Test`.

**Note:** The indentation in groups is optional.

After [`paket install`](paket-install.html) the generated
[`paket.lock` file](lock-file.html) looks like this:

```paket
NUGET
  remote: https://nuget.org/api/v2
    FSharp.Core (4.0.0.1)
    Newtonsoft.Json (7.0.1)
    UnionArgParser (0.6.3)
GITHUB
  remote: forki/FsUnit
    FsUnit.fs (81d27fd09575a32c4ed52eadb2eeac5f365b8348)
  remote: fsharp/FAKE
    src/app/FakeLib/Globbing/Globbing.fs (991bea743c5d5e8eec0defc7338a89281ed3f51a)
  remote: fsprojects/Chessie
    src/Chessie/ErrorHandling.fs (1f23b1caeb1f87e750abc96a25109376771dd090)

GROUP Build
NUGET
  remote: https://nuget.org/api/v2
    FAKE (4.3.1)
    FSharp.Compiler.Service (1.4.0.1)
    FSharp.Formatting (2.10.0)
      FSharp.Compiler.Service (>= 0.0.87)
      FSharpVSPowerTools.Core (1.8.0)
    FSharpVSPowerTools.Core (1.8.0)
      FSharp.Compiler.Service (>= 0.0.87)
    ILRepack (2.0.5)
    Microsoft.Bcl (1.1.10)
      Microsoft.Bcl.Build (>= 1.0.14)
    Microsoft.Bcl.Build (1.0.21)
    Microsoft.Net.Http (2.2.29)
      Microsoft.Bcl (>= 1.1.10)
      Microsoft.Bcl.Build (>= 1.0.14)
    Octokit (0.14.0)
      Microsoft.Net.Http
GITHUB
  remote: fsharp/FAKE
    modules/Octokit/Octokit.fsx (991bea743c5d5e8eec0defc7338a89281ed3f51a)
      Octokit

GROUP Test
NUGET
  remote: https://nuget.org/api/v2
    NUnit (2.6.4)
    NUnit.Runners (2.6.4)
```

As you can see every group is listed separately and it's possible to let Paket
[restore only specific groups](paket-restore.html).

If you want to reference dependencies from projects you can do this via the
following syntax in the [`paket.references` file.](references-files.html):

```paket
FSharp.Core

group Test
  NUnit
  NUnit.Runners
```

## Conflict resolution with groups

Paket's group feature allows you to use multiple versions of the same package.
Let consider the following [`paket.dependencies` file](dependencies-file.html):

```paket
source https://nuget.org/api/v2

nuget Newtonsoft.Json

group Legacy
  source https://nuget.org/api/v2

  nuget Newtonsoft.Json ~> 6
```

Every group will be resolved independently to the following
[`paket.lock` file](lock-file.html):

```paket
NUGET
  remote: https://nuget.org/api/v2
    Newtonsoft.Json (7.0.1)

GROUP Legacy
NUGET
  remote: https://nuget.org/api/v2
    Newtonsoft.Json (6.0.8)
```

Paket is downloading both version. You will get `packages/Newtonsoft.Json` with
version `7.0.1` and `packages/legacy/Newtonsoft.Json` with the `6.0.8`
libraries.

In your [`paket.references` files](references-files.html) you can now decide
which version you want to use:

```paket
group Legacy
Newtonsoft.Json // Version 6.0.8
```
