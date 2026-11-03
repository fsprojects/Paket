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
          // git defaults to a branch that the repositories don't have, as "master" for a "main" repository,
          // and allows a bare repository only when it is named
          "GIT_CONFIG_COUNT", "2"
          "GIT_CONFIG_KEY_0", "init.defaultBranch"
          "GIT_CONFIG_VALUE_0", "master"
          "GIT_CONFIG_KEY_1", "safe.bareRepository"
          "GIT_CONFIG_VALUE_1", "explicit" ]
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

let private tag repo name =
    git repo (sprintf "%s tag %s" asTestAuthor name) |> ignore

/// Creates a repository on the branch main with a commit of file.txt, and returns the sha of the commit
let private createRepository repo =
    Directory.CreateDirectory repo |> ignore
    git repo "init --initial-branch=main" |> ignore
    commitFile repo "content"

let private fileUrl (path: string) = "file:///" + path.Replace('\\', '/').TrimStart('/').Replace("%", "%25")

/// The branch main of the git cache
let private mirroredMain cache =
    git cache (sprintf "--git-dir=%s rev-parse refs/heads/main" (quote cache)) |> List.head

/// Restores the lock file even when paket-files/paket.restore.cached says that it is restored already
let private restoreAgain scenarioPath =
    File.Delete(scenarioPath @@ Constants.PaketRestoreHashFilePath)
    directPaketInPathEx "restore" scenarioPath |> ignore

/// A project name of its own, so that ~/.paket/git/db has no git cache for the repository yet
let private uniqueProject () = "repo-" + Guid.NewGuid().ToString "N"

let private deleteGitCache repoCacheFolder =
    try
        deleteDir (DirectoryInfo repoCacheFolder)
        // and the folder of the owner, unless other git caches are in it
        let ownerFolder = Path.GetDirectoryName repoCacheFolder
        if Directory.Exists ownerFolder && Seq.isEmpty (Directory.EnumerateFileSystemEntries ownerFolder) then
            Directory.Delete ownerFolder
    with exn ->
        printfn "Could not delete the git cache %s: %s" repoCacheFolder exn.Message

[<Test>]
let ``#4461 restore of a git dependency from a cold git cache`` () =
    inGitScenario "i004461-git-cold-cache" (fun scenarioPath ->
        let project = uniqueProject ()
        let repoCacheFolder = Constants.GitRepoCacheFolder @@ "localfilesystem" @@ project
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
        let repoCacheFolder = Constants.GitRepoCacheFolder @@ "localfilesystem" @@ project
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
let ``a git dependency whose checkout fails fails the install and leaves no clone`` () =
    inGitScenario "git-checkout-failure" (fun scenarioPath ->
        let project = uniqueProject ()
        let repoCacheFolder = Constants.GitRepoCacheFolder @@ "localfilesystem" @@ project
        try
            let repo = scenarioPath @@ project
            createRepository repo |> ignore
            // a file name too long for the file systems, so that the checkout fails
            let blob = git repo "rev-parse HEAD:file.txt" |> List.head
            git repo (sprintf "update-index --add --cacheinfo 100644,%s,%s" blob (String('a', 300))) |> ignore
            git repo (asTestAuthor + " commit --no-verify -m commit") |> ignore
            File.WriteAllText(scenarioPath @@ "paket.dependencies", sprintf "git %s main" (fileUrl repo))

            let errors =
                try
                    directPaketInPathEx "install" scenarioPath |> ignore
                    []
                with ProcessFailedWithExitCode(_, _, msgs) -> List.ofSeq msgs.Errors

            errors |> List.exists (fun error -> error.Contains "Checkout to") |> shouldEqual true
            // no clone without checkout is left, which the next run would take for an up-to-date one
            Directory.Exists(scenarioPath @@ "paket-files" @@ "localfilesystem" @@ project) |> shouldEqual false
        finally
            deleteGitCache repoCacheFolder)

[<Test>]
let ``a paket-files clone that a run left without checkout is cloned again`` () =
    inGitScenario "git-clone-without-checkout" (fun scenarioPath ->
        let project = uniqueProject ()
        let repoCacheFolder = Constants.GitRepoCacheFolder @@ "localfilesystem" @@ project
        try
            let repo = scenarioPath @@ project
            createRepository repo |> ignore
            File.WriteAllText(scenarioPath @@ "paket.dependencies", sprintf "git %s main" (fileUrl repo))
            directPaketInPathEx "install" scenarioPath |> ignore

            // as a run whose checkout failed left it, with the locked commit as HEAD but no file
            let paketFiles = scenarioPath @@ "paket-files" @@ "localfilesystem" @@ project
            deleteDir (DirectoryInfo paketFiles)
            git scenarioPath (sprintf "clone --no-checkout %s %s" (quote (fileUrl repo)) (quote paketFiles)) |> ignore

            restoreAgain scenarioPath

            File.ReadAllText(paketFiles @@ "file.txt") |> shouldEqual "content"
        finally
            deleteGitCache repoCacheFolder)

[<Test>]
let ``a paket-files folder without clone leaves the repository of the project alone`` () =
    inGitScenario "git-paket-files-without-clone" (fun scenarioPath ->
        let project = uniqueProject ()
        let repoCacheFolder = Constants.GitRepoCacheFolder @@ "localfilesystem" @@ project
        try
            let repo = scenarioPath @@ project
            createRepository repo |> ignore
            // a project in a repository, whose paket-files were committed without their clone
            let app = scenarioPath @@ "app"
            Directory.CreateDirectory app |> ignore
            git app "init" |> ignore
            File.WriteAllText(app @@ "paket.dependencies", sprintf "git %s main" (fileUrl repo))
            let paketFiles = app @@ "paket-files" @@ "localfilesystem" @@ project
            Directory.CreateDirectory paketFiles |> ignore
            File.WriteAllText(paketFiles @@ "file.txt", "committed")
            git app "add ." |> ignore
            git app (asTestAuthor + " commit --no-verify -m project") |> ignore
            let head = git app "rev-parse HEAD"

            // with --force, the folder goes straight to the checkout
            directPaketInPathEx "install --force" app |> ignore

            git app "rev-parse HEAD" |> shouldEqual head
            git app "tag" |> shouldEqual []
            File.ReadAllText(paketFiles @@ "file.txt") |> shouldEqual "content"
        finally
            deleteGitCache repoCacheFolder)

[<Test>]
let ``a paket-files clone whose .git is a file keeps its changes`` () =
    inGitScenario "git-paket-files-git-file" (fun scenarioPath ->
        let project = uniqueProject ()
        let repoCacheFolder = Constants.GitRepoCacheFolder @@ "localfilesystem" @@ project
        try
            let repo = scenarioPath @@ project
            createRepository repo |> ignore
            File.WriteAllText(scenarioPath @@ "paket.dependencies", sprintf "git %s main" (fileUrl repo))
            directPaketInPathEx "install" scenarioPath |> ignore

            // as a submodule or a worktree, the clone has a .git file that names its repository
            let paketFiles = scenarioPath @@ "paket-files" @@ "localfilesystem" @@ project
            git paketFiles (sprintf "init --separate-git-dir %s" (quote (scenarioPath @@ "separate-git-dir"))) |> ignore
            File.WriteAllText(paketFiles @@ "file.txt", "edited")

            restoreAgain scenarioPath

            File.Exists(paketFiles @@ ".git") |> shouldEqual true
            File.ReadAllText(paketFiles @@ "file.txt") |> shouldEqual "edited"

            // the checkout of a new commit stops at the changes, rather than cloning again over them
            commitFile repo "updated" |> ignore
            let errors =
                try
                    directPaketInPathEx "update" scenarioPath |> ignore
                    []
                with ProcessFailedWithExitCode(_, _, msgs) -> List.ofSeq msgs.Errors
            errors |> List.exists (fun error -> error.Contains "uncommitted changes") |> shouldEqual true
            File.ReadAllText(paketFiles @@ "file.txt") |> shouldEqual "edited"
        finally
            deleteGitCache repoCacheFolder)

[<Test>]
let ``repositories of the same name from two owners get a git cache each`` () =
    inGitScenario "git-cache-owners" (fun scenarioPath ->
        let project = uniqueProject ()
        let owners = [ "owner-a"; "owner-b" ]
        let repoCacheFolders = owners |> List.map (fun owner -> Constants.GitRepoCacheFolder @@ owner @@ project)
        try
            for owner, repoCacheFolder in List.zip owners repoCacheFolders do
                let repo = scenarioPath @@ owner @@ project
                let sha = createRepository repo
                tag repo "1.0.0"
                let range = ModuleResolver.VersionRestriction.VersionRequirement(VersionRequirement(VersionRange.AtLeast "1.0", PreReleaseStatus.No))
                RemoteDownload.getSHA1OfBranch (ModuleResolver.GitLink (Git.Handling.RemoteGitOrigin (fileUrl repo))) owner project range None
                |> Async.RunSynchronously
                |> shouldEqual sha
                mirroredMain repoCacheFolder |> shouldEqual sha
        finally
            repoCacheFolders |> List.iter deleteGitCache)

[<Test>]
let ``a local git repository whose folder name holds a % is checked out`` () =
    inGitScenario "git-percent-in-name" (fun scenarioPath ->
        // "%41" would be "A" in a URL
        let project = uniqueProject () + "%41"
        let repoCacheFolder = Constants.GitRepoCacheFolder @@ "localfilesystem" @@ project
        try
            let repo = scenarioPath @@ project
            createRepository repo |> ignore
            File.WriteAllText(scenarioPath @@ "paket.dependencies", sprintf "git %s main" (fileUrl repo))

            directPaketInPathEx "install" scenarioPath |> ignore

            File.ReadAllText(scenarioPath @@ "paket-files" @@ "localfilesystem" @@ project @@ "file.txt")
            |> shouldEqual "content"
        finally
            deleteGitCache repoCacheFolder)

[<Test>]
let ``an annotated tag of a local git dependency is locked at its commit`` () =
    inGitScenario "git-annotated-tag" (fun scenarioPath ->
        let project = uniqueProject ()
        let repoCacheFolder = Constants.GitRepoCacheFolder @@ "localfilesystem" @@ project
        try
            let repo = scenarioPath @@ project
            let sha = createRepository repo
            tag repo "-a 1.0.0 -m release"
            File.WriteAllText(scenarioPath @@ "paket.dependencies", sprintf "git %s >= 1.0" (fileUrl repo))

            directPaketInPathEx "install" scenarioPath |> ignore

            let lockFile = LockFile.LoadFrom(scenarioPath @@ "paket.lock")
            lockFile.Groups.[Constants.MainDependencyGroup].RemoteFiles.Head.Commit |> shouldEqual sha

            // the restores below fail if they download the dependency again
            let tagObject = git repo "rev-parse 1.0.0" |> List.head
            Directory.Move(repo, scenarioPath @@ "moved")
            deleteDir (DirectoryInfo repoCacheFolder)
            let checkedOutFile () = File.ReadAllText(scenarioPath @@ "paket-files" @@ "localfilesystem" @@ project @@ "file.txt")
            restoreAgain scenarioPath
            checkedOutFile () |> shouldEqual "content"

            // as the lock file of an earlier version, which holds the tag object
            let lockFilePath = scenarioPath @@ "paket.lock"
            File.WriteAllText(lockFilePath, File.ReadAllText(lockFilePath).Replace(sha, tagObject))
            restoreAgain scenarioPath
            checkedOutFile () |> shouldEqual "content"
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

[<Test>]
let ``fetchCache repairs a broken git cache and leaves the repository of a parent folder alone`` () =
    inGitScenario "git-cache-in-repository" (fun scenarioPath ->
        let repo = scenarioPath @@ "repo"
        createRepository repo |> ignore
        // as a dotfiles repository in the home folder
        let home = scenarioPath @@ "home"
        Directory.CreateDirectory home |> ignore
        git home "init" |> ignore
        git home "remote add origin https://example.com/dotfiles.git" |> ignore
        let cache = home @@ "db" @@ "repo"
        Git.Handling.fetchCache cache (fileUrl repo)

        // the cache loses its HEAD and config, as after an interrupted run
        File.Delete(cache @@ "HEAD")
        File.Delete(cache @@ "config")
        let sha = commitFile repo "updated"
        Git.Handling.fetchCache cache (fileUrl repo)

        git home "remote get-url origin" |> shouldEqual [ "https://example.com/dotfiles.git" ]
        mirroredMain cache |> shouldEqual sha)

[<Test>]
let ``the version range of a git dependency is resolved with the tags of the git cache`` () =
    inGitScenario "git-cache-tags" (fun scenarioPath ->
        let project = uniqueProject ()
        let repoCacheFolder = Constants.GitRepoCacheFolder @@ "localfilesystem" @@ project
        try
            let repo = scenarioPath @@ project
            createRepository repo |> ignore
            tag repo "1.0.0"
            let sha = commitFile repo "updated"
            // an annotated tag, whose name gives a tag object and not the commit
            tag repo "-a 2.0.0 -m release"
            let range = ModuleResolver.VersionRestriction.VersionRequirement(VersionRequirement(VersionRange.AtLeast "1.0", PreReleaseStatus.No))
            // a remote origin goes through the git cache, as a remote repository does
            let getSha1 restriction =
                RemoteDownload.getSHA1OfBranch (ModuleResolver.GitLink (Git.Handling.RemoteGitOrigin (fileUrl repo))) "localfilesystem" project restriction None
                |> Async.RunSynchronously

            getSha1 range |> shouldEqual sha
            // a concrete tag goes through ls-remote
            getSha1 (ModuleResolver.VersionRestriction.Concrete "2.0.0") |> shouldEqual sha
        finally
            deleteGitCache repoCacheFolder)
