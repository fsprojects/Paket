# Paket shell completion for [zsh](http://zsh.org/)

## Installation

### Download

1. Download the `paket-completion.zsh` file to a subdirectory of your home
   directory, preferably a directory with other function files.
1. Rename it to `_paket`.
1. Add the directory to your zsh `fpath` before running `compinit`.

```sh
$ target="$HOME/.zsh-completions"
$ mkdir "$target"
$ curl --fail --location --proto-redir -all,https --output "$target/_paket" https://raw.githubusercontent.com/fsprojects/Paket/master/completion/paket-completion.zsh
```

In your `~/.zshrc`:

```sh
fpath=($HOME/directory/where/_paket/resides $fpath)
```

### Updating an existing installation

Just repeat the download from above.

Alternatively we provide the `paket-completion-update` function that will
download the current `paket-completion.zsh` to the same file as above, even if
you changed its location after the initial download.

**Please note:** `paket-completion-update` is only available after your
first `paket` completion per shell session (i.e. after the Paket completion
functions have been autoloaded by zsh).

`paket-completion-update` supports an optional first parameter that allows you
to override the default download root URL which is
`https://raw.githubusercontent.com/fsprojects/Paket/master/completion/`.

### Paket alias

For easier consumption of Paket (without `paket.sh` or `paket.cmd`) it is advised
to create an alias and always run Paket from the repository root.

Somewhere in your `~/.zshrc`:

```sh
if [[ "$OS" != Windows* ]]; then
  alias paket='mono ./.paket/paket.exe'
else
  alias paket='./.paket/paket.exe'
fi
```

Also ensure that zsh
[completes aliases based on the expanded alias contents](http://zsh.sourceforge.net/Doc/Release/Options.html#index-COMPLETEALIASES).

```sh
unsetopt completealiases
```

If you don't like alias completion, define that the `paket` alias should be
completed using the `_paket` function defined in the `_paket` file.

```sh
setopt completealiases
compdef _paket paket
```

### Mono

If you use Mono (e.g. on Linux or macOS) and do not have Mono completion
installed, you need to define that `mono` invokes other programs:

```sh
compdef _precommand mono
```

This is similar to [`nohup`](http://man7.org/linux/man-pages/man1/nohup.1p.html)
invoking the "real" program that needs to be completed.
[More details.](https://unix.stackexchange.com/a/178054/72946)

For an exemplar `mono` completion, have a look at
[@agross dotfiles](https://github.com/agross/dotfiles/tree/master/mono/functions/_mono).

## Configuration

You can configure some aspects of Paket completion. Add these to your
`~/.zshrc`.

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

```
zstyle ':completion::complete:paket:*' use-fallback no
```

### Disable verbose completion of main commands

```
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
  # Rebuild if cache is more than a week old.
  local file="$1"
  local -a outdated
  # See http://zsh.sourceforge.net/Doc/Release/Expansion.html#Glob-Qualifiers
  outdated=( "$file"(Nm+7) )
  (( $#outdated ))
}

_strict_cache_policy () {
  return 0 # 0 == always outdated, you should better use use-cache off.
}
```

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

### Custom feed URLs for `--source` argument

```sh
zstyle ':completion::complete:paket:*' sources 'http://one.example.com/feed/v2'
zstyle ':completion::complete:paket:*' sources \
  'http://one.example.com/feed/v2' \
  'http://second.example.com/feed/v2'
```

Override list for a specific command; mind the trailing colon:

```
zstyle ':completion::complete:paket:find-package-versions:' sources \
  'http://another.example.com/feed/v2'
```
