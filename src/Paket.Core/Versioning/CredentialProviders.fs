namespace Paket

open System
open System.IO
open Newtonsoft.Json

type CredentialProviderResultMessage =
    { [<JsonProperty("Username")>]
      Username : string;
      [<JsonProperty("Password")>]
      Password : string;
      [<JsonProperty("Message")>]
      Message  : string
      [<JsonProperty("AuthTypes")>]
      AuthTypes  : string [] }
    // From https://github.com/NuGet/NuGet.Client/blob/c17547b5c64ab8d498cc24340a09ae647456cf20/src/NuGet.Clients/NuGet.Credentials/PluginCredentialResponse.cs#L34
    member x.IsValid =
        not (String.IsNullOrWhiteSpace x.Username) ||
            not (String.IsNullOrWhiteSpace x.Password)
type CredentialProviderExitCode =
    | Success = 0
    | ProviderNotApplicable = 1
    | Abort = 2

type CredentialProviderResult =
    | Success of UserPassword list
    | NoCredentials of string
    | Abort of string

type CredentialProviderVerbosity =
    | Normal
    | Quiet
    | Detailed

type CredentialProviderParameters =
    { Uri : string
      NonInteractive : bool
      IsRetry : bool
      Verbosity : CredentialProviderVerbosity }

/// Exception for request errors
#if !NETSTANDARD1_6
[<System.Serializable>]
#endif
type CredentialProviderUnknownStatusException =
    inherit Exception
    new (msg:string, inner:exn) = {
      inherit Exception(msg, inner) }
#if !NETSTANDARD1_5
    new (info:System.Runtime.Serialization.SerializationInfo, context:System.Runtime.Serialization.StreamingContext) = {
      inherit Exception(info, context)
    }
#endif

module CredentialProviders =
    open Logging
    open System.Collections.Concurrent

    let patternExe = "CredentialProvider*.exe"
    let patternDll = "CredentialProvider*.dll"
    let envVar = "NUGET_CREDENTIALPROVIDERS_PATH"
    let directoryRoot =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NuGet",
            "CredentialProviders")
    let findAll rootPath customPaths assemblyPattern paketDirectoryAssemblyPattern =
        let directories =
            [ yield! customPaths
              yield rootPath ]
        [ yield!
            directories
            |> Seq.filter Directory.Exists
            |> Seq.collect (fun d -> Directory.EnumerateFiles(d, assemblyPattern, SearchOption.AllDirectories))
          let paketDirectory = Path.GetDirectoryName(typeof<CredentialProviderUnknownStatusException>.Assembly.Location)
          if not (String.IsNullOrEmpty paketDirectory) then
            yield! Directory.EnumerateFiles(paketDirectory, paketDirectoryAssemblyPattern)
        ]
    let findPathsFromEnvVar key =
        let paths = Environment.GetEnvironmentVariable key
        if not (String.IsNullOrEmpty paths) then
            paths.Split([|';'|], StringSplitOptions.RemoveEmptyEntries)
            |> Array.toList
        else [ ]

    let collectProviders () =
        let customPaths = findPathsFromEnvVar envVar
        findAll directoryRoot customPaths patternExe patternExe @ findAll directoryRoot customPaths patternDll patternDll
        |> List.distinct

    // See https://github.com/NuGet/NuGet.Client/blob/c17547b5c64ab8d498cc24340a09ae647456cf20/src/NuGet.Clients/NuGet.Credentials/PluginCredentialProvider.cs#L169
    let formatCommandLine args =
        [
            yield! ["-uri"; args.Uri]
            if args.NonInteractive then yield "-nonInteractive"
            if args.IsRetry then yield "-isRetry"
            if args.Verbosity <> CredentialProviderVerbosity.Normal then
                yield! ["-verbosity"; args.Verbosity.ToString().ToLower()]
        ]
        |> Seq.map (fun arg -> if arg.Contains " " then failwithf "cannot contain space" else arg)
        |> String.concat " "

    let private availableAuthTypes =
        [ AuthType.Basic; AuthType.NTLM ]
        |> List.map (fun t -> t.ToString().ToLower(), t)
    let callProvider provider args =
        let cmdLine = formatCommandLine args
        let procResult =
            ProcessHelper.ExecProcessAndReturnMessages (fun info ->
              info.FileName <- provider
              info.WindowStyle <- System.Diagnostics.ProcessWindowStyle.Hidden
              info.ErrorDialog <- false
              info.Arguments <- cmdLine) (TimeSpan.FromMinutes 10.)

        let stdError = ProcessHelper.toLines procResult.Errors
        for line in procResult.Errors do
            Logging.traceVerbose (sprintf "%s: %s" provider line)

        let json = ProcessHelper.toLines procResult.Messages
        let credentialResponse =
            try
                JsonConvert.DeserializeObject<CredentialProviderResultMessage>(json)
            with e ->
                raise <| exn(sprintf "Credential provider returned an invalid result: %s\nError: %s" json stdError, e)
        let parsableResult = not (isNull (box credentialResponse))
        let validResult = parsableResult && credentialResponse.IsValid

        match enum procResult.ExitCode with
        | CredentialProviderExitCode.Success when validResult ->
            let createResult auth =
                {Username = credentialResponse.Username; Password = credentialResponse.Password; Type = auth }
            let results =
                if isNull (box credentialResponse.AuthTypes) || credentialResponse.AuthTypes.Length = 0 then
                    [createResult AuthType.Basic]
                else
                    let results =
                        credentialResponse.AuthTypes
                        |> List.ofArray
                        |> List.map (fun tp -> tp.ToLower())
                        |> List.map (fun tp ->
                            match availableAuthTypes |> List.tryFind (fun (s, _) -> s = tp) with
                            | Some (_, matching) ->
                                tp, Some (createResult matching)
                            | None ->
                                tp, None)
                    if results |> List.exists (snd >> Option.isSome) then
                        results |> List.choose snd
                    else
                        for tp, _ in results do
                            Logging.traceWarnfn "The authentication scheme '%s' is not supported" tp
                        []
            Success results
        | CredentialProviderExitCode.Success ->
            failwithf "Credential provider returned an invalid result (%d): %s\n Standard Error: %s" procResult.ExitCode json stdError
        | CredentialProviderExitCode.ProviderNotApplicable ->
            NoCredentials (if parsableResult then credentialResponse.Message else "")
        | CredentialProviderExitCode.Abort ->
            let msg = if parsableResult then credentialResponse.Message else ""
            Abort (sprintf "\"'%s' %s\":%s\nStandard Error: %s" provider cmdLine msg stdError)
        | _ ->
            raise <| CredentialProviderUnknownStatusException (sprintf "Credential provider returned an invalid result (%d): %s\nStandard Error: %s" procResult.ExitCode json stdError, (null : exn))

    let private  _providerCredentialCache = new ConcurrentDictionary<string, CredentialProviderResult>()
    let private getKey provider source =
        provider + "_" + source
    let handleProvider isRetry provider source =
        let key = getKey provider source
        let args =
            { Uri = source
              NonInteractive = not Environment.UserInteractive
              IsRetry = isRetry
              Verbosity = CredentialProviderVerbosity.Normal }
        match _providerCredentialCache.TryGetValue key with
        | true, v when not isRetry ->
            v
        | _ ->
          // Only ever show a single provider at the same time.
          lock _providerCredentialCache (fun _ ->
            match _providerCredentialCache.TryGetValue key with
            | true, v when not isRetry ->
                v
            | _ ->
                Logging.verbosefn "Calling provider '%s' for credentials" provider
                let result =
                    try callProvider provider args
                    with :? CredentialProviderUnknownStatusException when args.Verbosity <> CredentialProviderVerbosity.Normal ->
                        // https://github.com/NuGet/NuGet.Client/blob/c17547b5c64ab8d498cc24340a09ae647456cf20/src/NuGet.Clients/NuGet.Credentials/PluginCredentialProvider.cs#L117
                        callProvider provider { args with Verbosity = CredentialProviderVerbosity.Normal }

                match result with
                | CredentialProviderResult.Abort _ -> ()
                | _ ->
                    _providerCredentialCache.[key] <- result

                result)

    let GetAuthenticationDirect (source : string) isRetry =
        collectProviders()
        |> List.collect (fun provider ->
            match handleProvider isRetry provider source with
            | CredentialProviderResult.Success l -> l
            | CredentialProviderResult.NoCredentials _ -> []
            | CredentialProviderResult.Abort msg -> failwith msg)

    let GetAuthenticationProvider source =
        AuthProvider.ofFunction (fun isRetry ->
            match GetAuthenticationDirect source isRetry with
            | h :: _ -> Some (Credentials h)
            | _ -> None)

