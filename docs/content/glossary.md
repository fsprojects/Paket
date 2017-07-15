# Glossary

## paket.dependencies

The [`paket.dependencies` file](dependencies-file.html) is used to specify rules
regarding your application's dependencies.

## paket.lock

The [`paket.lock` file](lock-file.html) records the concrete dependency
resolution of all direct and indirect dependencies of your project.

## paket.references

The [`paket.references` files](references-files.html) are used to specify which
dependencies are to be installed into the MSBuild projects in your repository.

## paket.template

The [`paket.template` files](template-files.html) are used to specify rules to
create new NuGet packages by using the [`paket pack` command](paket-pack.html).

## .paket directory

This directory is used the same way a `.nuget` directory is used for the NuGet
package restore, that is to cache package archives for reference by the
development project. Place this directory into the root of your repository. It
should include the paket.targets and
[`paket.bootstrapper.exe`](https://github.com/fsprojects/Paket/releases/latest)
files which can be downloaded from GitHub. The bootstrapper executable will
always download the latest version of the `paket.exe` file into this directory.
