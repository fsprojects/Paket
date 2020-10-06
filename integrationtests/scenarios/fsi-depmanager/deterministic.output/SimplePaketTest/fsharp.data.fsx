#r "paket: nuget FSharp.Data"

open FSharp.Data

let v = FSharp.Data.JsonValue.Boolean true
printfn "%A" v