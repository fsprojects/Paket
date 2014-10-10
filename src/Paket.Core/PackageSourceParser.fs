module Paket.PackageSourceParser

open System
open Paket
open System.Text.RegularExpressions
open Paket.PackageSources

let userNameRegex = new Regex("username[:][ ]*[\"]([^\"]+)[\"]", RegexOptions.IgnoreCase);
let passwordRegex = new Regex("password[:][ ]*[\"]([^\"]+)[\"]", RegexOptions.IgnoreCase);

let parseAuth(text:string) =
    if userNameRegex.IsMatch(text) && passwordRegex.IsMatch(text) then
        let expanded = Environment.ExpandEnvironmentVariables(text)
        Some { Username = userNameRegex.Match(expanded).Groups.[1].Value; Password = passwordRegex.Match(expanded).Groups.[1].Value }
    else
        if text.Contains("username:") || text.Contains("password:") then
            failwithf "Could not parse auth in \"%s\"" text
        None

let getSources lines =
    [for line:string in lines do
        match line.Trim() with
        | trimmed when trimmed.StartsWith "source" ->
            let parts = trimmed.Split ' '
            let newSource = parts.[1].Replace("\"","").TrimEnd([|'/'|])
            yield PackageSource.Parse(newSource,parseAuth trimmed)
        | _ -> ()]