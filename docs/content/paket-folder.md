# The .paket folder

The [`.paket` folder](https://github.com/fsprojects/Paket/tree/master/.paket) is used the same way a `.nuget` folder is used for the [NuGet package restore](http://docs.nuget.org/docs/workflows/using-nuget-without-committing-packages).

Place this folder into the root of your solution. It should include the `paket.targets` and `paket.bootstrapper.exe` files which can be downloaded from [GitHub](https://github.com/fsprojects/Paket/releases/latest). 
The [bootstrapper](http://fsprojects.github.io/Paket/bootstrapper.html) will always download the latest version of the `paket.exe` file and it will be placed into the same folder.

Now, to install all the packages from the `paket.dependencies` files, just run the following command.

	.paket/paket.exe install