/// Contains helpers which allow to interact with [git](http://git-scm.com/) via the command line.
module Paket.Git.CommandHelper

open System
open System.Diagnostics
open System.IO
open System.Threading
open System.Text
open System.Collections.Generic
open Paket.Logging
open Paket

/// Specifies a global timeout for git.exe - default is *no timeout*
let mutable gitTimeOut = TimeSpan.MaxValue

let private GitPath = @"[ProgramFiles]\Git\cmd\;[ProgramFilesX86]\Git\cmd\;[ProgramFiles]\Git\bin\;[ProgramFilesX86]\Git\bin\;"

/// Tries to locate the git.exe via the eviroment variable "GIT".
let gitPath =
    if Utils.isUnix then
        "git"
    else
        let ev = Environment.GetEnvironmentVariable "GIT"
        if not (String.IsNullOrWhiteSpace ev) then ev else ProcessHelper.findPath "GitPath" GitPath "git.exe"


/// Runs git.exe with the given command in the given repository directory.
let runGitCommand repositoryDir command =
    let processResult =
        ProcessHelper.ExecProcessAndReturnMessages (fun info ->
          info.FileName <- gitPath
          info.WorkingDirectory <- repositoryDir
          info.Arguments <- command) gitTimeOut

    processResult.OK,processResult.Messages,ProcessHelper.toLines processResult.Errors

/// [omit]
let runGitCommandf fmt = Printf.ksprintf runGitCommand fmt

/// [omit]
let getGitResult repositoryDir command =
    let _,msg,_ = runGitCommand repositoryDir command
    msg

/// Fires the given git command ind the given repository directory and returns immediatly.
let fireAndForgetGitCommand repositoryDir command =
    ProcessHelper.fireAndForget (fun info ->
      info.FileName <- gitPath
      info.WorkingDirectory <- repositoryDir
      info.Arguments <- command)

/// Runs the given git command, waits for its completion and returns whether it succeeded.
let directRunGitCommand repositoryDir command =
    ProcessHelper.directExec (fun info ->
      info.FileName <- gitPath
      info.WorkingDirectory <- repositoryDir
      info.Arguments <- command)

/// Runs the given git command, waits for its completion.
let gitCommand repositoryDir command =
    let ok,msg,error = runGitCommand repositoryDir command

    if not ok then failwith error else
    if verbose then
        for m in msg do
            tracefn "%s" m

/// [omit]
let gitCommandf repositoryDir fmt = Printf.ksprintf (gitCommand repositoryDir) fmt

/// Runs the git command and returns the results.
let runFullGitCommand repositoryDir command =
    try
        let ok,msg,errors = runGitCommand repositoryDir command

        let errorText = ProcessHelper.toLines msg + Environment.NewLine + errors
        if errorText.Contains "fatal: " then
            failwith errorText

        if msg.Count = 0 then [||] else
        if verbose then
            for m in msg do
                tracefn "%s" m
        msg |> Seq.toArray
    with
    | exn -> raise (Exception(sprintf "Could not run \"git %s\"." command, exn))

/// Runs the git command and returns the first line of the result.
let runSimpleGitCommand repositoryDir command =
    try
        let ok,msg,errors = runGitCommand repositoryDir command

        let errorText = ProcessHelper.toLines msg + Environment.NewLine + errors
        if errorText.Contains "fatal: " then
            failwith errorText

        if msg.Count = 0 then "" else
        if verbose then
            for m in msg do
                tracefn "%s" m
        msg.[0]
    with
    | exn -> raise (Exception(sprintf "Could not run \"git %s\"." command, exn))