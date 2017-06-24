_paket()
{
  COMPREPLY=('Paket completion is not implemented')
}

paket-completion-update()
{
  local download_from="${1:-https://raw.githubusercontent.com/fsprojects/Paket/master/completion}"

  # entries are files under $download_from pointing to local files.
  local -A entries
  # Let the file point to the file defining our function.
  entries[paket-completion.bash]="${BASH_SOURCE[0]}"

  local key
  for key in "${!entries[@]}"; do
    local file="$key"
    local target="${entries[$key]}"

    if [[ -z "$target" ]]; then
      >&2 printf 'Could not determine target file for "%s"\n' "$file"
      return 1
    fi

    if [[ ! -f "$target" ]]; then
      >&2 printf 'Target file %s for %s does not exist\n' "$target" "$file"
      return 1
    fi

    local url="$download_from/$file"

    printf 'Downloading %s to %s\n' "$url" "$target"
    if ! curl --fail \
              --location \
              --proto-redir -all,https \
              "$url" \
              --output "$target"; then
      >&2 printf 'Error while downloading %s to %s\n' "$url" "$target"
      return 1
    fi
  done

  printf 'Paket completion was updated. Please restart your shell.\n'
}

complete -F _paket paket.exe
