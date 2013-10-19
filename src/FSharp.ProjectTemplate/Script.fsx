// Learn more about F# at http://fsharp.net. See the 'F# Tutorial' project
// for more guidance on F# programming.

#load "Library.fs"
open FSharp.ProjectTemplate

printfn "%s" <| Greetings.ShoutHello ()
printfn "%s" <| Greetings.SayHello "World"
