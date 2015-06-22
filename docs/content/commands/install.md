# [after-command]

If the paket.dependencies file has been changed since the last update of the paket.lock file (e.g. added dependencies or changed version requirements),
Paket will update the paket.lock file to make it match paket.dependencies again.

Unlike [`paket update`](paket-update.html), [`paket install`](paket-install.html) will only look for
new versions of dependencies that have been modified in paket.dependencies,
and use the version from paket.lock for all other dependencies.
