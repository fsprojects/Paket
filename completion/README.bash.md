# Paket shell completion for [bash](https://www.gnu.org/software/bash/)

## Installation

### Download

#### Option 1: Save the completion script to your home directory

1. Download the
   [`paket-completion.bash` file](https://raw.githubusercontent.com/fsprojects/Paket/master/completion/paket-completion.bash)
   to a subdirectory of your home directory, preferably a directory with other
   function files.
1. `source` the file in your `~/.bashrc`.

```sh
$ target="$HOME/.bash-completions/paket-completion.bash"
$ mkdir "$(dirname "$target")"
$ curl --fail --location --proto-redir -all,https --output "$target" \
  https://raw.githubusercontent.com/fsprojects/Paket/master/completion/paket-completion.bash
```

In your `~/.bashrc`:

```sh
source "$HOME/.bash-completions/paket-completion.bash"
```

#### Option 2: Save the completion script to `/etc/bash_completion.d/`

1. Download the `paket-completion.bash` file to `/etc/bash_completion.d/`.
1. Restart your shell.

```sh
$ target="/etc/bash_completion.d/paket-completion.bash"
$ curl --fail --location --proto-redir -all,https --output "$target" \
  https://raw.githubusercontent.com/fsprojects/Paket/master/completion/paket-completion.bash
```

### Updating an existing installation

Just repeat the download from above.

Alternatively the completion script comes bundled with the
`paket-completion-update` function that will download the current
`paket-completion.bash` to the same file as above, even if you changed its
location after the initial download.

**Please note:** The `paket-completion-update` function requires the completion
script to be `source`d and `curl` to be installed.

`paket-completion-update` supports an optional first parameter that allows you
to override the default download root URL which is
`https://raw.githubusercontent.com/fsprojects/Paket/master/completion`.

### `paket` alias

For easier consumption of Paket (without `paket.sh` or `paket.cmd`) it is
advised to create an alias and always run Paket from the repository root.

Also have a look at
[Paket's magic mode](https://fsprojects.github.io/Paket/bootstrapper.html#Magic-mode).

Somewhere in your `~/.bashrc`:

```sh
if [[ "$OS" != Windows* ]]; then
  alias paket='mono ./.paket/paket.exe'
else
  alias paket='./.paket/paket.exe'
fi

# Complete the paket alias using the _paket function.
complete -F _paket paket
```
