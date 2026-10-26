module SymlinkUtils

open System
open Paket
open System.Diagnostics

// "link/" stands for the folder the link leads to, which is no link and cannot be deleted as one;
// a root, such as "/" or "C:\", keeps its separators
let private withoutTrailingSeparators (directory: string) =
    let trimmed = directory.TrimEnd(IO.Path.DirectorySeparatorChar, IO.Path.AltDirectorySeparatorChar)
    if trimmed.Length <= (IO.Path.GetPathRoot directory).Length then directory else trimmed

let isDirectoryLink (directory: string) = isLink (IO.DirectoryInfo(withoutTrailingSeparators directory))

/// delete the symlink only (do not remove files before)
let delete directory = if isDirectoryLink directory then IO.Directory.Delete(withoutTrailingSeparators directory)

let makeDirectoryLink target source =
    let mklink (p:ProcessStartInfo) =
        p.FileName <- "cmd.exe"
        p.Arguments <- sprintf @"/c ""mklink /D ""%s"" ""%s""""" target source

    let ln (p:ProcessStartInfo) =
        p.FileName <- "ln"
        p.Arguments <- sprintf @"-sT ""%s"" ""%s""" source target

    let ln_onMacOS (p:ProcessStartInfo) =
        p.FileName <- "ln"
        p.Arguments <- sprintf @"-s ""%s"" ""%s""" source target

    let xLn = if isMacOS then ln_onMacOS elif isUnix then ln else mklink

    let r = ProcessHelper.ExecProcessAndReturnMessages xLn (TimeSpan.FromSeconds(10.))

    match r.OK, Logging.verbose with
    | true, true ->
        let m = ProcessHelper.toLines r.Messages
        sprintf "symlink used %s -> %s (%s)" source target m |> Logging.traceVerbose
    | true, false -> ()
    | false, _ ->
        let m = ProcessHelper.toLines r.Messages
        let e = ProcessHelper.toLines r.Errors
        if e.Contains "File exists" then
            sprintf "symlink already there" |> Logging.traceVerbose
        else
            failwithf "symlink %s -> %s failed with error : [%i] with output : %s%s and error : %s" source target r.ExitCode m Environment.NewLine e
