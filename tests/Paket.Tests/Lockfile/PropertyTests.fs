module Paket.LockFile.PropertyTests

open System
open System.Collections.Generic
open NUnit.Framework
open FsCheck
open Paket

type Generators =
  static member BigInt() =
    { new Arbitrary<bigint>() with
        override x.Generator = Arb.generate<int> |> Gen.map (fun x -> bigint x) }
  static member NonEmptyString() =
    { new Arbitrary<Paket.Domain.NonEmptyString>() with
        override x.Generator = 
            Arb.Default.String()
           |> Arb.filter (fun s -> String.IsNullOrWhiteSpace x |> not) 
           |> Arb.toGen
           |> Gen.map (fun x -> Paket.Domain.NonEmptyString x) }

let removeLineBreaks (text:string) = text.Replace("\r","").Replace("\n","")

let serializeAndParseLockFile (lockfile : LockFile) = 
    let serialized, text1 = 
        try 
            true, lockfile.ToString()
        with _ -> false, ""
    if not serialized then true
    else 
        let parsed = LockFile.Parse(lockfile.FileName, text1.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
        let text2 = parsed.ToString()
        if removeLineBreaks text2 <> removeLineBreaks text1 then 
            printfn "%s" text1
            printfn "%s" text2
            false
        else true
    
[<Test>][<Timeout(200000)>]
let ``serializing and parsing lockfile should give same lockfile`` () =
    Arb.register<Generators>() |> ignore
    Check.QuickThrowOnFailure serializeAndParseLockFile