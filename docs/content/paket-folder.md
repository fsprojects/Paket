# The .paket folder

The [`.paket` folder](https://github.com/fsprojects/Paket/tree/master/.paket) is used the same way a `.nuget` folder is used for the [NuGet package restore](http://docs.nuget.org/docs/workflows/using-nuget-without-committing-packages).

Place this folder into the root of your repository. It should include the `paket.targets` and `paket.bootstrapper.exe` files which can be downloaded from [GitHub](https://github.com/fsprojects/Paket/releases/latest). 
The [bootstrapper](http://fsprojects.github.io/Paket/bootstrapper.html) will always download the latest version of the `paket.exe` file and it will be placed into the same folder.

Now, to install all the packages from the `paket.dependencies` files, just run the following command.

	.paket/paket.exe install
	
The location of `.paket` folder and Paket related files is not bound to location of Visual Studio solution file. Paket does not read or look for any solution files.
In case you have multiple solutions in subfolders of some root folder, then that root folder would be a good place to put .paket folder and the [`paket.dependencies` file](dependencies-file.html) there. 
`.paket/paket.exe install` command processes all folders under the root recursively and touch only those projects which have `paket.references` file in their folder.
When Paket encounters [`paket.dependencies` files](dependencies-file.html) in subfolder it ignores that subfolder (and everything under it) completely, 
implying that they use an independent [`paket.lock` file](lock-file.html) and `packages` folder. 
`packages` folder would also be created at the root level for all projects under it.

