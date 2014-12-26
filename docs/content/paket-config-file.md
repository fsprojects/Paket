# paket.config file

Allows to store global configuration values like NuGet credentials. It can be found in:

	let AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
	let PaketConfigFolder = Path.Combine(AppDataFolder, "Paket")
	let PaketConfigFile = Path.Combine(PaketConfigFolder, "paket.config")

## Add credentials

Credentials for a specific source can be added with the following command:

	paket config add-credentials http://myserver.com/myfeed

Paket will then ask for username and password.

This credentials will be used if no username and password for the source are configured in a [paket.dependencies](nuget-dependencies.html).
