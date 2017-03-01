//<Expects status="Error" span="(3,1)" id="FS3216">Package resolution using</Expects>

#r "paket: nuget SomeInvalidNugetPackage"

open XPlot.Plotly

Chart.Line [ 1 .. 10 ]