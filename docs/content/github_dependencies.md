# Github dependencies

** Only in [0.2.0 alpha versions](https://www.nuget.org/packages/Paket/0.2.0-alpha001) **

Paket allows to link files from [Github.com](http://www.github.com) into your projects.

## Referencing a single file

You can reference a single file from [Github.com](http://www.github.com) simply by specifying the source repository and the file name in the [`paket.dependencies` file](dependencies_file.html):

    github forki/FsUnit FsUnit.fs