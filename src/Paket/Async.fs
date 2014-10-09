namespace FSharp.Control

[<AutoOpen>]
module AsyncExtensions = 

  type Microsoft.FSharp.Control.Async with 
    static member Parallel (a : Async<'a>, b : Async<'b>) : Async<'a * 'b> =
        async {
            let! a' = Async.StartChild a
            let! b' = Async.StartChild b
            let! a'' = a'
            let! b'' = b'
            return (a'',b'')
        }