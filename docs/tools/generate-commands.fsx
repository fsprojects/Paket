/// Writes the docs/content/paket-<command>.md pages from the Argu parsers of the published paket.dll
#I "../../bin/net10.0"
#r "Argu.dll"
#r "Paket.Core.dll"
#r "paket.dll"

open System.IO

let MaxCodeWidth = 100

let content = Path.Combine(__SOURCE_DIRECTORY__, "../content")

let verboseOption = """

If you add the `--verbose` flag Paket will run in verbose mode and show detailed information.

With `--log-file [path]` you can trace the logged information into a file.

"""

// the hidden commands are the obsolete ones, which paket --help doesn't list either
for command in Paket.Commands.getAllCommands () |> List.filter (fun c -> not (c.ParentInfo |> Option.get).IsHidden.Value) do
    let name = (command.ParentInfo |> Option.get).Name.Value
    let optFile = Path.Combine(__SOURCE_DIRECTORY__, "../commands", name + ".md")
    let additionalText =
        if File.Exists optFile
        then verboseOption + File.ReadAllText optFile
        else verboseOption

    File.WriteAllText(Path.Combine(content, sprintf "paket-%s.md" name), Paket.Commands.markdown command MaxCodeWidth additionalText)
