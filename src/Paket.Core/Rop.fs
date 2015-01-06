module Paket.Rop

open System

type RopResult<'TSuccess, 'TMessage> =    
    | Success of 'TSuccess * 'TMessage list
    | Failure of 'TMessage list

let succeed x = Success(x,[])

let fail msg = Failure([msg])

let either fSuccess fFailure = function
    | Success(x, msgs) -> fSuccess(x,msgs)
    | Failure(msgs) -> fFailure(msgs)

let returnOrFail result = 
    let raiseExn msgs = 
        msgs 
        |> Seq.map (sprintf "%O")
        |> String.concat (Environment.NewLine + "\t")
        |> failwith
    either fst raiseExn result

let mergeMessages msgs result =
    let fSuccess (x,msgs2) = 
        Success (x, msgs @ msgs2) 
    let fFailure errs = 
        Failure (errs @ msgs) 
    either fSuccess fFailure result

let bind f result =
    let fSuccess (x, msgs) =
        f x |> mergeMessages msgs
    let fFailure (msgs) =
        Failure msgs
    either fSuccess fFailure result      

let apply f result =
    match f,result with
    | Success (f,msgs1), Success (x,msgs2) -> 
        (f x, msgs1@msgs2) |> Success 
    | Failure errs, Success (_,msgs) 
    | Success (_,msgs), Failure errs -> 
        errs @ msgs |> Failure
    | Failure errs1, Failure errs2 -> 
        errs1 @ errs2 |> Failure 

let lift f result =
    let f' = f |> succeed
    apply f' result

let successTee f result = 
    let fSuccess (x,msgs) = 
        f (x,msgs)
        Success (x,msgs) 
    let fFailure errs = Failure errs 
    either fSuccess fFailure result

let failureTee f result = 
    let fSuccess (x,msgs) = Success (x,msgs) 
    let fFailure errs = 
        f errs
        Failure errs 
    either fSuccess fFailure result

let collect xs =
    Seq.fold (fun result next -> 
                    match result, next with
                    | Success(rs,m1), Success(r,m2) -> Success(r::rs,m1@m2)
                    | Success(_,m1), Failure(m2) 
                    | Failure(m1), Success(_,m2) -> Failure(m1@m2)
                    | Failure(m1), Failure(m2) -> Failure(m1@m2)) (succeed []) xs
    |> lift List.rev

let failIfNone message = function
    | Some x -> succeed x
    | None -> fail message 

/// infix version of Rop.bind
let (>>=) result f = bind f result

/// infix version of Rop.lift
let (<!>) = lift

/// infix version of Rop.apply
let (<*>) = apply