#compdef paket.exe

# Zsh completion script for Paket (https://github.com/fsprojects/Paket/).
#
# This script is based on the excellent git complection in zsh. Many thanks
# to its authors!

_paket() {
  # TODO
  echo $service > /tmp/_paket

  local curcontext=$curcontext state line ret=1
  local -A opt_args

  # Do not offer anything after these options.
  local -a terminating_options
  terminating_options=(
    '(- :)'--version'[show Paket version]'
  )

  # Currently not implemented:
  # ! means that if these options were specified right after paket, do not
  # offer them as completions for the command.
  # E.g. paket --verbose install --<tab> won't show verbose again
  local -a global_options
  global_options=(
    '(- :)'--help'[display help]'
    '(--log-file)'--log-file'[print output to a file]:log file:_files'
    '(-s --silent)'{-s,--silent}'[suppress console output]'
    '(-v --verbose)'{-v,--verbose}'[print detailed information to the console]'
  )

  local -a keep_options
  keep_options=(
    '(--keep-major --keep-minor --keep-patch)'--keep-major'[only allow updates that preserve the major version]'
    '(--keep-major --keep-minor --keep-patch)'--keep-minor'[only allow updates that preserve the minor version]'
    '(--keep-major --keep-minor --keep-patch)'--keep-patch'[only allow updates that preserve the patch version]'
  )

  local -a binding_redirects_options
  binding_redirects_options=(
    '(--redirects)'--redirects'[create binding redirects]'
    '(--clean-redirects)'--clean-redirects'[remove binding redirects that were not created by Paket]'
    '(--create-new-binding-files)'--create-new-binding-files'[create binding redirect files if needed]'
  )

  local -a download_options
  download_options=(
    '(-f --force)'{-f,--force}'[force download and reinstallation of all dependencies]'
    '(--touch-affected-refs)'--touch-affected-refs'[touch project files referencing affected dependencies to help incremental build tools detecting the change]'
  )

  # --verbose, --log-file and --silent (see above)
  #   These can appear as the first option, optionally followed by a command.
  #
  # --version and --help (see above)
  #   No more options are allowed afterwards.
  #
  # command
  #   Does not start with dash.
  #
  # option-or-argument
  #   This is the "rest" argument.
  #
  # For more information, see http://zsh.sourceforge.net/Doc/Release/Completion-System.html
  # and search for "Each of the forms above may be preceded by a list in
  # parentheses of option names and argument numbers".
  _arguments -C \
    $terminating_options \
    $global_options \
    '(-): :->command' \
    '(-)*:: :->option-or-argument' \
  && return

  # TODO
  typeset -p state >> /tmp/_paket

  case "$state" in
    (command)
      _paket_commands && ret=0
      ;;

    (option-or-argument)
      curcontext=${curcontext%:*:*}:paket-$words[1]:
      typeset -p curcontext >> /tmp/_paket
      typeset -p words >> /tmp/_paket

      if ! _call_function ret _paket-$words[1]; then
        _message "paket command '$words[1]' is not implemented, please contact @agross"

        if zstyle -T :completion:$curcontext: use-fallback; then
          _default && ret=0
        fi
      fi
      ;;
  esac

  return ret
}

#(( $+functions[_paket-add] )) ||
_paket-add() {
  local curcontext=$curcontext state line ret=1
  declare -A opt_args

  local -a args
  args=(
    $global_options
    $keep_options
    $binding_redirects_options
    $download_options
    '(--interactive -i)'{--interactive,-i}"[ask for every project whether to add the dependency]"
    '(--no-install)'--no-install'[skip install process after resolving dependencies]'
    '(--project -p)'{--project,-p}'[add the package to a single project only]:project:_path_files -g "**/*.??proj"'
    '(--version -V)'{--version,-V}'[package version constraint]:version constraint'
    '(--group -g)'{--group,-g}'[add the package to a group (default: Main group)]:group:_paket_groups'
  )

  _arguments -C \
    $args \
    ':NuGet package ID' \
  && ret=0

  return ret
}

#(( $+functions[_paket-install] )) ||
_paket-install() {
  _arguments \
    $global_options \
    $keep_options \
    $binding_redirects_options \
    $download_options
}

#(( $+functions[_paket-restore] )) ||
_paket-restore() {
  local curcontext=$curcontext state line ret=1
  declare -A opt_args

  local -a args
  args=(
    $global_options
    $download_options
    '(--ignore-checks)'--ignore-checks'[Skips the test if paket.dependencies and paket.lock are in sync]'
    '(--references-files --only-referenced)'--references-files'[Restore all packages from the given paket.references files. This implies --only-referenced]'
  )

  _arguments -C \
    $args \
    ': :->command' \
    '*:: :->option-or-argument' \
  && ret=0

  case $state in
    (command)
      local -a commands

      commands=(
        group:'Restore a single group'
      )

      _describe -t commands command commands && ret=0
      ;;

    (option-or-argument)
      curcontext=${curcontext%:*}-$line[1]:

      if [[ $line[1] == 'group' ]]; then
        _arguments \
          ': :_paket_groups' \
          $args \
        && ret=0
      fi
      ;;
  esac

  return ret
}

(( $+functions[_paket_commands] )) ||
_paket_commands() {
  local -a types
  types=(
    dependency
    inspection
    nuget
    misc
  )

  for type in $types; do
    local -a $type
  done

  dependency=(
    add:'add a new dependency to paket.dependencies'
    install:'download the dependencies specified by paket.dependencies or paket.lock into the packages/ directory and update projects'
    outdated:'find dependencies that have newer versions available'
    remove:'remove a dependency from paket.dependencies and all paket.references files'
    restore:'download the dependencies specified by the paket.lock file into the packages/ directory'
    simplify:'simplify paket.dependencies and paket.references by removing transitive dependencies'
    update:'update dependencies to their latest version'
  )

  inspection=(
    find-packages:'search for packages'
    find-package-versions:'search for package versions'
    find-refs:'find all project files that have a dependency installed'
    show-groups:'show groups'
    show-installed-packages:'show installed top-level packages'
    why:'determine why a dependency is required'
  )

  nuget=(
    convert-from-nuget:'convert projects from NuGet to Paket'
    fix-nuspecs:'patch a list of .nuspec files to correct transitive dependencies'
    generate-nuspec:'generate a default nuspec for a project including its direct dependencies'
    pack:'pack paket.template files within this repository'
    push:'push a .nupkg file'
  )

  misc=(
    auto-restore:'manage automatic package restore during the build process inside Visual Studio'
    clear-cache:'clear the NuGet and git cache directories'
    config:'store global configuration values like NuGet credentials'
    generate-load-scripts:'generate C# and F# include scripts that reference installed packages in a interactive environment like F# Interactive or ScriptCS'
    init:'create an empty paket.dependencies file in the current working directory'
  )

  for type in $types; do
    local -a all_commands "${type}_commands"

    # Remove everything after the colon of the command definition above.
    set -A "${type}_commands" ${(P)type%%:*}
    # Copy command list to all_commands.
    all_commands+=(${(P)${:-${type}_commands}})
  done

  # To get the length of the longest matching command, filter the list of
  # commands down to the prefix the user typed.
  # Get applicable matchers.
  local expl
  _description '' expl ''
  local -a all_matching_commands
  compadd "$expl[@]" -O all_matching_commands -a all_commands
  # Length of longest match.
  longest_match=${#${(O)all_matching_commands//?/.}[1]}

  # Verbose/long display requested?
  local -a disp
  if zstyle -T ":completion:${curcontext}:" verbose; then
    disp=(-ld '${type}_desc')
  fi

  local -a alternatives
  for type in $types; do
    local -a "${type}_desc"

    # Write description:
    #   1. command padded with spaces up to longest_match
    #   2. ' -- '
    #   3. description, trimmed if longer than screen width
    set -A "${type}_desc" \
      ${${(r.$COLUMNS-1.)${(P)type}/(#s)(#m)[^:]##:/${(r.longest_match.)MATCH[1,-2]} -- }%% #}

    alternatives+=("${type}:$type command:compadd ${(e)disp} -a ${type}_commands")
  done

  _alternative $alternatives
}

#(( $+functions[_paket_groups] )) ||
_paket_groups() {
  local -a groups
  local output exit_status

  # Replace CR, in case we're running on Windows.
  output="${$(_call_program groups "$(_paket_executable)" show-groups 2> /dev/null)//$'\r'/}"
  _paket_command_successful $? || return 1

  # Split output on \n, creating array of lines.
  groups=(${${(f)output}})

  # Remove first and last two lines (paket version and performance), sort elements.
  groups=(${(i)groups[2,-3]})

  _wanted paket-groups expl 'paket group' compadd $groups
}

#(( $+functions[_paket_command_successful] )) ||
_paket_command_successful () {
  if (( ${#*:#0} > 0 )); then
    _message "paket invocation failed with exit status $1"
    return 1
  fi
  return 0
}

#(( $+functions[_paket_executable] )) ||
_paket_executable() {
  local -a locations
  locations=(
    ./.paket/$service
    ./$service
  )

  if [[ $OS != 'Windows_NT' ]]; then
    local mono=mono
  fi

  local location
  for location in $locations; do
    [[ -f "$location" ]] && echo $mono $location && return
  done

  return 1
}

_paket "$@"

# vim: ft=zsh sw=2 ts=2 et
