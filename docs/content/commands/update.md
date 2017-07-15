## Updating all packages

If you do not specify a package, then all packages from
[`paket.dependencies`](dependencies-file.html) are updated.

```sh
paket update
```

First, the current [`paket.lock` file](lock-file.html) is deleted.
[`paket update`](paket-update.html) then recomputes the current dependency
resolution, as explained under
[Package resolution algorithm](resolver.html), and writes it to
[`paket.lock` file](lock-file.html). It then proceeds to download the packages
and to install them into the projects.

Please see [`paket install`](paket-install.html) if you want to keep the current
versions from your [`paket.lock` file](lock-file.html).

## Updating a single package, or packages matching a pattern

It's also possible to update only specified packages and to keep all other
dependencies fixed:

```sh
paket update <package id>
paket update <package id> --filter
```

The `--filter` parameter makes Paket interpret the `<package id>` as a regular
expression pattern to match against, rather than a single package. Paket
enforces a "total" match (i.e. an implicit `^` and `$` at beginning and end of
`<package id>` as added).

## Updating a single group

If you want to update a single group you can use the following command:

```sh
paket update --group <group>
```

## Updating [`http` dependencies](http-dependencies.html)

If you want to update a file you need to use the
[`paket install` command](paket-install.html) or
[`paket update` command](paket-update.html) with the `--force` parameter.

Using [groups](groups.html) for [`http` dependent files](http-dependencies.html)
can be helpful in order to reduce the number of files that are reinstalled.
