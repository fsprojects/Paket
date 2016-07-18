// Learn more about F# at http://fsharp.org
module MyProgram

open System

[<EntryPoint>]
let main argv = 
    printfn "Hello World!"
    printfn "%A" argv
    0 // return an integer exit code
