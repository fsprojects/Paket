# paket push

Pushes the given `.nupkg` file.

    [lang=msh]
    paket push url <url> file <path> [apikey <key>] [endpoint <path>] [--help]

### Options:USAGE: paket push url <url> file <path> [apikey <key>] [endpoint <path>] [--help]

OPTIONS:


  `url <url>`: Url of the NuGet feed.

  `file <path>`: Path to the package.

  `apikey <key>`: Optionally specify your API key on the command line. Otherwise uses the value of the `nugetkey` environment variable.

  `endpoint <path>`: Optionally specify a custom api endpoint to push to. Defaults to `/api/v2/package`.

If you add the `-v` flag, then Paket will run in verbose mode and show detailed information.

With `--log-file [FileName]` you can trace the logged information into a file.

