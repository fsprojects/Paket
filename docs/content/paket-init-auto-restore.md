# paket init-auto-restore

Enables automatic Package Restore in Visual Studio. Under the hood, the command:

  - creates a `.paket` directory in your solution root
  - downloads `paket.targets` and `paket.bootstrapper.exe` into it
  - adds an `<Import` statement for `paket.targets` to all projects under the working directory.


    [lang=batchfile]
    $ paket init-auto-restore
