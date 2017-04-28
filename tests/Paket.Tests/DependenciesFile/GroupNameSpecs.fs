namespace  Paket.DependenciesFile

open Paket
open NUnit.Framework

[<TestFixture(Category=Category.DependenciesFile)>]
module GroupNameSpecs =

    open Paket
    open Paket.Domain
    open NUnit.Framework
    open FsUnit

    [<Test>]
    let ``should throw on prohibited group names``() = 
        shouldFail<System.ArgumentException>(fun () -> GroupName("lib") |> ignore)
        shouldFail<System.ArgumentException>(fun () -> GroupName("runtimes") |> ignore)
