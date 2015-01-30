# paket init-auto-restore
Enables automatic Package Restore in Visual Studio during the build process. 

    [lang=batchfile]
    $ paket init-auto-restore

The command:

  - creates a `.paket` directory in your solution root
  - downloads `paket.targets` and `paket.bootstrapper.exe` into it
  - adds an `<Import>` statement for `paket.targets` to all projects under the working directory.