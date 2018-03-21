module SymlinkUtils

open System
open Paket
open System.Diagnostics

let isDirectoryLink directory = 
    let di = IO.DirectoryInfo(directory)
    di.Exists && di.Attributes.HasFlag(IO.FileAttributes.ReparsePoint)

/// delete the symlink only (do not remove files before)
let delete directory = if isDirectoryLink directory then IO.Directory.Delete directory

let makeDirectoryLink target source = 
    let mklink (p:ProcessStartInfo) = 
        p.FileName <- "cmd.exe"
        p.Arguments <- sprintf @"/c ""mklink /D ""%s"" ""%s""""" target source

    let ln (p:ProcessStartInfo) = 
        p.FileName <- "ln"
        p.Arguments <- sprintf @"-sT ""%s"" ""%s""" target source

    let xLn = if isUnix then ln else mklink
    
    let r = ProcessHelper.ExecProcessAndReturnMessages xLn (TimeSpan.FromSeconds(10.))
    
    match r.OK, Logging.verbose with
    | true, true -> 
        let m = ProcessHelper.toLines r.Messages
        sprintf "symlink used %s -> %s (%s)" source target m |> Logging.traceVerbose
    | true, false -> ()
    | false, _ ->
        let m = ProcessHelper.toLines r.Messages
        let e = ProcessHelper.toLines r.Errors
        failwithf "symlink %s -> %s failed with error : [%i] with output : %s%s and error : %s" source target r.ExitCode m Environment.NewLine e