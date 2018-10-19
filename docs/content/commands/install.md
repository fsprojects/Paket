## Notes on changes to [`paket.dependencies`](dependencies-file.html)

If the [`paket.dependencies` file](dependencies-file.html) has been changed
since the last update of the [`paket.lock` file](lock-file.html) (e.g. you added
dependencies or changed version constraints), Paket will update the
[`paket.lock` file](lock-file.html) to make it match
[`paket.dependencies`](dependencies-file.html) again.

Unlike [`paket update`](paket-update.html),
[`paket install`](paket-install.html) will only look for new versions of
dependencies that have been modified in
[`paket.dependencies`](dependencies-file.html) and use the version from
[`paket.lock`](lock-file.html) for all other dependencies.
