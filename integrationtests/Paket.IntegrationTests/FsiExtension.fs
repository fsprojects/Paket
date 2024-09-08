namespace Paket.IntegrationTests
module FsiExtension =
    open System
    open System.IO
    open FSharp.Compiler.SourceCodeServices
    open FSharp.Compiler.Text
    open NUnit.Framework

    let configuration =
    #if DEBUG
      "Debug"
    #else
      "Release"
    #endif

    let pathToExtension = Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "src", "FSharp.DependencyManager.Paket", "bin", configuration, "netstandard2.0")
    let extensionFileName = "FSharp.DependencyManager.Paket.dll"

    [<Test>]
    let ``fcs can type check `` () =
      System.AppDomain.CurrentDomain.add_AssemblyResolve(fun _ (e: System.ResolveEventArgs) ->
          // the paket dependency manager assembly depends on fsharp.core version
          // which may not be the same as hosting process
          if e.Name.StartsWith "FSharp.Core," then
              printfn "binding redirect for FSharp.Core..."
              typedefof<Map<_,_>>.Assembly
          else
              e.RequestingAssembly  
      )
      let checker = FSharpChecker.Create(suggestNamesForErrors=true, keepAssemblyContents=true)
      let sourceText = """
      #r "paket: nuget FSharp.Data"
      let v = FSharp.Data.JsonValue.Boolean true
      """
      let projectOptions = 
        checker.GetProjectOptionsFromScript("test.fsx", SourceText.ofString sourceText, otherFlags = [| "/langversion:preview"; sprintf "/compilertool:%s" pathToExtension |] )
        |> Async.RunSynchronously
        |> fst
      
      let _, answer = checker.ParseAndCheckFileInProject("test.fsx", 0, SourceText.ofString sourceText, projectOptions) |> Async.RunSynchronously
      match answer with
      | FSharpCheckFileAnswer.Succeeded(result) ->
        Assert.IsTrue result.HasFullTypeCheckInfo
        Assert.IsTrue (Array.isEmpty result.Errors)
        Assert.AreEqual("v", result.PartialAssemblySignature.Entities.[0].MembersFunctionsAndValues.[0].DisplayName)
        Assert.AreEqual("FSharp.Data", result.PartialAssemblySignature.Entities.[0].MembersFunctionsAndValues.[0].FullType.TypeDefinition.AccessPath)
        Assert.AreEqual("JsonValue", result.PartialAssemblySignature.Entities.[0].MembersFunctionsAndValues.[0].FullType.TypeDefinition.DisplayName)
      | _ -> Assert.Fail()

    type FsxRun = { file: FileInfo; arguments: string; stdOut: string; errOut: string }
    type FsxTestResult =
        | Pass of fsxFilename: string
        | Failed of fsxFilename: string * expectedOutput: string * actualOutuput: string * commandLineArguments: string

    let fsxsFolder = Path.Combine(__SOURCE_DIRECTORY__, "..", "scenarios", "fsi-depmanager", "deterministic.output")

    let runSingleFsxTestForOutput (fsxFile: FileInfo) =
        let arguments = sprintf @"fsi --langversion:preview %s %s" (sprintf "--compilertool:%s" pathToExtension) fsxFile.FullName
        let standardOutput, errorOutput =
            let p = new System.Diagnostics.Process()
            p.StartInfo.UseShellExecute <- false
            p.StartInfo.FileName <- "dotnet"
            p.StartInfo.Arguments <- arguments
            p.StartInfo.RedirectStandardOutput <- true
            p.StartInfo.RedirectStandardError <- true
            p.Start() |> ignore
            let standardOutput, errorOutput = p.StandardOutput.ReadToEnd(), p.StandardError.ReadToEnd()
            p.WaitForExit()
            standardOutput, errorOutput
        { file = fsxFile; stdOut = standardOutput; errOut = errorOutput; arguments = arguments }
        
    let runSingleFsxTest (fsxFile: FileInfo) =

        let expectedOutput =
            let goldenFile = Path.Combine(fsxFile.Directory.FullName, Path.ChangeExtension(fsxFile.Name, "golden"))
            if File.Exists goldenFile then
                File.ReadAllText(goldenFile)
            else
                File.Create goldenFile |> ignore
                ""
        let result = runSingleFsxTestForOutput fsxFile
        
        let actualOutput =
            (result.stdOut + result.errOut).Split([|System.Environment.NewLine|], System.StringSplitOptions.None)
            |> Array.filter (fun s -> not (s.StartsWith ":paket>"))
            |> String.concat System.Environment.NewLine

        File.WriteAllText(Path.Combine(fsxFile.Directory.FullName, Path.ChangeExtension(fsxFile.Name, "output")), actualOutput)
        let normalizeCR (a: string) = a.Replace("\r\n", "\n")
        let actualOutput = normalizeCR actualOutput
        let expectedOutput = normalizeCR expectedOutput
        if actualOutput = expectedOutput then
            Pass fsxFile.FullName 
        else 
            Failed(fsxFile.FullName, expectedOutput, actualOutput, result.arguments)

    [<Test>]
    let ``run fsi integration tests that have deterministic output`` () =
        let fsxFiles = DirectoryInfo(fsxsFolder).GetFiles("*.fsx", SearchOption.AllDirectories)
        let failures = 
            [|
                for fsx in fsxFiles do
                    if not (fsx.Name.StartsWith "skip.") then
                        
                        match runSingleFsxTest fsx with
                        | Pass _ -> printfn "OK: %s" fsx.FullName
                        | Failed(file,_,_,commandArguments) ->
                            printfn "KO: %s" fsx.FullName
                            yield file, commandArguments
            |]
        
        if failures.Length > 0 then
            failures
            |> Array.map (fun (fsxFile, arguments) -> sprintf "file: %s\ncommand: %s" fsxFile arguments)
            |> String.concat System.Environment.NewLine
            |> Assert.Fail

    [<Test>]
    let ``custom/SimplePaketFailTest/plot.fsx`` () =
        let fsxFile = FileInfo(Path.Combine(fsxsFolder, "..", "custom", "SimplePaketFailTest", "plot.fsx"))
        let result = runSingleFsxTestForOutput fsxFile
        if not (result.stdOut.Contains "Unable to retrieve package versions for 'SomeInvalidNugetPackage'") then
            printfn "arguments:\n%s" result.arguments
            printfn "stdOut:\n%s" result.stdOut
            printfn "errOut:\n%s" result.errOut
            Assert.Fail()