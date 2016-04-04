# paket push

Pushes the given `.nupkg` file.

    [lang=msh]
    paket push url <string> file <string> [apikey <string>] [endpoint <string>]

### Options:

  `url <string>`: Url of the NuGet feed.

  `file <string>`: Path to the package.

  `apikey <string>`: Optionally specify your API key on the command line. Otherwise uses the value of the `nugetkey` environment variable, or the api key stored in the [`paket.config` file](paket-config.html) for this url.

  `endpoint <string>`: Optionally specify a custom api endpoint to push to. Defaults to `/api/v2/package`.

If you add the `-v` flag, then Paket will run in verbose mode and show detailed information.

With `--log-file [FileName]` you can trace the logged information into a file.
