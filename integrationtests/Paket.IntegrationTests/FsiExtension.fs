module FsiExtension
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
    checker.GetProjectOptionsFromScript("test.fsx", SourceText.ofString sourceText, otherFlags = [| "/langversion:preview"; |] )
    |> Async.RunSynchronously
    |> fst
  
  let _, answer = checker.ParseAndCheckFileInProject("test.fsx", 0, SourceText.ofString sourceText, projectOptions) |> Async.RunSynchronously
  match answer with
  | FSharpCheckFileAnswer.Succeeded(result) ->
    Assert.IsTrue result.HasFullTypeCheckInfo
  | _ -> Assert.Fail()

type FsxTestResult =
    | Pass of fsxFilename: string
    | Failed of fsxFilename: string * expectedOutput: string * actualOutuput: string

let singleFsxFolder = Path.Combine(__SOURCE_DIRECTORY__, "..", "scenarios", "fsi-depmanager", "SimplePaketTest")

let runSingleFsxTest (fsxFile: FileInfo) =

    let expectedOutput =
        let goldenFile = Path.Combine(singleFsxFolder, Path.ChangeExtension(fsxFile.Name, "golden"))
        if File.Exists goldenFile then
            File.ReadAllText(goldenFile)
        else
            File.Create goldenFile |> ignore
            ""

    let actualOutput =
        let p = new System.Diagnostics.Process()
        p.StartInfo.UseShellExecute <- false
        p.StartInfo.FileName <- "dotnet"
        p.StartInfo.Arguments <- (sprintf @"fsi --langversion:preview %s %s" (sprintf "--compilertool:%s" pathToExtension)) fsxFile.FullName
        p.StartInfo.RedirectStandardOutput <- true
        p.StartInfo.RedirectStandardError <- true
        p.Start() |> ignore
        
        let output = 
            p.StandardOutput
                .ReadToEnd()
                .Split([|System.Environment.NewLine|], System.StringSplitOptions.None)
            |> Array.filter (fun s -> not (s.StartsWith ":paket>"))
            |> String.concat System.Environment.NewLine

        p.WaitForExit()
        output

    File.WriteAllText(Path.Combine(singleFsxFolder, Path.ChangeExtension(fsxFile.Name, "output")), actualOutput)
    let normalizeCR (a: string) = a.Replace("\r\n", "\n")
    let actualOutput = normalizeCR actualOutput
    let expectedOutput = normalizeCR expectedOutput
    if actualOutput = expectedOutput then
        Pass fsxFile.Name 
    else 
        Failed(fsxFile.Name, expectedOutput, actualOutput)

[<Test>]
let ``run fsi integration tests`` () =
    let fsxFiles = DirectoryInfo(singleFsxFolder).GetFiles("*.fsx")
    let results = 
        [|
            for fsx in fsxFiles do
                runSingleFsxTest fsx
        |]

    let failures =
        results
        |> Array.choose (function Pass _ -> None | Failed(fsx, expected, actual) -> Some (fsx, expected, actual))

    if failures.Length > 0 then
        // http://www.fssnip.net/l4/title/Diff-two-strings
        let DiffStrings (s1 : string) (s2 : string) =
           let s1', s2' = s1.PadRight(s2.Length), s2.PadRight(s1.Length)

           let d1, d2 = 
              (s1', s2')
              ||> Seq.zip 
              |> Seq.map (fun (c1, c2) -> if c1 = c2 then '-','-' else c1, c2)
              |> Seq.fold (fun (d1, d2) (c1, c2) -> (sprintf "%s%c" d1 c1), (sprintf "%s%c" d2 c2) ) ("","")
           d1, d2
        failures
        |> Array.map (fun (fsx, expected, actual) -> sprintf "expected output didn't match for %s\nexpected:\n%s\n\nactual:\n%s\n%A" fsx expected actual (DiffStrings expected actual))
        |> String.concat System.Environment.NewLine
        |> Assert.Fail
