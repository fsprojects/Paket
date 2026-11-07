module Paket.GitHandlingSpecs

open Paket.Git.Handling
open NUnit.Framework
open FsUnit

[<Test>]
let ``localPathOfFileUrl keeps the leading slash of a Unix path``() =
    localPathOfFileUrl "file:///tmp/x" |> shouldEqual "/tmp/x"

[<Test>]
let ``localPathOfFileUrl reads a Windows path``() =
    localPathOfFileUrl "file:///C:/code/repo" |> shouldEqual "C:/code/repo"

[<Test>]
let ``localPathOfFileUrl reads a Windows path with a lowercase drive``() =
    localPathOfFileUrl "file:///c:/code/repo" |> shouldEqual "c:/code/repo"

[<Test>]
let ``localPathOfFileUrl reads a Windows path with backslashes``() =
    localPathOfFileUrl @"file:///C:\Users\Steffen\AskMe" |> shouldEqual @"C:\Users\Steffen\AskMe"

[<Test>]
let ``localPathOfFileUrl keeps the extra slash of file:////``() =
    localPathOfFileUrl "file:////tmp/x" |> shouldEqual "//tmp/x"

[<Test>]
let ``localPathOfFileUrl decodes the escaped characters``() =
    localPathOfFileUrl "file:///tmp/a%20b" |> shouldEqual "/tmp/a b"

[<Test>]
let ``localPathOfFileUrl keeps a path without file://``() =
    localPathOfFileUrl "/tmp/x" |> shouldEqual "/tmp/x"

let private projectOf gitConfig =
    let _,_,project,_,_,_,_ = extractUrlParts gitConfig
    project

[<Test>]
let ``the project of a local repository is its folder name``() =
    projectOf "file:///C:/code/Repo.git master" |> shouldEqual "Repo"
    projectOf "file:///tmp/code/Repo" |> shouldEqual "Repo"

[<Test>]
let ``the project of a repository keeps a .git that doesn't end its name``() =
    projectOf "file:///tmp/code/me.github.io" |> shouldEqual "me.github.io"
    projectOf "https://github.com/me/me.github.io.git" |> shouldEqual "me.github.io"
    projectOf "git@github.com:fsprojects/Paket.git" |> shouldEqual "Paket"

[<Test>]
let ``the project of a repository ignores the case of its .git``() =
    projectOf "file:///C:/code/Repo.GIT master" |> shouldEqual "Repo"
    projectOf "file:///tmp/code/Repo/.GIT master" |> shouldEqual "Repo"
    projectOf "https://github.com/me/Repo.Git" |> shouldEqual "Repo"

[<Test>]
let ``the project of a local repository with a Windows path is its folder name``() =
    projectOf @"file:///C:\code\Repo master" |> shouldEqual "Repo"

[<Test>]
let ``the project of a local repository whose path ends with a backslash is its folder name``() =
    projectOf @"file:///C:\code\Repo\ master" |> shouldEqual "Repo"

[<Test>]
let ``the project of a local repository given by its .git folder is the folder of the .git``() =
    projectOf "file:///tmp/code/Repo/.git master" |> shouldEqual "Repo"
    projectOf @"file:///C:\code\Repo\.git\ master" |> shouldEqual "Repo"

[<Test>]
let ``a local repository at the root of a file system has no project``() =
    (fun () -> projectOf "file:///.git master" |> ignore) |> shouldFail<exn>
    (fun () -> projectOf "file:///C:/.git master" |> ignore) |> shouldFail<exn>
    (fun () -> projectOf "file:///C:/ master" |> ignore) |> shouldFail<exn>
    (fun () -> projectOf @"file:///C:\.git master" |> ignore) |> shouldFail<exn>

[<Test>]
let ``a repository whose path ends with a dot segment has no project``() =
    (fun () -> projectOf "file:///tmp/code/Repo/. master" |> ignore) |> shouldFail<exn>
    (fun () -> projectOf "file:///tmp/code/Repo/.. master" |> ignore) |> shouldFail<exn>
    (fun () -> projectOf @"file:///C:\code\Repo\. master" |> ignore) |> shouldFail<exn>
    (fun () -> projectOf "file:///tmp/code/..git master" |> ignore) |> shouldFail<exn>
    (fun () -> projectOf "https://example.com/me/..git" |> ignore) |> shouldFail<exn>

[<Test>]
let ``a remote repository that gives no folder name has no project``() =
    (fun () -> projectOf "https://example.com/me/.git/.git" |> ignore) |> shouldFail<exn>

let private ownerAndProjectOf gitConfig =
    let server,_,project,_,_,_,_ = extractUrlParts gitConfig
    server, project

[<Test>]
let ``a remote repository given by its .git folder is named after the folder of the .git``() =
    ownerAndProjectOf "ssh://host/home/me/repo/.git" |> shouldEqual ("host/home/me", "repo")
    ownerAndProjectOf "ssh://host/home/me/repo" |> shouldEqual ("host/home/me", "repo")
    ownerAndProjectOf "https://example.com/me/repo/.GIT" |> shouldEqual ("example.com/me", "repo")
    ownerAndProjectOf "git@example.com:me/repo/.git" |> shouldEqual ("example.com/me", "repo")

[<Test>]
let ``the project of a local repository with an escaped path is its decoded folder name``() =
    projectOf "file:///tmp/my%20repo" |> shouldEqual "my repo"
