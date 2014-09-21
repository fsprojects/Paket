# paket init-auto-restore

Enables automatic package restore in Visual Studio.
Under the hood, the command creates `.paket` directory, downloads `paket.targets` and `paket.bootstrapper.exe`, and finally adds import statement for `paket.targets` to all projects under the working directory.

    [lang=batchfile]
    $ paket init-auto-restore