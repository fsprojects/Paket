namespace Paket.ProjectJson

open Newtonsoft.Json
open System.Collections.Generic
open Newtonsoft.Json.Linq
open System.Text
open System


type ProjectJsonProperties = {
      [<JsonProperty("dependencies")>]
      Dependencies : Dictionary<string, string>

      [<JsonExtensionData>]
      mutable AdditionalData: IDictionary<string, JToken>
    }

type ProjectJsonProject(text:string) =
    
    let findPos (property:string) =
        let needle = sprintf "\"%s\"" property
        match text.IndexOf needle with
        | -1 -> text.Length - 1,text.Length - 1
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


            start,!pos

    member this.WithDependencies dependencies =
        let dependencies = Map.ofSeq dependencies
        let start,endPos = findPos "dependencies"
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
            if Map.isEmpty dependencies then
                sb.Append "{ }"
            else
                sb.AppendLine "{" |> ignore
                let indent = "".PadLeft (max 2 (getIndent() + 3))
                let i = ref 1
                let n = dependencies.Count
                for d in dependencies do
                    let line = sprintf "\"%s\": \"%O\"%s" d.Key d.Value (if !i < n then "," else "")

                    sb.AppendLine(indent + line) |> ignore
                    incr i
                sb.Append(indent.Substring(4) +  "}")

        sb.Append(text.Substring(endPos)) |> ignore

        ProjectJsonProject(sb.ToString())

    override __.ToString() = text