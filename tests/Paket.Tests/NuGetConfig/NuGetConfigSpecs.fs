module Paket.NuGetConfigSpecs

open System.IO
open Paket
open NUnit.Framework
open FsUnit
open Paket.NuGetConvert
open PackageSources
open System.Security.Cryptography
open System.Text
open System
open Chessie.ErrorHandling

let parse fileName = 
    FileInfo(fileName)
    |> NugetConfig.getConfigNode
    |> returnOrFail
    |> NugetConfig.overrideConfig NugetConfig.empty

[<Test>]
let ``can detect encrypted passwords in nuget.config``() = 
    // encrypted password is machine-specific, thus cannot be hardcoded in test file and needs to be generated dynamically
    let encrypted = 
        ProtectedData.Protect(
            Encoding.UTF8.GetBytes "secret", 
            Encoding.UTF8.GetBytes "NuGet", 
            DataProtectionScope.CurrentUser) 
        |> Convert.ToBase64String
    let withPasswordFilled = File.ReadAllText("NuGetConfig/PasswordConfig.xml").Replace("placeholder-for-password", encrypted)
    File.WriteAllText("NuGetConfig/PasswordConfig.xml", withPasswordFilled)
    
    parse "NuGetConfig/PasswordConfig.xml" 
    |> shouldEqual
        { PackageSources = 
            [ "https://www.nuget.org/api/v2/",None
              "https://tc/httpAuth/app/nuget/v1/FeedService.svc/",
                      Some { Username = "notty"; Password = "secret" } ]
          PackageRestoreEnabled = false
          PackageRestoreAutomatic = false }

[<Test>]
let ``can detect cleartextpasswords in nuget.config``() = 
    parse "NuGetConfig/ClearTextPasswordConfig.xml" 
    |> shouldEqual
        { PackageSources = 
            [ "https://www.nuget.org/api/v2/",None
              "https://nuget/somewhere/",
                      Some { Username = "myUser"; Password = "myPassword" } ]
          PackageRestoreEnabled = false
          PackageRestoreAutomatic = false }

[<Test>]
let ``ignores disabled nuget feed`` () =
    parse "NuGetConfig/ConfigWithDisabledFeed.xml"
    |> shouldEqual
        { PackageSources = 
            [ "https://www.nuget.org/api/v2/",None]
          PackageRestoreEnabled = true
          PackageRestoreAutomatic = true }