namespace Paket.ProjectJson

open Newtonsoft.Json
open System.Collections.Generic
open Newtonsoft.Json.Linq
open System.Text
open System
open System.IO
open Paket
open System
open Paket.Domain

type ProjectJsonProperties = {
      [<JsonProperty("dependencies")>]
      Dependencies : Dictionary<string, JToken>
    }

/// Project references inside of project.json files.
type ProjectJsonReference = 
    { Name : PackageName
      Data : string }

    override this.ToString() = sprintf "\"%O\": %s" this.Name this.Data

type ProjectJsonFile(fileName:string,text:string) =
    let parsed = lazy(JsonConvert.DeserializeObject<ProjectJsonProperties>(text))

    let dependencies = 
        lazy(
            match parsed.Force().Dependencies with
            | null -> []
            | dependencies ->
                dependencies
                |> Seq.choose (fun kv -> 
                    let text = kv.Value.ToString()
                    if text.Contains "{" then
                        None
                    else
                        Some(PackageName kv.Key, VersionRequirement.Parse(kv.Value.ToString())))
                |> Seq.toList)

    let interProjectDependencies = 
        lazy(
            match parsed.Force().Dependencies with
            | null -> []
            | dependencies ->
                dependencies
                |> Seq.choose (fun kv -> 
                    let text = kv.Value.ToString(Formatting.None)
                    if text.Contains "{" then
                        Some
                            { Name = PackageName kv.Key
                              Data = text }
                    else
                        None)
                |> Seq.toList)

    let rec findPos (property:string) (text:string) =
        let needle = sprintf "\"%s\"" property
        match text.IndexOf needle with
        | -1 -> 
            if String.IsNullOrWhiteSpace text then findPos property (sprintf "{%s    \"%s\": { }%s}" Environment.NewLine property Environment.NewLine) else
            let i = ref (text.Length - 1)
            let n = ref 0
            while !i > 0 && !n < 2 do
                if text.[!i] = '}' then
                    incr n
                decr i

            if !i = 0 then findPos property (sprintf "{%s    \"%s\": { }%s}" Environment.NewLine property Environment.NewLine) else
            findPos property (text.Substring(0,!i+2) + "," + Environment.NewLine + Environment.NewLine + "    \"" + property + "\": { }" + text.Substring(!i+2))
        | start ->
            let pos = ref (start + needle.Length)
            while text.[!pos] <> '{' do
                incr pos

            let balance = ref 1
            incr pos
            while !balance > 0 do
                match text.[!pos] with
                | '{' -> incr balance
                | '}' -> decr balance
                |_ -> ()
                incr pos


            start,!pos,text

    member __.FileName = fileName

    member __.GetDependencies() = dependencies.Force()

    member __.GetInterProjectDependencies() = interProjectDependencies.Force()

    member this.WithDependencies dependencies =
        let nuGetDependencies = 
            dependencies 
            |> List.sortByDescending fst
            |> List.map (fun (name,version) -> sprintf "\"%O\": \"[%O]\"" name version)

        let interProjectDependencies = 
            interProjectDependencies.Force()
            |> List.map (fun p -> p.ToString())

        let dependencies = 
            match interProjectDependencies, nuGetDependencies with
            | [],nuGetDependencies -> nuGetDependencies
            | interProjectDependencies,[] -> interProjectDependencies
            | _ -> interProjectDependencies @ [""] @ nuGetDependencies

        let start,endPos,text = findPos "dependencies" text
        let getIndent() =
            let pos = ref start
            let indent = ref 0
            while !pos > 0 && text.[!pos] <> '\r' && text.[!pos] <> '\n' do
                incr indent
                decr pos
            !indent

        let sb = StringBuilder(text.Substring(0,start))
        sb.Append("\"dependencies\": ") |> ignore

        let deps =
            if List.isEmpty dependencies then
                sb.Append "{ }"
            else
                sb.AppendLine "{" |> ignore
                let indent = "".PadLeft (max 4 (getIndent() + 3))
                let i = ref 1
                let n = dependencies.Length
                for d in dependencies do
                    if d = "" then
                        sb.AppendLine("") |> ignore
                        incr i
                    else
                        let line = d + (if !i < n then "," else "")

                        sb.AppendLine(indent + line) |> ignore
                        incr i
                sb.Append(indent.Substring(4) +  "}")

        sb.Append(text.Substring(endPos)) |> ignore

        ProjectJsonFile(fileName,sb.ToString())

    override __.ToString() = text

    member __.Save(forceTouch) =
        if forceTouch || text <> File.ReadAllText fileName then
            File.WriteAllText(fileName,text)

    static member Load(fileName) : ProjectJsonFile =
        ProjectJsonFile(fileName,File.ReadAllText fileName)
