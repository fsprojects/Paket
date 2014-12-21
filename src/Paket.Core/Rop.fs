module Paket.Rop

type RopResult<'TSuccess, 'TMessage> =    
    | Success of 'TSuccess * 'TMessage list
    | Failure of 'TMessage list

let succeed x = Success(x,[])

let failure msg = Failure([msg])

let either fSuccess fFailure = function
    | Success(x, msgs) -> fSuccess(x,msgs)
    | Failure(msgs) -> fFailure(msgs)