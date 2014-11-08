#r @"..\..\bin\Paket.Core.dll"

open Paket

Dependencies.Locate(__SOURCE_DIRECTORY__)

Dependencies.Add "FAKE"
Dependencies.Add "FSharp.Data"

Dependencies.GetInstalledVersion "FAKE"