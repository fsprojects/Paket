## Adding credentials

### URL credentials

```sh
paket config --add-credentials SOURCE-URL
```

Paket will then ask you for the username and password that will be used for the
specified `SOURCE-URL`.

The credentials you enter here will then be used for `source`s in the
[`paket.dependencies` file](nuget-dependencies.html) that match `SOURCE-URL`
unless they carry a username and password.

### GitHub credentials

```sh
paket config --add-credentials CREDENTIAL-KEY
```

Paket will then ask you for the username and password that will be used for the
specified `CREDENTIAL-KEY`.

The credentials you enter here will then be used to access any GitHub files from
the [`paket.dependencies` file](github-dependencies.html) that carry the
specified `CREDENTIAL-KEY`.

```sh
paket config --add-token CREDENTIAL-KEY TOKEN
```

The `TOKEN` you enter here will then be used to access any GitHub files from the
[`paket.dependencies` file](github-dependencies.html) that carry the
specified `CREDENTIAL-KEY`.

The configuration file can be found at:

```fsharp
let AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
let PaketConfigFolder = Path.Combine(AppDataFolder, "Paket")
let PaketConfigFile = Path.Combine(PaketConfigFolder, "paket.config")
```

## Example

### Adding a NuGet API key

```sh
paket config --add-token 'https://www.nuget.org' '4003d786-cc37-4004-bfdf-c4f3deadbeef'
```
