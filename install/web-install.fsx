// This script downloades and saves the latest paket bootstrapper (in magic mode) into ./.paket/ . 

open System
open System.IO
open System.Net
open System.Text.RegularExpressions

let paketDir = Path.Combine [|Environment.CurrentDirectory; ".paket"|]
let paketExe = Path.Combine [|paketDir; "paket.exe"|]
let bootstrapperExeRegex = new Regex(@"""browser_download_url"":\s*""(?<url>[^""]*/paket.bootstrapper.exe)""")

let latestBootstrapperUrl (wc: WebClient) =
    wc.Headers.Add ("user-agent", "fsharp-script")
    let json = wc.DownloadString "https://api.github.com/repos/fsprojects/Paket/releases/latest"
    (bootstrapperExeRegex.Match json).Groups.["url"].Value

let main () =
    use wc = new WebClient ()
    printfn "Fetching latest paket release..."
    let url = latestBootstrapperUrl wc
    printfn "Saving paket bootstrapper as paket.exe (magic mode) in %s..." paketDir
    Directory.CreateDirectory paketDir |> ignore
    wc.DownloadFile (url, paketExe)
    printfn "Done. Paket should now be working."

main ()
