#compdef paket=paket.exe paket.exe

# Zsh completion script for Paket (https://github.com/fsprojects/Paket/).
#
# This script is based on the excellent git complection in zsh. Many thanks
# to its authors!
#
#
# INSTALLATION
#
# 1. Download the paket-completion.zsh file to a subdirectory of your home
#    directory, preferably a directory with other function files.
# 2. Rename it to _paket.
# 3. Add the directory to your zsh fpath before running compinit.
#
#   $ target="$HOME/.zsh-completions"
#   $ mkdir "$target"
#   $ curl --fail --location --proto-redir -all,https --output "$target/_paket" https://raw.githubusercontent.com/fsprojects/Paket/master/completion/paket-completion.zsh
#
# In your ~/.zshrc:
#
#   fpath=($HOME/directory/where/_paket/resides $fpath)
#
#
# UPDATING AN EXISTING INSTALLATION
#
# Just repeat the download from above.
#
# Alternatively the completion script comes bundled with the
# paket-completion-update function that will download the current
# paket-completion.zsh to the same file as above, even if you changed its
# location after the initial download.
#
# Please note: The paket-completion-update function requires curl to be
# installed. paket-completion-update is *only available after your
# first paket completion* per shell session (i.e. after the Paket completion
# functions have been autoloaded by zsh).
#
# paket-completion-update supports an optional first parameter that allows you
# to override the default download root URL which is
# https://raw.githubusercontent.com/fsprojects/Paket/master/completion.
#
#
# PAKET ALIAS
#
# For easier consumption of Paket (without paket.sh or paket.cmd) it is advised
# to create an alias and always run Paket from the repository root.
#
# Also have a look at Paket's magic mode.
# https://fsprojects.github.io/Paket/bootstrapper.html#Magic-mode
#
# Somewhere in your ~/.zshrc:
#
#   if [[ "$OS" != Windows* ]]; then
#     alias paket='mono ./.paket/paket.exe'
#   else
#     alias paket='./.paket/paket.exe'
#   fi
#
# Also ensure that zsh completes aliases based on the expanded alias contents.
# http://zsh.sourceforge.net/Doc/Release/Options.html#index-COMPLETEALIASES
#
#   unsetopt completealiases
#
# If you don't like alias completion, define that the paket alias should be
# completed using the _paket function defined in this file.
#
#   setopt completealiases
#   compdef _paket paket
#
#
# MONO
#
# If you use Mono (e.g. on Linux or macOS) and do not have Mono completion
# installed, you need to define that mono invokes other programs:
#
#   compdef _precommand mono
#
# This is similar to `nohup` invoking the "real" program that needs to be
# completed. More details: https://unix.stackexchange.com/a/178054/72946
#
# For an exemplar mono completion, have a look here:
# https://github.com/agross/dotfiles/tree/master/mono/functions/_mono
#
#
# CONFIGURATION
#
# You can configure some aspects of Paket completion. Add these to your
# ~/.zshrc.
#
# Define where to look for paket.exe
#
#   Depending on what should be completed the Paket executable will be run by
#   the completion script. The completion script searches for a local
#   installation at ./.paket/paket.exe first. Local installs will be prepended
#   with mono unless you are running Windows. Global installations will be
#   looked at last (i.e. paket in the $PATH). They are not prepended with
#   mono.
#
#   To override the list of possible locations for local installations of
#   paket.exe, define the following `paket-executable` style. The list of values
#   will be searched as defined.
#
#     # This is the default.
#     zstyle ':completion::complete:paket:*' paket-executable './.paket/paket.exe'
#     # Useful for Paket developers: Prefer the locally built version in bin over the one in .paket.
#     zstyle ':completion::complete:paket:*' paket-executable './bin/paket.exe' './.paket/paket.exe'
#
#
# Enable infix matching for package IDs
#
#   By default paket find-packages will match infixes, which is not the default
#   mode for Paket completion. I think it's unnatural to type
#     paket add FAK<tab>
#   which will be completed to
#     paket add Altairis.Fakturoid.Client
#   if infix matching is enabled.
#
#   If you want this behavior, enable infix matches:
#
#     zstyle ':completion::complete:paket:*' infix-match yes
#     zstyle ':completion::complete:paket:add:*' infix-match yes # Only for paket add.
#
#
# Disable fallback (i.e. default zsh) completion for Paket commands that do not
# have a completion function:
#
#   zstyle ':completion::complete:paket:*' use-fallback no
#
#
# Disable verbose completion of main commands:
#
#   zstyle ':completion::complete:paket:*' verbose no
#
#
# Define the zsh completion cache policy for expensive operations that generate
# completions.
#
#   Paket will e.g. issue HTTP requests when you complete a package ID or a
#   version number. These take time so we take advantage of the zsh
#   completion cache and store such results.
#
#   Enable the zsh completion cache globally, including completions for other
#   commands that also leverage caching:
#
#     zstyle ':completion:*' use-cache on
#     zstyle ':completion:*' cache-path instead/of/$HOME/.zcompcache # Optional.
#
#   To disable the cache for specific Paket commands to always get fresh
#   results when completing e.g. paket add:
#
#     zstyle ':completion::complete:paket:add:*' use-cache off
#
#   The caches are stored under the cache-path as follows:
#
#     <cache-path>/paket/<expensive command>/<parameter>
#
#   e.g.
#
#     <cache-path>/paket/find-packages/fak
#
#   if you completed 'paket find-packages FAK' or 'paket find-packages fak'
#   before.
#
#   The default cache policy caches results for 1 day (see _paket_cache_policy).
#   To remove cached results you can either delete the
#   <cache-path>/paket directory or provide a custom cache policy to control
#   cache expiration for all or specific Paket commands:
#
#     zstyle ':completion::complete:paket:*' cache-policy _default_cache_policy
#     zstyle ':completion::complete:paket:find-packages:*' cache-policy _strict_cache_policy
#
#     _default_cache_policy () {
#       # Rebuild if the cache is more than a week old.
#       local file="$1"
#       local -a outdated
#       # See http://zsh.sourceforge.net/Doc/Release/Expansion.html#Glob-Qualifiers
#       outdated=( "$file"(Nm+7) )
#       (( $#outdated ))
#     }
#
#     _strict_cache_policy () {
#       return 0 # 0 == always outdated, you should better use use-cache off.
#   }
#
#   There are two special default cache policies for completions relying on the
#   existence of local files, e.g. paket.dependencies or paket.lock.
#   For Paket commands that read these, the cache is invalidated as soon as the
#   file's modification time is newer than the cache.
#   (See _paket_cache_policy_dependencies_file and
#   _paket_cache_policy_lock_file.)
#
#
# Disable running Paket to get packages, versions etc. as completion arguments
#
#   Disable globally:
#
#     zstyle ':completion::complete:paket:*' disable-completion yes
#
#   Disable only a single means to get completion values:
#
#     # Used by e.g. paket add:
#     zstyle ':completion::complete:paket:find-packages:' disable-completion yes
#     zstyle ':completion::complete:paket:find-package-versions:' disable-completion yes
#     zstyle ':completion::complete:paket:show-groups:' disable-completion yes
#
#     # Used by e.g. paket why:
#     zstyle ':completion::complete:paket:show-installed-packages:' disable-completion yes
#
#
# Custom feed URLs for --source and paket push --url argument
#
#   Define additional sources that will be completed for all Paket commands:
#
#     zstyle ':completion::complete:paket:*' sources 'http://one.example.com/feed/v2'
#     zstyle ':completion::complete:paket:*' sources \
#            'http://one.example.com/feed/v2' \
#            'http://second.example.com/feed/v2'
#
#   Override list for a specific command:
#
#     zstyle ':completion::complete:paket:find-package-versions:*' sources \
#            'http://another.example.com/feed/v2'
#     zstyle ':completion::complete:paket:push:*' sources \
#            'https://myget.org/F/my-feed-name'

_paket() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  # Strip .exe extension.
  curcontext="${curcontext%.*}:"

  # Set up default cache policy.
  local -A default_policies
  default_policies[${curcontext}*]=_paket_cache_policy
  default_policies[${curcontext}*:*:show-groups:]=_paket_cache_policy_dependencies_file
  default_policies[${curcontext}*:*:show-installed-packages:]=_paket_cache_policy_lock_file

  local key cache_policy
  for key in "${(@kO)default_policies}"; do
    local ctx="$key"
    local policy="${default_policies[$key]}"

    zstyle -s ":completion:$ctx" cache-policy cache_policy
    if [[ -z "$cache_policy" ]]; then
      zstyle ":completion:$ctx" cache-policy $policy
    fi
  done

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
  && ret=0

  case "$state" in
    (command)
      _paket_commands && ret=0
      ;;

    (option-or-argument)
      # Construct :complete:paket:<command>:.
      curcontext="${curcontext%:*}:${words[1]}:"

      if ! _call_function ret "_paket-${words[1]}"; then
        _message "Completion for Paket command '${words[1]}' is not implemented, please contact @agross"

        if zstyle -T ":completion:$curcontext:" use-fallback; then
          _default && ret=0
        fi
      fi
      ;;
  esac

  return ret
}

(( $+functions[_paket_group_option] )) ||
_paket_group_option() {
  print -l '(--group -g)'{--group,-g}"[$1]:group:_paket_groups"
}

(( $+functions[_paket_source_url] )) ||
_paket_source_url() {
  local state="$1" curcontext="${2%:*}" ret=1

  case $state in
    (source-url)
      local -a user_sources
      zstyle -a ":completion:$curcontext:" sources user_sources

      local -a args
      args=(
        'source::_paket_sources'
        'NuGet.org feed:NuGet.org feed:(https://www.nuget.org/api/v2 https://api.nuget.org/v3/index.json)'
        "user-defined feed:user-defined feed:($user_sources)"
        'urls::_urls'
      )

      _alternative \
        $args \
      && ret=0
    ;;
  esac

  return ret
}

(( $+functions[_paket-add] )) ||
_paket-add() {
  local curcontext=$curcontext context state state_descr ret=1
  typeset -A opt_args
  local -a line

  local -a args
  args=(
    $global_options
    $keep_options
    $binding_redirects_options
    $download_options
    '(--interactive -i)'{--interactive,-i}"[ask for every project whether to add the dependency]"
    '(--no-install)'--no-install'[do not modify projects]'
    '(--project -p)'{--project,-p}'[add the dependency to a single project only]:project:_path_files -g "**/*.??proj"'
    '(--version -V)'{--version,-V}'[dependency version constraint]: :->version'
    "${(f)$(_paket_group_option 'add the dependency to a group (default: Main group)')}"
  )

  _arguments -C \
    $args \
    ':NuGet package ID:->package-id' \
  && return

  case $state in
    (package-id)
      _paket_packages && ret=0
      ;;

    (version)
      _paket_version_constraint && ret=0
      ;;
  esac

  return ret
}

(( $+functions[_paket-auto-restore] )) ||
_paket-auto-restore() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
  )

  _arguments -C \
    $args \
    '1: :->mode' \
  && ret=0

  case $state in
    (mode)
      local -a modes

      modes=(
        'on:enable automatic restore'
        'off:disable automatic restore'
      )

      _describe -t modes mode modes \
      && ret=0
      ;;
  esac

  return ret
}

(( $+functions[_paket-clear-cache] )) ||
_paket-clear-cache() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
  )

  _arguments -C \
    $args \
  && ret=0

  return ret
}

(( $+functions[_paket-config] )) ||
_paket-config() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
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
        'add-credentials:add credentials for URL or credential key'
        'add-token:add token for URL or credential key'
      )

      _describe -t commands command commands \
      && ret=0
      ;;

    (option-or-argument)
      curcontext=${curcontext%:*}-$line[1]:

      case $line[1] in
        (add-credentials)
          _arguments -C \
            $args \
            '(--username)'--username'[provide username]:user name: ' \
            '(--password)'--password'[provide password]:password: ' \
            '1: :->source-url-or-credential-key' \
          && ret=0

          case $state in
            (source-url-or-credential-key)
              _alternative \
                'source::_paket_sources' \
                'credential key::_paket_credential_keys' \
                'urls::_urls' \
              && ret=0
            ;;
          esac
          ;;

        (add-token)
          _arguments -C \
            $args \
            '1: :->source-url-or-credential-key' \
            '2:token' \
          && ret=0

          case $state in
            (source-url-or-credential-key)
              _alternative \
                'source::_paket_sources' \
                'credential key::_paket_credential_keys' \
                ' :URL to set NuGet.org API key:(https://www.nuget.org)' \
                'urls::_urls' \
              && ret=0
            ;;
          esac
          ;;
      esac
  esac

  return ret
}

(( $+functions[_paket-convert-from-nuget] )) ||
_paket-convert-from-nuget() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
    '(-f --force)'{-f,--force}'[force the conversion even if paket.dependencies or paket.references files are present]'
    '(--no-install)'--no-install'[do not modify projects]'
    '(--no-auto-restore)'--no-auto-restore"[do not enable automatic package restore]"
    '(--migrate-credentials)'--migrate-credentials"[specify mode for NuGet source credential migration (default: encrypt)]:credential migration mode:((\
      encrypt\:'store encrypted in paket.config (default)' \
      plaintext\:'store as plain text in paket.dependencies' \
      selective\:'be asked for every feed'))"
  )

  _arguments -C \
    $args \
  && ret=0

  return ret
}

(( $+functions[_paket-find-refs] )) ||
_paket-find-refs() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
    "${(f)$(_paket_group_option 'specify dependency group (default: Main group)')}"
  )

  _arguments -C \
    $args \
    '*: :->package-id' \
  && ret=0

  case $state in
    (package-id)
      local group=${(v)opt_args[(i)--group|-g]:-Main}

      _paket_installed_packages $group \
      && ret=0
      ;;
  esac

  return ret
}

(( $+functions[_paket-find-package-versions] )) ||
_paket-find-package-versions() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
    '(--source)'--source'[specify source URL]: :->source-url'
    '(--max)'--max'[limit maximum number of results]:maxiumum results:(1 5 10 50 100 1000)'
  )

  _arguments -C \
    $args \
    ':NuGet package ID:_paket_packages' \
  && ret=0

  _paket_source_url "$state" "$curcontext" && ret=0

  return ret
}

(( $+functions[_paket-find-packages] )) ||
_paket-find-packages() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
    '(--source)'--source'[specify source URL]: :->source-url'
    '(--max)'--max'[limit maximum number of results]:maxiumum results:(1 5 10 50 100 1000)'
  )

  _arguments -C \
    $args \
    ':NuGet package ID:_paket_packages' \
  && ret=0

  _paket_source_url "$state" "$curcontext" && ret=0

  return ret
}

(( $+functions[_paket-generate-load-scripts] )) ||
_paket-generate-load-scripts() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
    '*'{-f,--framework}'[framework identifier to generate scripts for]:framework: '
    '*'{-t,--type}'[language to generate scripts for]: :->language'
    '*'{-g,--group}'[group to generate scripts for (default: all groups)]: :->group'
  )

  _arguments -C \
    $args \
  && ret=0

  case $state in
    (language)
      local -a languages
      languages=(${(v@Qs_:_)opt_args[(I)--type|-t]})

      _paket_languages -F languages \
      && ret=0
      ;;

    (group)
      local -a groups
      # Search --group and -g optargs for values (v), make array (@) and
      # remove quotes (Q) and split by : (s_:_).
      groups=(${(v@Qs_:_)opt_args[(I)--group|-g]})

      _paket_groups -F groups \
      && ret=0
      ;;
  esac

  return ret
}

(( $+functions[_paket-init] )) ||
_paket-init() {
  _arguments $global_options
}

(( $+functions[_paket-install] )) ||
_paket-install() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
    $keep_options
    $binding_redirects_options
    $download_options
    '(--only-referenced)'--only-referenced'[only install dependencies listed in paket.references files, instead of all packages in paket.dependencies]'
    '(--generate-load-scripts)'--generate-load-scripts'[generate F# and C# include scripts that reference installed packages in a interactive environment like F# Interactive or ScriptCS]'
    '*--load-script-framework[framework identifier to generate scripts for]:framework: '
    '*--load-script-type[language to generate scripts for]: :->language'
  )

  _arguments -C \
    $args \
  && ret=0

  case $state in
    (language)
      local -a languages
      languages=(${(vQs_:_)opt_args[(i)--load-script-type]})

      _paket_languages -F languages \
      && ret=0
      ;;
  esac

  return ret
}

(( $+functions[_paket-outdated] )) ||
_paket-outdated() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
    "${(f)$(_paket_group_option 'specify dependency group (default: all groups)')}"
    '(-f --force)'{-f,--force}'[force download and reinstallation of all dependencies]'
    '(--ignore-constraints)'--ignore-constraints'[ignore version constraints in the paket.dependencies file]'
    '(--pre --include-prereleases)'{--pre,--include-prereleases}'[consider prerelease versions as updates]'
  )

  _arguments -C \
    $args \
  && ret=0

  return ret
}

(( $+functions[_paket-pack] )) ||
_paket-pack() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
    '(--build-config)'--build-config'[build configuration that should be packaged (default: Release)]:build configuration: '
    '(--build-platform)'--build-platform'[build platform that should be packaged (default: check all known platform targets)]:build platform: '
    '(--version)'--version'[version of the package]:version: '
    '(--template)'--template'[pack a single paket.template file]:template:_path_files -g "**/paket.template"'
    '*--exclude[exclude paket.template file by package ID]: :->template-id'
    '*--specific-version[version number to use for package ID]: :->specific-version'
    '(--release-notes)'--release-notes'[release notes]:release notes: '
    '(--lock-dependencies)'--lock-dependencies'[use version constraints from paket.lock instead of paket.dependencies]'
    '(--minimum-from-lock-file)'--minimum-from-lock-file'[use version constraints from paket.lock instead of paket.dependencies and add them as a minimum version; --lock-dependencies overrides this option]'
    '(--pin-project-references)'--pin-project-references'[pin dependencies generated from project references to exact versions (=) instead of using minimum versions (>=); with --lock-dependencies project references will be pinned even if this option is not specified]'
    '(--symbols)'--symbols'[create symbol and source packages in addition to library and content packages]'
    '(--include-referenced-projects)'--include-referenced-projects'[include symbols and source from referenced projects]'
    '(--project-url)'--project-url'[homepage URL for the package]:URL:_urls'
  )

  _arguments -C \
    $args \
    ':output directory for .nupkg files:_directories' \
  && ret=0

  case $state in
    (template-id)
      local -a ids
      ids=(${(vQs_:_)opt_args[(i)--exclude]})

      _paket_template_ids -F ids \
      && ret=0
      ;;

    (specific-version)
      _message 'TODO, not implemented' \
      && ret=0
      ;;
  esac

  return ret
}

(( $+functions[_paket-push] )) ||
_paket-push() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
    '(--url)'--url'[URL of the NuGet feed]: :->url'
    '(--api-key)'--api-key'[API key for the URL (default: value of the NUGET_KEY environment variable)]:API key: '
    '(--endpoint)'--endpoint'[API endpoint to push to (default: /api/v2/package))]:endpoint: '
  )

  _arguments -C \
    $args \
    '1:NuGet package:_path_files -g "(^packages/)#/*.nupkg"' \
  && ret=0

  case $state in
    (url)
      local -a user_sources
      zstyle -a ":completion:$curcontext:" sources user_sources

      local -a args
      args=(
        'source::_paket_sources'
        'NuGet.org feed:NuGet.org feed:(https://www.nuget.org/)'
        "user-defined feed:user-defined feed:($user_sources)"
        'urls::_urls'
      )

      _alternative \
        $args \
      && ret=0
      ;;
  esac

  return ret
}

(( $+functions[_paket-remove] )) ||
_paket-remove() {
  local curcontext=$curcontext context state state_descr ret=1
  typeset -A opt_args
  local -a line

  local -a args
  args=(
    $global_options
    '(-f --force)'{-f,--force}'[force download and reinstallation of all dependencies]'
    '(--interactive -i)'{--interactive,-i}"[ask for every project whether to remove the dependency]"
    '(--no-install)'--no-install'[do not modify projects]'
    '(--project -p)'{--project,-p}'[remove the dependency from a single project only]:project:_path_files -g "**/*.??proj"'
    "${(f)$(_paket_group_option 'remove the dependency from a group (default: Main group)')}"
  )

  _arguments -C \
    $args \
    ':NuGet package ID:->package-id' \
  && return

  case $state in
    (package-id)
      local group=${(v)opt_args[(i)--group|-g]}

      _paket_installed_packages $group \
      && ret=0
      ;;
  esac

  return ret
}

(( $+functions[_paket-restore] )) ||
_paket-restore() {
  local curcontext=$curcontext context state state_descr ret=1
  typeset -A opt_args
  local -a line

  local -a args
  args=(
    $global_options
    $download_options
    '*--references-file[restore packages from a paket.references file]:paket.references:_path_files -g "**/*paket.references"'
    '(--only-referenced)'--only-referenced'[only restore packages that are referenced by paket.references files]'
    '(--project -p)'{--project,-p}'[restore dependencies of a single project]:project:_path_files -g "**/*.??proj"'
    '(--target-framework)'--target-framework'[restore only for the specified target framework]:target framework: '
    '(--ignore-checks --fail-on-checks)'--ignore-checks'[do not check if paket.dependencies and paket.lock are in sync]'
    '(--ignore-checks --fail-on-checks)'--fail-on-checks'[abort if any checks fail]'
    "${(f)$(_paket_group_option 'restore dependencies of a single group')}"
  )

  _arguments -C \
    $args \
  && return

  return ret
}

(( $+functions[_paket-show-groups] )) ||
_paket-show-groups() {
  _arguments $global_options
}

(( $+functions[_paket-show-installed-packages] )) ||
_paket-show-installed-packages() {
    local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
    '(-p --project)'{-p,--project}'[specify project to show dependencies for]:project:_path_files -g "**/*.??proj"'
    '(-a --all)'--all'[include transitive dependencies]'
  )

  _arguments -C \
    $args \
  && ret=0

  return ret
}

(( $+functions[_paket-simplify] )) ||
_paket-simplify() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
    '(-i --interactive)'{-i,--interactive}'[confirm deletion of every transitive dependency]'
  )

  _arguments -C \
    $args \
  && ret=0

  return ret
}

(( $+functions[_paket-update] )) ||
_paket-update() {
  local curcontext=$curcontext context state state_descr ret=1
  typeset -A opt_args
  local -a line

  local -a args
  args=(
    $global_options
    $keep_options
    $binding_redirects_options
    $download_options
    '(--filter)'--filter'[treat the NuGet package ID as a regex to filter packages]'
    '(--no-install)'--no-install'[do not modify projects]'
    '(--version -V)'{--version,-V}'[dependency version constraint]: :->version'
    "${(f)$(_paket_group_option 'specify dependency group to update (default: all groups)')}"
  )

  _arguments -C \
    $args \
    ':NuGet package ID:->package-id' \
  && return

  case $state in
    (package-id)
      local group=${(v)opt_args[(i)--group|-g]}

      _paket_installed_packages $group \
      && ret=0
      ;;

    (version)
      _paket_version_constraint && ret=0
      ;;
  esac

  return ret
}

(( $+functions[_paket-why] )) ||
_paket-why() {
  local curcontext=$curcontext context state state_descr line ret=1
  typeset -A opt_args

  local -a args
  args=(
    $global_options
    '(--details)'--details'[display detailed information with all paths, versions and framework restrictions]'
    "${(f)$(_paket_group_option 'specify dependency group (default: Main group)')}"
  )

  _arguments -C \
    $args \
    ': :->package-id' \
  && ret=0

  case $state in
    (package-id)
      local group=${(v)opt_args[(i)--group|-g]:-Main}

      _paket_installed_packages $group \
      && ret=0
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
    NuGet
    misc
  )

  for type in $types; do
    local -a $type
  done

  dependency=(
    add:'add a new dependency'
    install:'download dependencies and update projects'
    outdated:'find dependencies that have newer versions available'
    remove:'remove a dependency'
    restore:'download the computed dependency graph'
    simplify:'simplify declared dependencies by removing transitive dependencies'
    update:'update dependencies to their latest version'
  )

  inspection=(
    find-packages:'search for NuGet packages'
    find-package-versions:'search for dependency versions'
    find-refs:'find all project files that have a dependency installed'
    show-groups:'show groups'
    show-installed-packages:'show installed dependencies'
    why:'determine why a dependency is required'
  )

  NuGet=(
    convert-from-nuget:'convert projects from NuGet to Paket'
    pack:'create NuGet packages from paket.template files'
    push:'push a NuGet package'
  )

  misc=(
    auto-restore:'manage automatic package restore during the build process inside Visual Studio'
    clear-cache:'clear the NuGet and git cache directories'
    config:'store global configuration values like NuGet credentials'
    generate-load-scripts:'generate F# and C# include scripts that reference installed packages in a interactive environment like F# Interactive or ScriptCS'
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
  compadd "$expl[@]" -O all_matching_commands -a -- all_commands
  # Length of longest match.
  longest_match=${#${(O)all_matching_commands//?/.}[1]}

  # Verbose/long display requested?
  local -a disp
  if zstyle -T ":completion:$curcontext:" verbose; then
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

    alternatives+=("${type}:$type command:compadd ${(e)disp} -a -- ${type}_commands")
  done

  _alternative $alternatives
}

(( $+functions[_paket_packages] )) ||
_paket_packages() {
  local cmd=find-packages
  local what='NuGet package ID'

  if ! _paket_should_run $cmd; then
    _message "Enter $what"
    return
  fi

  local package_id="${words[$CURRENT]}"
  local cache_id="${service%.*}/$cmd/${(L)package_id:-_}"

  if ! _retrieve_cache $cache_id; then
    # We need to replace CR, in case we're running on Windows (//$'\r'/).
    local -a output
    output=(
      ${(f)"$(_call_program $cmd \
              "$(_paket_executable) $cmd --silent --max 100 '$package_id' 2> /dev/null")"//$'\r'/}
      )
    _paket_command_successful $? || return 1

    _store_cache $cache_id output
  fi

  local infix_match
  local -a compadd_args
  if zstyle -b ":completion:${curcontext}" infix-match infix_match; then
    compadd_args=(-U)
  fi

  local expl
  _wanted paket-packages expl $what \
    compadd $compadd_args \
    -M 'm:{[:lower:][:upper:]}={[:upper:][:lower:]}' -a - output
}

(( $+functions[_paket_package_versions] )) ||
_paket_package_versions() {
  local cmd=find-package-versions
  local what='version'

  if ! _paket_should_run $cmd; then
    _message "Enter $what"
    return
  fi

  local package_id="${line[1]}"
  if [[ -z "$package_id" ]]; then
    _message 'Cannot complete version without NuGet package ID'
    return 1
  fi
  local constraint="$IPREFIX"

  local cache_id="${service%.*}/$cmd/${(L)package_id:-_}"

  if ! _retrieve_cache $cache_id; then
    # We need to replace CR, in case we're running on Windows (//$'\r'/).
    local -a output
    output=(
      ${(f)"$(_call_program $cmd \
              "$(_paket_executable) $cmd --silent --max 100 '$package_id' 2> /dev/null")"//$'\r'/}
      )
    _paket_command_successful $? || return 1

    _store_cache $cache_id output
  fi

  # If the there is a constraint, also complete fake values,
  # e.g. 1.2.3 adds 1.2 and 1.
  local -a fake_versions
  if [[ -n "$constraint" ]]; then
    local version index
    for version in $output; do
      local -a parts=(${(s:.:)version})

      for (( index = 1; index <= $#parts; index++ )); do
        local -a up_to_index=(${parts[1,index]})
        version=${(j:.:)up_to_index}
        fake_versions+=$version
      done
    done

    # Remove fake versions that are real versions.
    fake_versions=(${fake_versions:|output})
  fi

  local expl
  _wanted paket-package-versions expl "$what for $package_id" \
    compadd -a -- output
  _wanted paket-fake-package-versions expl "fake $what for $package_id" \
    compadd -n -a -- fake_versions
}

(( $+functions[_paket_groups] )) ||
_paket_groups() {
  local cmd=show-groups
  local what='group'

  if ! _paket_should_run $cmd; then
    _message "Enter $what"
    return
  fi

  # Modify context for cache retrieval only.
  local curcontext="$curcontext:$cmd"
  local cache_id="${service%.*}/$cmd/${PWD//\//-}"
  if ! _retrieve_cache $cache_id; then
    # We need to replace CR, in case we're running on Windows (//$'\r'/).
    local -a output
    output=(
      ${(f)"$(_call_program $cmd \
              "$(_paket_executable) $cmd --silent 2> /dev/null")"//$'\r'/}
      )
    _paket_command_successful $? || return 1

    _store_cache $cache_id output
  fi

  local expl
  _wanted paket-groups expl $what compadd -a $@ -- output
}

(( $+functions[_paket_installed_packages] )) ||
_paket_installed_packages() {
  local group="$1"
  local cmd=show-installed-packages

  local what='NuGet package ID'
  [[ -n "$group" ]] && what="$what for group $group"

  if ! _paket_should_run $cmd; then
    _message "Enter $what"
    return
  fi

  # Modify context for cache retrieval only.
  local curcontext="$curcontext:$cmd"
  local cache_id="${service%.*}/$cmd/${PWD//\//-}"
  if ! _retrieve_cache $cache_id; then
    local -a output
    output=(
      ${(f)"$(_call_program $cmd \
              "$(_paket_executable) $cmd --silent --all 2> /dev/null")"}
      )
    _paket_command_successful $? || return 1

    _store_cache $cache_id output
  fi

  # Filter packages by optional group name followed by space.
  [[ -n "$group" ]] && output=(${(M)output:#$group *})

  # Take the second word (the package ID).
  # Format: <group> <package ID> - <version>
  output=(${output/(#m)*/${MATCH[(w)2]}})

  # -F line: exclude $line elements as possible completions (i.e. remove
  # packages already typed).
  local expl
  _wanted paket-installed-packages expl $what compadd -F line -a -- output
}

(( $+functions[_paket_sources] )) ||
_paket_sources() {
  local -a output
  output=(
    ${(f)"$(_call_program sources \
            "grep '^\s*source\s\+\S' paket.dependencies 2> /dev/null")"}
    )
  (( $? == 0 )) || return 1

  # Take the second word (the source URL).
  # Format: source URL ...
  output=(${output/(#m)*/${MATCH[(w)2]}})

  local expl
  _wanted paket-sources expl 'source URL' compadd -a -- output
}

(( $+functions[_paket_credential_keys] )) ||
_paket_credential_keys() {
  local -a output
  output=(
    ${(f)"$(_call_program credential-keys \
            "grep '^\s*github\(\s\+\S\+\)\{3\}' paket.dependencies 2> /dev/null")"}
    )
  (( $? == 0 )) || return 1

  # Take the fourth word (the credential key).
  # Format: github repo file credential-key
  output=(${output/(#m)*/${MATCH[(w)4]}})

  local expl
  _wanted paket-credential-keys expl 'credential key' compadd -a -- output
}

(( $+functions[_paket_template_ids] )) ||
_paket_template_ids() {
  local -a output
  output=(
    ${(f)"$(_call_program template_ids \
            "grep -r '^id\s\+\S' **/*paket.template 2> /dev/null")"}
    )
  (( $? == 0 )) || return 1

  # TODO: Check for 'type project' templates and determine IDs from ??proj.
  # src/Paket.Bootstrapper/paket.template is a good example.

  # Take the second word (the package ID).
  # Format: id package-id
  output=(${output/(#m)*/${MATCH[(w)2]}})

  local expl
  _wanted paket-template-ids expl 'template package ID' compadd -a $@ -- output
}

(( $+functions[_paket_languages] )) ||
_paket_languages() {
  local -a args desc
  args=(
    fsx
    csx
  )
  desc=(
    'fsx for F# Interactive'
    'csx for ScriptCS'
  )

  local expl
  _wanted paket-language expl 'language' compadd -a -ld desc $@ -- args
}

(( $+functions[_paket_strategy_modifiers] )) ||
_paket_strategy_modifiers() {
  local -a args
  args=(
    '@:use maximum versions for transitive dependencies'
    '!:use minimum versions for transitive dependencies'
  )

  _describe -t paket-strategy-modifier 'strategy modifier' args -qS ''
}

(( $+functions[_paket_version_constraints] )) ||
_paket_version_constraints() {
  local -a args
  args=(
    '~>:pessimistic (i.e. ~> 1.0 equals >= 1.0 and < 2.0)'
    '=:pin version'
    '==:exact version'
    '=>:at least'
    '>:greater than'
    '<=:less than or equal'
    '<:less than'
  )

  _describe -t paket-version-constraint 'version constraint' args -qS '\ '
}

(( $+functions[_paket_version_constraint] )) ||
_paket_version_constraint() {
  if compset -P '* '; then
    # For some reason an ! properly escaped on the command line will
    # unescaped when generating more completions.
    [[ "$IPREFIX" == !* ]] && IPREFIX='\!'${IPREFIX[2,-1]}

    _paket_package_versions && ret=0
  elif compset -P '!' || compset -P '@'; then
    # For some reason an ! properly escaped on the command line will
    # unescaped when generating more completions.
    [[ "$IPREFIX" == !* ]] && IPREFIX='\!'${IPREFIX[2,-1]}

    _paket_version_constraints && ret=0
  else
    _alternative \
      "strategy-modifier::_paket_strategy_modifiers" \
      "version-constraint::_paket_version_constraints" \
      "package-version::_paket_package_versions" \
    && ret=0
  fi
}

(( $+functions[_paket_should_run] )) ||
_paket_should_run() {
  local key=":completion:${curcontext%:*:*}:${1?Need type}:"

  local disable_completion
  if zstyle -b "$key" disable-completion disable_completion; then
    return 1
  fi

  return 0
}

(( $+functions[_paket_command_successful] )) ||
_paket_command_successful () {
  if (( $1 > 0 )); then
    _message "${2:-paket} invocation failed with exit status $1"
    return 1
  fi
  return 0
}

(( $+functions[_paket_executable] )) ||
_paket_executable() {
  local -a locations
  if ! zstyle -a ":completion:$curcontext:" paket-executable locations; then
    locations=(./.paket/$service)
  fi

  if [[ $OS != Windows* ]]; then
    local mono=mono
  fi

  local location
  for location in $locations; do
    [[ -f "$location" ]] && printf '%s "%s"' "$mono" "$location" && return
  done

  if (($+commands[paket])); then
    printf '"%s"' "$commands[paket]" && return
  fi

  return 1
}

(( $+functions[_paket_cache_policy] )) ||
_paket_cache_policy() {
  local cache="$1"

  # Rebuild if the cache is more than a day old.
  # See http://zsh.sourceforge.net/Doc/Release/Expansion.html#Glob-Qualifiers
  local -a outdated
  outdated=("$cache"(mh+24))
  (( $#outdated )) && return 0

  # Still fresh!
  return 1
}

(( $+functions[_paket_cache_policy_dependencies_file] )) ||
_paket_cache_policy_dependencies_file() {
  local cache="$1"

  # Rebuild if paket.dependencies is newer than cache.
  if [[ ! -f paket.dependencies || paket.dependencies -nt $cache ]]; then
    return 0
  fi

  return 1
}

(( $+functions[_paket_cache_policy_lock_file] )) ||
_paket_cache_policy_lock_file() {
  local cache="$1"

  # Rebuild if paket.dependencies is newer than cache.
  if [[ ! -f paket.lock || paket.lock -nt $cache ]]; then
    return 0
  fi

  return 1
}

(( $+functions[paket-completion-update] )) ||
paket-completion-update() {
  local download_from="${1:-https://raw.githubusercontent.com/fsprojects/Paket/master/completion}"

  # entries are files under $download_from pointing to local files.
  local -A entries
  # Let the file point to the file defining our function, even if it is
  # symlinked.
  # http://stackoverflow.com/a/28336473/149264
  # https://unix.stackexchange.com/a/136565/72946
  entries[paket-completion.zsh]=${${(%):-%x}:A}

  local key
  for key in "${(@k)entries}"; do
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

_paket "$@"

# vim: ft=zsh sw=2 ts=2 et
