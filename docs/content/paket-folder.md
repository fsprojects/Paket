# The `.paket` folder

The [`.paket` folder](https://github.com/fsprojects/Paket/tree/master/.paket) is used the same way a `.nuget` folder is used for the [NuGet package restore](http://docs.nuget.org/docs/workflows/using-nuget-without-committing-packages).

Place this folder into the root of your solution. It will include the `paket.targets` and `paket.bootstrapper.exe` files. The [bootstrapper](http://fsprojects.github.io/Paket/bootstrapper.html) will download the latest version of the `paket.exe` file (if it doesn't already exist) and it will be placed into the same folder.

Now, to install all the packages from the `paket.dependencies` files, just run the following command.

	.paket/paket.exe install