module Paket.IntegrationTests.GitSpecs

open System
open System.IO
open NUnit.Framework
open FsUnit
open Paket
open Paket.Utils

/// Runs the test in a new scenario folder, with an environment that the git of the test, of paket and of
/// Paket.Core called in process all get, and that keeps git inside the scenario folder
let private inGitScenario scenario (test: string -> unit) =
    let scenarioPath = createScenarioDir scenario
    let variables =
        [ // git doesn't look for a repository above the scenario folder, as the Paket repository
          "GIT_CEILING_DIRECTORIES", scenarioPath
          // nor takes the one of a git hook that runs the tests
          "GIT_DIR", null
          "GIT_WORK_TREE", null
          "GIT_INDEX_FILE", null
          // git defaults to a branch that the repositories don't have, as "master" for a "main" repository
          "GIT_CONFIG_COUNT", "1"
          "GIT_CONFIG_KEY_0", "init.defaultBranch"
          "GIT_CONFIG_VALUE_0", "master" ]
    let saved = variables |> List.map (fun (name, _) -> name, Environment.GetEnvironmentVariable name)
    for name, value in variables do
        Environment.SetEnvironmentVariable(name, value)
    try
        test scenarioPath
    finally
        for name, value in saved do
            Environment.SetEnvironmentVariable(name, value)
        // a failed cleanup doesn't hide the failure of the test
        try
            cleanup scenario
        with e -> eprintfn "Error in test cleanup: %O" e

/// Runs git in the folder and returns its standard output
let private git folder command =
    let output = ResizeArray<string>()
    let messages = ResizeArray<string>()
    let exitCode =
        execProcess (fun info ->
            info.FileName <- "git"
            info.WorkingDirectory <- folder
            info.Arguments <- command)
            (TimeSpan.FromMinutes 1.)
            true
            (fun line -> lock output (fun () -> output.Add line))
            (fun line -> lock output (fun () -> output.Add line; messages.Add line))
    if exitCode <> 0 then
        failwithf "git %s failed in %s:%s%s" command folder Environment.NewLine (String.Join(Environment.NewLine, output))
    List.ofSeq messages

/// The author of the commits and tags, which are neither signed nor checked by hooks, whatever the git config
let private asTestAuthor = "-c user.name=Paket -c user.email=paket@example.com -c commit.gpgsign=false -c tag.gpgSign=false"

/// Commits file.txt with the content, and returns the sha of the commit
let private commitFile repo (content: string) =
    File.WriteAllText(repo @@ "file.txt", content)
    git repo "add file.txt" |> ignore
    git repo (asTestAuthor + " commit --no-verify -m commit") |> ignore
    git repo "rev-parse HEAD" |> List.head

/// Creates a repository on the branch main with a commit of file.txt, and returns the sha of the commit
let private createRepository repo =
    Directory.CreateDirectory repo |> ignore
    git repo "init --initial-branch=main" |> ignore
    commitFile repo "content"

let private fileUrl (path: string) = "file:///" + path.Replace('\\', '/').TrimStart '/'

/// The branch main of the git cache
let private mirroredMain cache =
    git cache (sprintf "--git-dir=%s rev-parse refs/heads/main" (quote cache)) |> List.head

/// A project name of its own, so that ~/.paket/git/db has no git cache for the repository yet
let private uniqueProject () = "repo-" + Guid.NewGuid().ToString "N"

let private deleteGitCache repoCacheFolder =
    try
        deleteDir (DirectoryInfo repoCacheFolder)
    with exn ->
        printfn "Could not delete the git cache %s: %s" repoCacheFolder exn.Message

[<Test>]
let ``#4461 restore of a git dependency from a cold git cache`` () =
    inGitScenario "i004461-git-cold-cache" (fun scenarioPath ->
        let project = uniqueProject ()
        let repoCacheFolder = Constants.GitRepoCacheFolder @@ project
        try
            let repo = scenarioPath @@ project
            createRepository repo |> ignore
            File.WriteAllText(scenarioPath @@ "paket.dependencies", sprintf "git %s main" (fileUrl repo))
            let checkedOutFile () = File.ReadAllText(scenarioPath @@ "paket-files" @@ "localfilesystem" @@ project @@ "file.txt")

            directPaketInPathEx "install" scenarioPath |> ignore
            Directory.Exists(repoCacheFolder @@ "objects") |> shouldEqual true

            // as on a fresh CI agent, the restore of the lock file finds neither the git cache nor the paket-files
            deleteDir (DirectoryInfo repoCacheFolder)
            deleteDir (DirectoryInfo (scenarioPath @@ "paket-files"))
            directPaketInPathEx "restore" scenarioPath |> ignore
            Directory.Exists(repoCacheFolder @@ "objects") |> shouldEqual true
            checkedOutFile () |> shouldEqual "content"

            // the update fetches the new commit into the existing git cache and paket-files
            commitFile repo "updated" |> ignore
            directPaketInPathEx "update" scenarioPath |> ignore
            checkedOutFile () |> shouldEqual "updated"
        finally
            deleteGitCache repoCacheFolder)

[<Test>]
let ``a git dependency is checked out from a git cache whose HEAD names no branch`` () =
    inGitScenario "git-cache-without-head-branch" (fun scenarioPath ->
        let project = uniqueProject ()
        let repoCacheFolder = Constants.GitRepoCacheFolder @@ project
        try
            let repo = scenarioPath @@ project
            createRepository repo |> ignore
            // the repository has neither master nor main, so the HEAD of its git cache names no branch
            git repo "checkout --detach" |> ignore
            git repo "branch -m main dev" |> ignore
            File.WriteAllText(scenarioPath @@ "paket.dependencies", sprintf "git %s dev" (fileUrl repo))

            directPaketInPathEx "install" scenarioPath |> ignore

            File.ReadAllText(scenarioPath @@ "paket-files" @@ "localfilesystem" @@ project @@ "file.txt")
            |> shouldEqual "content"
        finally
            deleteGitCache repoCacheFolder)

[<Test>]
let ``fetchCache sets up a git cache that holds only the lock file or has no origin`` () =
    inGitScenario "git-cache-setup" (fun scenarioPath ->
        let repo = scenarioPath @@ "repo"
        let sha = createRepository repo

        let lockedCache = scenarioPath @@ "locked"
        Directory.CreateDirectory lockedCache |> ignore
        File.WriteAllText(lockedCache @@ Constants.AccessLockFileName, "0")
        Git.Handling.fetchCache lockedCache (fileUrl repo)
        mirroredMain lockedCache |> shouldEqual sha

        // as after a run that failed before it added origin
        let cacheWithoutOrigin = scenarioPath @@ "without-origin"
        Directory.CreateDirectory cacheWithoutOrigin |> ignore
        git cacheWithoutOrigin "init --bare" |> ignore
        Git.Handling.fetchCache cacheWithoutOrigin (fileUrl repo)
        mirroredMain cacheWithoutOrigin |> shouldEqual sha)
