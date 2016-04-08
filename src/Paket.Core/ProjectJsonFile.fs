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

type ProjectJsonFrameworks = {
      [<JsonProperty("frameworks")>]
      Frameworks : Dictionary<string, JToken>
    }

/// Project references inside of project.json files.
type ProjectJsonReference = 
    { Name : PackageName
      Data : string }

    override this.ToString() = sprintf "\"%O\": %s" this.Name this.Data

type ProjectJsonFile(fileName:string,text:string) =
    let getDependencies text =
        let parsed = JsonConvert.DeserializeObject<ProjectJsonProperties>(text)
        match parsed.Dependencies with
        | null -> []
        | dependencies ->
            dependencies
            |> Seq.choose (fun kv -> 
                let text = kv.Value.ToString()
                if text.Contains "{" then
                    None
                else
                    Some(PackageName kv.Key, VersionRequirement.Parse(kv.Value.ToString())))
            |> Seq.toList

    let getInterProjectDependencies text =
        let parsed = JsonConvert.DeserializeObject<ProjectJsonProperties>(text)
    
        match parsed.Dependencies with
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
            |> Seq.toList

    let dependencies = lazy(getDependencies text)

    let dependenciesByFramework = lazy(
        let parsed = JsonConvert.DeserializeObject<ProjectJsonFrameworks>(text)
        parsed.Frameworks
        |> Seq.map (fun kv ->
            kv.Key,getDependencies(kv.Value.ToString()))
        |> Map.ofSeq
    )

    let interProjectDependencies = lazy(getInterProjectDependencies text)

    let interProjectDependenciesByFramework = lazy(
        let parsed = JsonConvert.DeserializeObject<ProjectJsonFrameworks>(text)
        parsed.Frameworks
        |> Seq.map (fun kv ->
            kv.Key,getInterProjectDependencies(kv.Value.ToString()))
        |> Map.ofSeq
    )

    let getIndent (text:string) start =
        if start >= text.Length then
            0
        else
            let pos = ref start
            let indent = ref 0
            while !pos > 0 && text.[!pos] <> '\r' && text.[!pos] <> '\n' do
                decr pos

            if !pos = 0 then 0 else

            incr pos
            while !pos < text.Length && text.[!pos] = ' ' do
                incr pos
                incr indent
            !indent

    let rec findPos startPosition (property:string) (text:string) =
        let needle = sprintf "\"%s\"" property
        let getBalance start =
            let pos = ref startPosition
            let balance = ref 0
            while !pos <= start do
                match text.[!pos] with
                | '{' -> incr balance
                | '}' -> decr balance
                |_ -> ()
                incr pos
            !balance

        let rec find (startWith:int) =
            match text.IndexOf(needle,startWith) with
            | -1 -> 
                if String.IsNullOrWhiteSpace text then
                    let text = sprintf "{%s    \"%s\": { }%s}" Environment.NewLine property Environment.NewLine
                    findPos startPosition property text
                else

                    let pos = ref startPosition
                    while !pos < text.Length && text.[!pos] <> '{' do
                        incr pos

                    let i = 
                        if !pos < text.Length then
                            incr pos

                            let balance = ref 1
                            while !balance <> 0 do
                                match text.[!pos] with
                                | '{' -> incr balance
                                | '}' -> decr balance
                                |_ -> ()
                                incr pos
                            ref !pos
                        else
                            ref (text.Length - 1)

                    if !i = 0 then
                        let text = sprintf "{%s    \"%s\": { }%s}" Environment.NewLine property Environment.NewLine
                        findPos startPosition property text 
                    else
                        let text = 
                            let firstPart = text.Substring(0,!i-1).TrimEnd()
                            let lastPart = text.Substring(!i-1)
                            let comma =
                                let s = firstPart.TrimEnd()
                                if s.[s.Length - 1] = '{' then "" else ","
                            let currentIndent = getIndent text (!i - 1)

                            let indentCounter =  currentIndent + 4

                            let indent = "".PadLeft indentCounter
                            let indent2 = "".PadLeft (indentCounter - 4)

                            let newLines = 
                                if comma = "," then
                                    Environment.NewLine + Environment.NewLine
                                else
                                    Environment.NewLine

                            firstPart + comma + newLines + indent + "\"" + property + "\": { }" + Environment.NewLine + indent2 + lastPart
                        findPos startPosition property text
            | start when getBalance start <> 1 -> find(start + 1)
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
        find startPosition

    member __.FileName = fileName

    member __.GetGlobalDependencies() = dependencies.Force()

    member __.GetDependencies() = dependenciesByFramework.Force()

    member __.GetGlobalInterProjectDependencies() = interProjectDependencies.Force()

    member __.GetInterProjectDependencies() = interProjectDependenciesByFramework.Force()

    member this.WithFrameworkDependencies framework dependencies =
        let nuGetDependencies = 
            dependencies 
            |> List.sortByDescending fst
            |> List.map (fun (name,version) -> sprintf "\"%O\": \"[%O]\"" name version)

        let interProjectDependencies =
            if framework = "" then
                interProjectDependencies.Force()
            else
                match interProjectDependenciesByFramework.Force() |> Map.tryFind framework with
                | Some deps -> deps
                | _ -> []
            |> List.map (fun p -> p.ToString())

        let dependencies = 
            match interProjectDependencies, nuGetDependencies with
            | [],nuGetDependencies -> nuGetDependencies
            | interProjectDependencies,[] -> interProjectDependencies
            | _ -> interProjectDependencies @ [""] @ nuGetDependencies

        let start,endPos,text = 
            if framework = "" then
                findPos 0 "dependencies" text
            else
                let frameworksPos,_,textWithFrameworks = findPos 0 "frameworks" text
                let frameworkPos,_,textWithFramework = findPos frameworksPos framework textWithFrameworks
                findPos frameworkPos "dependencies" textWithFramework



        let sb = StringBuilder(text.Substring(0,start))
        sb.Append("\"dependencies\": ") |> ignore

        let deps =
            if List.isEmpty dependencies then
                sb.Append "{ }"
            else
                sb.AppendLine "{" |> ignore
                let indent = "".PadLeft (getIndent text start + 4)
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

    member this.WithDependencies dependencies = this.WithFrameworkDependencies "" dependencies

    override __.ToString() = text

    member __.Save(forceTouch) =
        if forceTouch || text <> File.ReadAllText fileName then
            File.WriteAllText(fileName,text)

    static member Load(fileName) : ProjectJsonFile =
        ProjectJsonFile(fileName,File.ReadAllText fileName)
