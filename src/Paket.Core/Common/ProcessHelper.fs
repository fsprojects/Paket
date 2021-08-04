module Paket.ProcessHelper

open System.Diagnostics
open System.IO
open System
open System.Collections.Generic

/// Arguments on the Mono executable
let mutable monoArguments = ""


/// Searches the given directories for all occurrences of the given file name
/// [omit]
let tryFindFile dirs file =
    let files =
        dirs
        |> Seq.map (fun (path : string) ->
            try
               let dir =
                 DirectoryInfo(
                   path
                    .Replace("[ProgramFiles]",ProgramFiles)
                    .Replace("[ProgramFilesX86]",ProgramFilesX86)
                    .Replace("[SystemRoot]",SystemRoot))

               if not dir.Exists then ""
               else
                   let fi = FileInfo(Path.Combine(dir.FullName,file))
                   if fi.Exists then fi.FullName
                   else ""
            with
            | exn ->
                Logging.verbosefn "Exception while searching %s in %s:" file path
                Logging.verbosefn "%O" exn

                "")
        |> Seq.filter ((<>) "")
        |> Seq.cache
    if not (Seq.isEmpty files) then Some(Seq.head files)
    else None

/// Searches the given directories for the given file, failing if not found.
/// [omit]
let findFile dirs file =
    match tryFindFile dirs file with
    | Some found -> found
    | None -> failwithf "%s not found in %A." file dirs

/// Retrieves the environment variable or None
let environVarOrNone name =
    let var = name
              |> Environment.GetEnvironmentVariable
              |> Environment.ExpandEnvironmentVariables

    if String.IsNullOrEmpty var then None
    else Some var

/// Splits the entries of an environment variable and removes the empty ones.
let splitEnvironVar name =
    let var = environVarOrNone name
    if var = None then [ ]
    else var.Value.Split([| Path.PathSeparator |]) |> Array.toList


/// Detects whether the given path does not contains invalid characters.
let isValidPath (path:string) =
    Path.GetInvalidPathChars()
    |> Array.filter (fun char -> path.Contains(char.ToString()))
    |> Array.isEmpty

/// Gets the list of valid directories included in the PATH environment variable.
let pathDirectories =
    splitEnvironVar "PATH"
    |> Seq.map (fun value -> value.Trim())
    |> Seq.filter (fun value -> not (String.IsNullOrEmpty value) && isValidPath value)

/// Searches the current directory and the directories within the PATH
/// environment variable for the given file. If successful returns the full
/// path to the file.
/// ## Parameters
///  - `file` - The file to locate
let tryFindFileOnPath (file : string) : string option =
    pathDirectories
    |> Seq.append [ "." ]
    |> fun path -> tryFindFile path file

/// Modifies the ProcessStartInfo according to the platform semantics
let platformInfoAction (psi : ProcessStartInfo) =
    if isMonoRuntime && psi.FileName.EndsWith ".exe" then
        psi.Arguments <- monoArguments + " \"" + psi.FileName + "\" " + psi.Arguments
        psi.FileName <- monoPath

    if psi.FileName.ToLowerInvariant().EndsWith(".dll") then
        // Run DotNetCore
        let exeName = if isUnix then "dotnet" else "dotnet.exe"
        let dotnetExe =
            match tryFindFileOnPath exeName with
            | Some exe -> exe
            | None -> exeName
        psi.Arguments <- "\"" + psi.FileName + "\" " + psi.Arguments
        psi.FileName <- dotnetExe



/// Returns the AppSettings for the key - Splitted on ;
/// [omit]
let appSettings (key : string) (fallbackValue : string) =
    let value =
        let setting =
#if NO_CONFIGURATIONMANAGER
            ""
#else
            try
                System.Configuration.ConfigurationManager.AppSettings.[key]
            with exn -> ""
#endif
        if not (String.IsNullOrWhiteSpace setting) then setting
        else fallbackValue
    value.Split([| ';' |], StringSplitOptions.RemoveEmptyEntries)

/// Tries to find the tool via AppSettings. If no path has the right tool we are trying the PATH system variable.
/// [omit]
let tryFindPath settingsName fallbackValue tool =
    let paths = appSettings settingsName fallbackValue
    match tryFindFile paths tool with
    | Some path -> Some path
    | None -> tryFindFileOnPath tool

/// Tries to find the tool via AppSettings. If no path has the right tool we are trying the PATH system variable.
/// [omit]
let findPath settingsName fallbackValue tool =
    match tryFindPath settingsName fallbackValue tool with
    | Some file -> file
    | None -> tool

/// [omit]
let internal startedProcesses = HashSet()

/// [omit]
let start (proc : Process) =
    platformInfoAction proc.StartInfo
    proc.Start() |> ignore
    startedProcesses.Add(proc.Id, proc.StartTime) |> ignore

/// Runs the given process and returns the exit code.
/// ## Parameters
///
///  - `configProcessStartInfoF` - A function which overwrites the default ProcessStartInfo.
///  - `timeOut` - The timeout for the process.
///  - `silent` - If this flag is set then the process output is redirected to the given output functions `errorF` and `messageF`.
///  - `errorF` - A function which will be called with the error log.
///  - `messageF` - A function which will be called with the message log.
let ExecProcessWithLambdas configProcessStartInfoF (timeOut : TimeSpan) silent errorF messageF =
    use proc = new Process()
    proc.StartInfo.UseShellExecute <- false
    configProcessStartInfoF proc.StartInfo
    platformInfoAction proc.StartInfo
    if String.IsNullOrEmpty proc.StartInfo.WorkingDirectory |> not then
        if Directory.Exists proc.StartInfo.WorkingDirectory |> not then
            failwithf "Start of process %s failed. WorkingDir %s does not exist." proc.StartInfo.FileName
                proc.StartInfo.WorkingDirectory
    if silent then
        proc.StartInfo.RedirectStandardOutput <- true
        proc.StartInfo.RedirectStandardError <- true
        proc.ErrorDataReceived.Add(fun d ->
            if d.Data <> null then errorF d.Data)
        proc.OutputDataReceived.Add(fun d ->
            if d.Data <> null then messageF d.Data)
    try
        start proc
    with exn -> raise (Exception(sprintf "Start of process %s failed." proc.StartInfo.FileName, exn))
    if silent then
        proc.BeginErrorReadLine()
        proc.BeginOutputReadLine()
    if timeOut = TimeSpan.MaxValue then proc.WaitForExit()
    else
        if not (proc.WaitForExit(int timeOut.TotalMilliseconds)) then
            let inner =
                try
                    proc.Kill()
                    null
                with exn -> exn
            raise (Exception(sprintf "Process %s %s timed out." proc.StartInfo.FileName proc.StartInfo.Arguments, inner))
    // See http://stackoverflow.com/a/16095658/1149924 why WaitForExit must be called twice.
    proc.WaitForExit()
    proc.ExitCode

/// A process result including error code, message log and errors.
type ProcessResult =
    { ExitCode : int
      Messages : List<string>
      Errors : List<string> }
    member x.OK = x.ExitCode = 0
    static member New exitCode messages errors =
        { ExitCode = exitCode
          Messages = messages
          Errors = errors }

/// Runs the given process and returns the process result.
/// ## Parameters
///
///  - `configProcessStartInfoF` - A function which overwrites the default ProcessStartInfo.
///  - `timeOut` - The timeout for the process.
let ExecProcessAndReturnMessages configProcessStartInfoF timeOut =
    let errors = new List<_>()
    let messages = new List<_>()
    let exitCode = ExecProcessWithLambdas configProcessStartInfoF timeOut true errors.Add messages.Add
    ProcessResult.New exitCode messages errors

/// Converts a sequence of strings to a string with delimiters
let inline separated (delimiter: string) (items : string seq) = String.Join(delimiter, Array.ofSeq items)

/// Converts a sequence of strings into a string separated with line ends
let inline toLines text = separated Environment.NewLine text

/// Starts the given process and returns immediatly.
let fireAndForget configProcessStartInfoF =
    use proc = new Process()
    proc.StartInfo.UseShellExecute <- false
    configProcessStartInfoF proc.StartInfo
    try
        start proc
    with exn -> raise (Exception(sprintf "Start of process %s failed." proc.StartInfo.FileName, exn))

/// Runs the given process, waits for its completion and returns if it succeeded.
let directExec configProcessStartInfoF =
    use proc = new Process()
    proc.StartInfo.UseShellExecute <- false
    configProcessStartInfoF proc.StartInfo
    try
        start proc
    with exn -> raise (Exception(sprintf "Start of process %s failed." proc.StartInfo.FileName, exn))
    proc.WaitForExit()
    proc.ExitCode = 0

