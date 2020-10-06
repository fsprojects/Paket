#r "paket: nuget XPlot.Plotly"
open XPlot.Plotly

printfn "%A" (Chart.Line [ 1 .. 10 ])
printfn "ok"
