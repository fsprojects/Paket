#I @"..\..\bin"
#r "Paket.Core.dll"

open Paket

Dependencies.Locate(__SOURCE_DIRECTORY__)

Dependencies.Add "FAKE"