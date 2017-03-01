//<Expects status="Error" span="(3,1)" id="FS3216">PaketWithBrokenDepsFile</Expects>

#r "paket: "

open XPlot.Plotly

Chart.Line [ 1 .. 10 ]