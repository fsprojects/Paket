# GitHub dependencies

Paket allows you to automatically manage the linking of files from
[github.com](https://github.com) or
[gist.github.com](https://gist.github.com/) into your projects.

If you have Git installed then Paket also allows you to
[reference files from other git repositories](git-dependencies.html).

## Referencing a single file

You can reference a single file from [github.com](https://github.com) simply
by specifying the source repository and the file name in the
[`paket.dependencies` file](dependencies-file.html):

```paket
github forki/FsUnit FsUnit.fs
```

If you run the [`paket update` command](paket-update.html), it will download the
file to a subdirectory in `paket-files` and also add a new section to your
[`paket.lock` file](lock-file.html):

```paket
GITHUB
  remote: forki/FsUnit
    FsUnit.fs (7623fc13439f0e60bd05c1ed3b5f6dcb937fe468)
```

As you can see the file is pinned to a concrete commit. This allows you to
reliably use the same file version in succeeding builds until you elect to
perform a [`paket update` command](paket-update.html) at a time of your
choosing.

By default the `master` branch is used to determine the commit to reference, you
can specify the desired branch or commit in the
[`paket.dependencies` file](dependencies-file.html):

```paket
github fsharp/fsfoundation:gh-pages img/logo/fsharp.svg
github forki/FsUnit:7623fc13439f0e60bd05c1ed3b5f6dcb937fe468 FsUnit.fs
```

If you want to reference the file in one of your project files then add an entry
to the project's [`paket.references` file](references-files.html):

```paket
File: FsUnit.fs
```

and run [`paket install` command](paket-install.html). This will reference the
linked file directly into your project and by default, be visible under
`paket-files` directory in project.

![GitHub file referenced in project with default link](img/github_ref_default_link.png "GitHub file referenced in project with default link")

You can specify custom directory for the file:

```paket
File: FsUnit.fs Tests\FsUnit
```

![GitHub file referenced in project with custom link](img/github_ref_custom_link.png "GitHub file referenced in project with custom link")

Or if you use `.` for the directory, the file will be placed under the root of the project:

```paket
File: FsUnit.fs .
```

![GitHub file referenced in project under root of project](img/github_ref_root.png "GitHub file referenced in project under root of project")

## Referencing a GitHub repository

You can also reference a complete [github.com](http://github.com) repository
by specifying the repository id in the [`paket.dependencies`
file](dependencies-file.html):

```paket
// master branch
github tpetricek/FSharp.Formatting

// version 2.13.5
github tpetricek/FSharp.Formatting:2.13.5

// specific commit 30cd5366a4f3f25a443ca4cd62cd592fd16ac69
github tpetricek/FSharp.Formatting:30cd5366a4f3f25a443ca4cd62cd592fd16ac69
```

This will download the given repository and put it into your `paket-files`
directory. In this case we download the source of
[FSharp.Formatting](https://github.com/fsprojects/FSharp.Formatting).

## Recognizing build action

Paket will recognize build action for referenced file based on the project type.
As example, for a `*.csproj` project file, it will use `Compile` build action if
you reference `*.cs` file and `Content` build action if you reference file with
any other extension.

## Remote dependencies

If the remote file needs further dependencies then you can just put a
[`paket.dependencies` file](dependencies-file.html) into the same GitHub
repository directory. Let's look at a sample:

![Octokit module](img/octokit-module.png "Octokit module")

And we reference this in our own
[`paket.dependencies` file](dependencies-file.html):

```paket
github fsharp/FAKE modules/Octokit/Octokit.fsx
```

This generates the following [`paket.lock` file](lock-file.html):

```paket
NUGET
  remote: https://nuget.org/api/v2
    Microsoft.Bcl (1.1.9)
      Microsoft.Bcl.Build (>= 1.0.14)
    Microsoft.Bcl.Build (1.0.21)
    Microsoft.Net.Http (2.2.28)
      Microsoft.Bcl (>= 1.1.9)
      Microsoft.Bcl.Build (>= 1.0.14)
    Octokit (0.4.1)
      Microsoft.Net.Http (>= 0)
GITHUB
  remote: fsharp/FAKE
    modules/Octokit/Octokit.fsx (a25c2f256a99242c1106b5a3478aae6bb68c7a93)
      Octokit (>= 0)
```

As you can see Paket also resolved the `Octokit` dependency.

## Referencing a private GitHub repository

To reference a private GitHub repository the syntax is identical to above and
supports the same branch and file definitions the only extra item to add is an
identifier which defines which credential key to use (see [`paket
config`](paket-config.html)).

```paket
github fsharp/private src/myprivate/file.fs githubAuthKey
```

## Using a GitHub authentication key from environment variable

Paket will use a GitHub token from a enviroment variable
`PAKET_GITHUB_API_TOKEN`. This will allow you to access private repositories and to
work around the GitHub API limit on public repositories.

## Gist

Gist works the same way. You can fetch single files or multi-file Gists as well:

```paket
gist Thorium/1972308 gistfile1.fs
gist Thorium/6088882
```

If you run the [`paket update` command](paket-update.html), it will add a new
section to your [`paket.lock` file](lock-file.html):

```paket
GIST
  remote: Thorium/1972308
    gistfile1.fs
  remote: Thorium/6088882
    FULLPROJECT
```
