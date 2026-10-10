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

This directory sits in the root of your repository, next to the
`paket.dependencies` file. It holds the MSBuild files that restore packages
before a build, `Paket.Restore.targets` and `paket.targets`, and the load
scripts Paket generates. See [the .paket directory](paket-folder.html).
