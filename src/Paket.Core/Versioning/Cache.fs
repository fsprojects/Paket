namespace Paket

open System
open System.IO
open System.Text.RegularExpressions

type CacheType = 
    | AllVersions
    | CurrentVersion

type Cache = { 
    Location : string
    CacheType : CacheType option 
} with
    member this.BaseOnRoot root = 
        if Path.IsPathRooted this.Location && not(String.IsNullOrWhiteSpace root) then 
            this 
        else 
            { this with Location = Path.Combine(root,this.Location) |> normalizePath }

    static member Parse(line : string) =
        let sourceRegex = Regex("cache[ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase)
        let parts = line.Split ' '
        let source = 
            if sourceRegex.IsMatch line then
                sourceRegex.Match(line).Groups.[1].Value.TrimEnd([| '/' |])
            else
                parts.[1].Replace("\"","").TrimEnd([| '/' |])

        let rest =
            let start = line.IndexOf source + source.Length
            line.Substring(start)

        let kvPairs = parseKeyValuePairs (rest.ToLower())

        let getPair key =
            match kvPairs.TryGetValue key with
            | true, x -> kvPairs.Remove key |> ignore; Some x
            | _ -> None

        let settings = { 
            Location = normalizeFeedUrl (normalizeHomeDirectory source)
            CacheType = 
                match getPair "versions" with
                | Some "current" -> Some CacheType.CurrentVersion
                | Some "all" -> Some CacheType.AllVersions
                | _ -> None 
        }

        for kv in kvPairs do
            failwithf "Unknown package settings %s: %s" kv.Key kv.Value

        settings


module Cache =
    let private lockObj = System.Object()
    let private inaccessibleCaches = System.Collections.Generic.HashSet<Cache>()
    let setInaccessible cache =
        lock lockObj (fun () -> inaccessibleCaches.Add cache |> ignore)

    let isInaccessible cache =
        lock lockObj (fun () -> inaccessibleCaches.Contains cache)
