## Adding credentials

```batchfile
paket config add-credentials SOURCEURL
```

Paket will then ask you for the username and password that will be used for the specified `SOURCEURL`.

The credentials you enter here will then be used if no username and password for the source are configured in the [`paket.dependencies` file](nuget-dependencies.html).

The configuration file can be found at:

	let AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
	let PaketConfigFolder = Path.Combine(AppDataFolder, "Paket")
	let PaketConfigFile = Path.Combine(PaketConfigFolder, "paket.config")
