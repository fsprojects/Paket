## Adding credentials

### URL credentials

```sh
paket config add-credentials <source URL>
```

Paket will then ask you for the username, password, and authentication type that 
will be used for the specified `<source URL>`.

The credentials you enter here will then be used for `source`s in the
[`paket.dependencies` file](nuget-dependencies.html) that match `<source URL>`
unless they carry a username and password.

### Verifying the source URL and credentials

```sh
paket config add-credentials <source URL> --verify
```

Paket will verify that the `<source URL>` and the credentials entered with the given
authentication type are valid. This feature is useful if you want to avoid storing
wrong credentials or URL in the Paket configuration file.

### GitHub credentials

```sh
paket config add-credentials <credential key>
```

Paket will then ask you for the username and password that will be used for the
specified `<credential key>`.

The credentials you enter here will then be used to access any GitHub files from
the [`paket.dependencies` file](github-dependencies.html) that carry the
specified `<credential key>`.

```sh
paket config add-token <credential key> <token>
```

The `<token>` you enter here will then be used to access any GitHub files from
the [`paket.dependencies` file](github-dependencies.html) that carry the
specified `<credential key>`.

The configuration file can be found at:

```fsharp
let AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
let PaketConfigFolder = Path.Combine(AppDataFolder, "Paket")
let PaketConfigFile = Path.Combine(PaketConfigFolder, "paket.config")
```

## Example

### Adding a NuGet API key

```sh
paket config add-token 'https://www.nuget.org' '4003d786-cc37-4004-bfdf-c4f3deadbeef'
```
