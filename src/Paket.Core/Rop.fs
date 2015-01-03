module Paket.Rop

type RopResult<'TSuccess, 'TMessage> =    
    | Success of 'TSuccess * 'TMessage list
    | Failure of 'TMessage list

let succeed x = Success(x,[])

let fail msg = Failure([msg])

let either fSuccess fFailure = function
    | Success(x, msgs) -> fSuccess(x,msgs)
    | Failure(msgs) -> fFailure(msgs)

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

let collect xs =
    Seq.fold (fun result next -> 
                    match result, next with
                    | Success(rs,m1), Success(r,m2) -> Success(r::rs,m1@m2)
                    | Success(_), Failure(m) 
                    | Failure(m), Success(_) -> Failure(m)
                    | Failure(m1), Failure(m2) -> Failure(m1@m2)) (succeed []) xs

let failIfNone message = function
    | Some x -> succeed x
    | None -> fail message 

/// infix version of Rop.bind
let (>>=) result f = bind f result

/// infix version of Rop.lift
let (<!>) = lift

/// infix version of Rop.apply
let (<*>) = apply