_paket()
{
  COMPREPLY=()

  local current="${COMP_WORDS[COMP_CWORD]}"
  local previous="${COMP_WORDS[COMP_CWORD-1]}"

  local -a line=(${COMP_WORDS[@]})
  # Remove paket.exe
  unset line[0]

  local -a commands=(
    add
    auto-restore
    clear-cache
    config
    convert-from-nuget
    find-package-versions
    find-packages
    find-refs
    generate-load-scripts
    init
    install
    outdated
    pack
    push
    remove
    restore
    show-groups
    show-installed-packages
    simplify
    update
    why
  )
  local -a opts=(
    --help
    --log-file
    --silent
    --verbose
    --version
  )

  if [[ "$COMP_CWORD" == '1' ]]; then
    if [[ "$current" == -* ]] ; then
      COMPREPLY=( $(compgen -W "$(printf '%s ' "${opts[@]}")" -- "$current") )
      return 0
    fi

    COMPREPLY=( $(compgen -W "$(printf '%s ' "${commands[@]}")" -- "$current") )
    return 0
  fi

  case "${line[@]}" in
    (--log-file*)
      # Complete file name.
      COMPREPLY=( $(compgen -f "$current") )
      return 0
      ;;

    (add*)
      opts+=(
        --clean-redirects
        --create-new-binding-files
        --force
        --group
        --interactive
        --keep-major
        --keep-minor
        --keep-patch
        --no-install
        --project
        --redirects
        --touch-affected-refs
        --version
      )

      COMPREPLY=( $(compgen -W "$(printf '%s ' "${opts[@]}")" -- "$current") )
      return 0
      ;;

    *)
      ;;
  esac

  return 1
}

paket-completion-update()
{
  local download_from="${1:-https://raw.githubusercontent.com/fsprojects/Paket/master/completion}"

  # entries are files under $download_from pointing to local files.
  local -A entries
  # Let the file point to the file defining our function.
  entries[paket-completion.bash]="$(readlink "${BASH_SOURCE[0]}")"

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
