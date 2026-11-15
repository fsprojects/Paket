# Paket shell completion for [zsh](http://zsh.org/)

## Installation

### Download

1. Download the
  [`paket-completion.zsh` file](https://raw.githubusercontent.com/fsprojects/Paket/master/completion/paket-completion.zsh)
  to a subdirectory of your home directory, preferably a directory with other
  function files.
1. Rename it to `_paket`.
1. Add the directory to your zsh `fpath` before running `compinit`.

```sh
$ target="$HOME/.zsh-completions"
$ mkdir "$target"
$ curl --fail --location --proto-redir -all,https --output "$target/_paket" \
  https://raw.githubusercontent.com/fsprojects/Paket/master/completion/paket-completion.zsh
```

In your `~/.zshrc`:

```sh
fpath=($HOME/directory/where/_paket/resides $fpath)
```

### Updating an existing installation

Just repeat the download from above.

Alternatively the completion script comes bundled with the
`paket-completion-update` function that will download the current
`paket-completion.zsh` to the same file as above, even if you changed its
location after the initial download.

**Please note:** The `paket-completion-update` function requires `curl` to be
installed. `paket-completion-update` is *only available after your
first `paket` completion* per shell session (i.e. after the Paket completion
functions have been `autoload`ed by zsh).

`paket-completion-update` supports an optional first parameter that allows you
to override the default download root URL which is
`https://raw.githubusercontent.com/fsprojects/Paket/master/completion`.

### `paket` alias

Paket is a .NET tool. When it is a
[local tool](https://fsprojects.github.io/Paket/installation.html#Local-tool) of
the repository, it is advised to create an alias and always run Paket from the
repository root.

Somewhere in your `~/.zshrc`:

```sh
alias paket='dotnet paket'
setopt completealiases
```

With
[`completealiases`](http://zsh.sourceforge.net/Doc/Release/Options.html#index-COMPLETEALIASES),
zsh completes the alias as a command of its own, which the `_paket` function
completes as `paket`. Otherwise zsh completes the arguments of `dotnet`. The
option applies to all aliases: one that relies on the completion of its
command, such as `alias g=git`, then needs `compdef g=git` after `compinit`.
Instead of the alias and the option, a function gets the completion of `paket`:

```sh
paket() { dotnet paket "$@" }
```

## Configuration

You can configure some aspects of Paket completion. Add these to your
`~/.zshrc`.

### Define where to look for `paket`

Depending on what should be completed the Paket executable will be run by the
completion script. The completion script runs a local tool when a tool
manifest declares Paket, else a global tool (i.e. `paket` in the `$PATH`). As
dotnet, it reads the closest `.config/dotnet-tools.json` or `dotnet-tools.json`
from the current directory up that declares Paket, and doesn't look above an
`"isRoot"` one.

Completing in a repository shouldn't run one of its files. So the script runs
the `paket.dll` of the local tool restored in `$NUGET_PACKAGES` or
`~/.nuget/packages`, with `dotnet`, instead of `dotnet paket`, which reads the
`global.json` of the repository: its `sdk.paths` can point dotnet at an SDK of
the repository. It reads no `NuGet.config`, so that the repository can't
choose the `paket.dll`: if yours sets `globalPackagesFolder`, export
`NUGET_PACKAGES` with that folder. When the local tool isn't restored there, it
runs the global tool.

For the same reason, it doesn't run a Paket of the current directory, such as
one installed with `dotnet tool install paket --tool-path .paket`, unless the
following `paket-executable` style lists it. The list of values will be
searched as defined, before the local tool and the global tool. A relative path
runs that file in every directory you complete in, the repositories you don't
trust included.

```sh
# Run Paket installed with --tool-path .paket or --tool-path . first.
zstyle ':completion::complete:paket:*' paket-executable './.paket/paket' './paket'
# Or, useful for Paket developers: Prefer the locally built version in bin over the local tool.
zstyle ':completion::complete:paket:*' paket-executable './bin/net10.0/paket'
```

### Enable infix matching for package IDs

By default `paket find-packages` will match infixes, which is not the default
mode for Paket completion. I think it's unnatural to type

```sh
paket add FAK<tab>
```

which will be completed to

```sh
paket add Altairis.Fakturoid.Client
```

if infix matching is enabled.

If you want this behavior, enable infix matches:

```sh
zstyle ':completion::complete:paket:*' infix-match yes
zstyle ':completion::complete:paket:add:*' infix-match yes # Only for paket add.
```

### Disable fallback (i.e. default zsh) completion for Paket commands that do not have a completion function

```sh
zstyle ':completion::complete:paket:*' use-fallback no
```

### Disable verbose completion of main commands

```sh
zstyle ':completion::complete:paket:*' verbose no
```

### Define the zsh completion cache policy for expensive operations that generate completions

Paket will e.g. issue HTTP requests when you complete a package ID or a
version number. These take time so we take advantage of the zsh
completion cache and store such results.

Enable the zsh completion cache globally, including completions for other
commands that also leverage caching:

```sh
zstyle ':completion:*' use-cache on
zstyle ':completion:*' cache-path instead/of/$HOME/.zcompcache # Optional.
```

To disable the cache for specific Paket commands to always get fresh
results when completing e.g. `paket add`:

```sh
zstyle ':completion::complete:paket:add:*' use-cache off
```

The caches are stored under the `cache-path` as follows:

```text
<cache-path>/paket/<expensive command>/<parameter>
```

e.g. `<cache-path>/paket/find-packages/fak` if you completed `paket add FAK` or
` paket add fak` before.

The default cache policy caches results for 1 day (see `_paket_cache_policy`).
To remove cached results you can either delete the
`<cache-path>/paket` directory or provide a custom cache policy to control
cache expiration for all or specific Paket commands:

```sh
zstyle ':completion::complete:paket:*' cache-policy _default_cache_policy
zstyle ':completion::complete:paket:find-packages:*' cache-policy _strict_cache_policy

_default_cache_policy () {
  # Rebuild if the cache is more than a week old.
  local cache="$1"
  local -a outdated
  # See http://zsh.sourceforge.net/Doc/Release/Expansion.html#Glob-Qualifiers
  outdated=( "$cache"(Nm+7) )
  (( $#outdated ))
}

_strict_cache_policy () {
  return 0 # 0 == always outdated, you should better use use-cache off.
}
```

There are two special default cache policies for completions relying on the
existence of local files, e.g. `paket.dependencies` and `paket.lock`.
For Paket commands that read these, the cache is invalidated as soon as the
file's modification time is newer than the cache.
(See `_paket_cache_policy_dependencies_file` and
`_paket_cache_policy_lock_file`.)

### Disable running Paket to get packages, versions etc. as completion arguments

```sh
zstyle ':completion::complete:paket:*' disable-completion yes
```

Disable only a single means to get completion values:

```sh
# Used by e.g. paket add:
zstyle ':completion::complete:paket:find-packages:' disable-completion yes
zstyle ':completion::complete:paket:find-package-versions:' disable-completion yes
zstyle ':completion::complete:paket:show-groups:' disable-completion yes

# Used by e.g. paket why:
zstyle ':completion::complete:paket:show-installed-packages:' disable-completion yes
```

### Sources of the package IDs and versions

To complete package IDs and versions, the completion script runs
`paket find-packages` and `paket find-package-versions` against
`https://www.nuget.org/api/v2`, and not against the sources of
`paket.dependencies`: their credentials can name environment variables, whose
values Paket sends to those sources, so that a repository would get them on a
key press. To query other sources, list them. Paket doesn't send them the
credentials stored with `paket config add-credentials`, and through `--source`,
`paket find-package-versions` only reads NuGet v2 feeds: a v3 URL, ending with
`index.json`, gives no versions.

Unlike the `sources` style below, which only lists the URLs offered after
`--source` and `--url`, this style sets the feeds that are queried. Here
`find-packages` and `find-package-versions` name the queries, whatever command
you complete, as for `disable-completion`.

```sh
zstyle ':completion::complete:paket:*' package-sources \
  'https://www.nuget.org/api/v2' \
  'https://example.com/feed/v2'
# Only for the versions.
zstyle ':completion::complete:paket:find-package-versions:' package-sources \
  'https://example.com/feed/v2'
```

### Custom feed URLs for `--source` and `paket push --url` argument

```sh
zstyle ':completion::complete:paket:*' sources 'http://one.example.com/feed/v2'
zstyle ':completion::complete:paket:*' sources \
  'http://one.example.com/feed/v2' \
  'http://second.example.com/feed/v2'
```

Override list for a specific command:

```sh
zstyle ':completion::complete:paket:find-package-versions:*' sources \
  'http://another.example.com/feed/v2'
zstyle ':completion::complete:paket:push:*' sources \
  'https://myget.org/F/my-feed-name'
```
