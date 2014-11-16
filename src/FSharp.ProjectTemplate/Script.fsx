// Learn more about F# at http://fsharp.org. See the 'F# Tutorial' project
// for more guidance on F# programming.

#load "Library.fs"
open FSharp.ProjectTemplate

let num = Library.hello 42
printfn "%i" num
