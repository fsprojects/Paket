module Paket.FrameworkHandling.FrameworkIdentifierSpecs

open System.IO
open Paket
open Paket.Domain
open Chessie.ErrorHandling
open FsUnit
open NUnit.Framework
open TestHelpers
open Paket.Requirements

let silverlight x = FrameworkIdentifier.Silverlight(x)
let dotnet x = FrameworkIdentifier.DotNetFramework(x)

[<Test>]
let ``.>= should work for dotnet``() = 
    dotnet FrameworkVersion.V4 .>= dotnet FrameworkVersion.V1
    |> shouldEqual true
    dotnet FrameworkVersion.V4 .>= dotnet FrameworkVersion.V4
    |> shouldEqual true
    dotnet FrameworkVersion.V4 .>= dotnet FrameworkVersion.V4_5
    |> shouldEqual false

[<Test>]
let ``.> should work for dotnet``() = 
    dotnet FrameworkVersion.V4 .> dotnet FrameworkVersion.V1
    |> shouldEqual true
    dotnet FrameworkVersion.V4 .> dotnet FrameworkVersion.V4
    |> shouldEqual false
    dotnet FrameworkVersion.V4 .> dotnet FrameworkVersion.V4_5
    |> shouldEqual false

[<Test>]
let ``.<= should work for dotnet``() = 
    dotnet FrameworkVersion.V4 .<= dotnet FrameworkVersion.V1
    |> shouldEqual false
    dotnet FrameworkVersion.V4 .<= dotnet FrameworkVersion.V4
    |> shouldEqual true
    dotnet FrameworkVersion.V4 .<= dotnet FrameworkVersion.V4_5
    |> shouldEqual true

[<Test>]
let ``.< should work for dotnet``() = 
    dotnet FrameworkVersion.V4 .< dotnet FrameworkVersion.V1
    |> shouldEqual false
    dotnet FrameworkVersion.V4 .< dotnet FrameworkVersion.V4
    |> shouldEqual false
    dotnet FrameworkVersion.V4 .< dotnet FrameworkVersion.V4_5
    |> shouldEqual true

[<Test>]
let ``.>= should work for silverlight``() = 
    silverlight "v4.0" .>= silverlight "v3.0"
    |> shouldEqual true
    silverlight "v4.0" .>= silverlight "v4.0"
    |> shouldEqual true
    silverlight "v4.0" .>= silverlight "v5.0"
    |> shouldEqual false

[<Test>]
let ``.> should work for silverlight``() = 
    silverlight "v4.0" .> silverlight "v3.0"
    |> shouldEqual true
    silverlight "v4.0" .> silverlight "v4.0"
    |> shouldEqual false
    silverlight "v4.0" .> silverlight "v5.0"
    |> shouldEqual false

[<Test>]
let ``.<= should work for silverlight``() = 
    silverlight "v4.0" .<= silverlight "v3.0"
    |> shouldEqual false
    silverlight "v4.0" .<= silverlight "v4.0"
    |> shouldEqual true
    silverlight "v4.0" .<= silverlight "v5.0"
    |> shouldEqual true

[<Test>]
let ``.< should work for silverlight``() = 
    silverlight "v4.0" .< silverlight "v3.0"
    |> shouldEqual false
    silverlight "v4.0" .< silverlight "v4.0"
    |> shouldEqual false
    silverlight "v4.0" .< silverlight "v5.0"
    |> shouldEqual true

[<Test>]
let ``Same framework can be detected``() =
    FrameworkIdentifier.IsSameFramework (silverlight "v4.0") (silverlight "v3.0")
    |> shouldEqual true
    FrameworkIdentifier.IsSameFramework (dotnet FrameworkVersion.V4) (dotnet FrameworkVersion.V1)
    |> shouldEqual true
    FrameworkIdentifier.IsSameFramework (dotnet FrameworkVersion.V4) (silverlight "v4.0")
    |> shouldEqual false

[<Test>]
let ``Min works for dotnet``() =
    FrameworkIdentifier.Min (dotnet FrameworkVersion.V4) (dotnet FrameworkVersion.V1)
    |> shouldEqual (dotnet FrameworkVersion.V1)
    FrameworkIdentifier.Min (dotnet FrameworkVersion.V1) (dotnet FrameworkVersion.V4)
    |> shouldEqual (dotnet FrameworkVersion.V1)
    FrameworkIdentifier.Min (dotnet FrameworkVersion.V4) (dotnet FrameworkVersion.V4)
    |> shouldEqual (dotnet FrameworkVersion.V4)

[<Test>]
let ``Max works for dotnet``() =
    FrameworkIdentifier.Max (dotnet FrameworkVersion.V4) (dotnet FrameworkVersion.V1)
    |> shouldEqual (dotnet FrameworkVersion.V4)
    FrameworkIdentifier.Max (dotnet FrameworkVersion.V1) (dotnet FrameworkVersion.V4)
    |> shouldEqual (dotnet FrameworkVersion.V4)
    FrameworkIdentifier.Max (dotnet FrameworkVersion.V4) (dotnet FrameworkVersion.V4)
    |> shouldEqual (dotnet FrameworkVersion.V4)

[<Test>]
let ``Min works for silverlight``() =
    FrameworkIdentifier.Min (silverlight "v4.0") (silverlight "v3.0")
    |> shouldEqual (silverlight "v3.0")
    FrameworkIdentifier.Min (silverlight "v3.0") (silverlight "v4.0")
    |> shouldEqual (silverlight "v3.0")
    FrameworkIdentifier.Min (silverlight "v4.0") (silverlight "v4.0")
    |> shouldEqual (silverlight "v4.0")

[<Test>]
let ``Max works for silverlight``() =
    FrameworkIdentifier.Max (silverlight "v4.0") (silverlight "v3.0")
    |> shouldEqual (silverlight "v4.0")
    FrameworkIdentifier.Max (silverlight "v3.0") (silverlight "v4.0")
    |> shouldEqual (silverlight "v4.0")
    FrameworkIdentifier.Max (silverlight "v4.0") (silverlight "v4.0")
    |> shouldEqual (silverlight "v4.0")

[<Test; ExpectedException>]
let ``Max throw for mixed framework``() =
    FrameworkIdentifier.Max (silverlight "v4.0") (dotnet FrameworkVersion.V4)
    |> ignore

[<Test; ExpectedException>]
let ``Min throw for mixed framework``() =
    FrameworkIdentifier.Min (silverlight "v4.0") (dotnet FrameworkVersion.V4)
    |> ignore