Auto-restore on:

  - creates a `.paket` directory in your root directory,
  - downloads `paket.targets` and `paket.bootstrapper.exe` into the `.paket` directory,
  - adds an `<Import>` statement for `paket.targets` to projects that have the [references file](references-files.html).
  
Auto-restore off:

  - removes `paket.targets` from the `.paket` directory,
  - removes the `<Import>` statement for `paket.targets` from projects that have the [references file](references-files.html).