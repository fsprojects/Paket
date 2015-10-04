## Adding credentials

```batchfile
paket config add-credentials SOURCEURL
```

Paket will then ask you for the username and password that will be used for the specified `SOURCEURL`.

The credentials you enter here will then be used if no username and password for the source are configured in the [`paket.dependencies` file](nuget-dependencies.html).

```batchfile
paket config add-credentials TAG
```

Paket will then ask you for the username and password that will be used for the specified `TAG`.

The credentials you enter here will then be used to access any Github files specified in the [`paket.dependencies` file](github-dependencies.html) with the specified `TAG`.

```batchfile
paket config add-token TAG TOKEN
```

The token you enter here will then be used to access any Github files specified in the [`paket.dependencies` file](github-dependencies.html) with the specified `TAG`.


The configuration file can be found at:

	let AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
	let PaketConfigFolder = Path.Combine(AppDataFolder, "Paket")
	let PaketConfigFile = Path.Combine(PaketConfigFolder, "paket.config")
