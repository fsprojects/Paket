# Paket shell completion for [fish](https://fishshell.com)

## Installation
Download and place the completion file in your fish completions directory
```fish
$ set target "$HOME/.config/fish/completions/paket-completion.fish"
$ curl --fail --location --proto-redir -all,https --output "$target" \
  https://raw.githubusercontent.com/fsprojects/Paket/master/completion/paket-completion.fish
```
