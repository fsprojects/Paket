module Paket.PackageSourceParser

open System
open System.IO
open Paket
open System.Text.RegularExpressions
open Paket.PackageSources

let userNameRegex = new Regex("username[:][ ]*[\"]?([^\"]+)[\"]?", RegexOptions.IgnoreCase);
let passwordRegex = new Regex("password[:][ ]*[\"]?([^\"]+)[\"]?", RegexOptions.IgnoreCase);
let parseAuth(text:string) =
    if userNameRegex.IsMatch(text) && passwordRegex.IsMatch(text) then
        Some { Username = userNameRegex.Match(text).Groups.[1].Value; Password = passwordRegex.Match(text).Groups.[1].Value }
    else
        None

let getSources lines =
    [for line:string in lines do
        match line.Trim() with
        | trimmed when trimmed.StartsWith "source" ->
            let parts = trimmed.Split ' '
            let newSource = parts.[1].Replace("\"","").TrimEnd([|'/'|])
            yield PackageSource.Parse(newSource,parseAuth trimmed) 
        | _ -> ()]