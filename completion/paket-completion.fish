complete --command paket --exclusive --condition __fish_use_subcommand --arguments add
complete --command paket --exclusive --condition __fish_use_subcommand --arguments auto-restore
complete --command paket --exclusive --condition __fish_use_subcommand --arguments clear-cache
complete --command paket --exclusive --condition __fish_use_subcommand --arguments config
complete --command paket --exclusive --condition __fish_use_subcommand --arguments convert-from-nuget
complete --command paket --exclusive --condition __fish_use_subcommand --arguments find-package-versions
complete --command paket --exclusive --condition __fish_use_subcommand --arguments find-packages
complete --command paket --exclusive --condition __fish_use_subcommand --arguments find-refs
complete --command paket --exclusive --condition __fish_use_subcommand --arguments generate-load-scripts
complete --command paket --exclusive --condition __fish_use_subcommand --arguments init
complete --command paket --exclusive --condition __fish_use_subcommand --arguments install
complete --command paket --exclusive --condition __fish_use_subcommand --arguments outdated
complete --command paket --exclusive --condition __fish_use_subcommand --arguments pack
complete --command paket --exclusive --condition __fish_use_subcommand --arguments push
complete --command paket --exclusive --condition __fish_use_subcommand --arguments remove
complete --command paket --exclusive --condition __fish_use_subcommand --arguments restore
complete --command paket --exclusive --condition __fish_use_subcommand --arguments show-groups
complete --command paket --exclusive --condition __fish_use_subcommand --arguments show-installed-packages
complete --command paket --exclusive --condition __fish_use_subcommand --arguments simplify
complete --command paket --exclusive --condition __fish_use_subcommand --arguments update
complete --command paket --exclusive --condition __fish_use_subcommand --arguments why


complete --command paket --exclusive --long help
complete --command paket --long log-file
complete --command paket --long silent
complete --command paket --long verbose
complete --command paket --exclusive --long version


complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long clean-redirects
complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long create-new-binding-files
complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long force
complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long group
complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long interactive
complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long keep-major
complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long keep-minor
complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long keep-patch
complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long no-install
complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long project
complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long redirects
complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long touch-affected-refs
complete -c paket -A -f -n '__fish_seen_subcommand_from add' --long version
