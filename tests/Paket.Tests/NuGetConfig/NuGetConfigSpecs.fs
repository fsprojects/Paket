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

let parse fileName = NugetConfig.empty.ApplyConfig fileName

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
            [ PackageSource.Nuget { Url = "https://www.nuget.org/api/v2/"; Auth = None }
                                                                  
              PackageSource.Nuget 
                    { Url = "https://tc/httpAuth/app/nuget/v1/FeedService.svc/"
                      Auth = Some { Username = AuthEntry.Create "notty"; Password = AuthEntry.Create "secret" } } ]
          PackageRestoreEnabled = false
          PackageRestoreAutomatic = false }

[<Test>]
let ``can detect cleartextpasswords in nuget.config``() = 
    parse "NuGetConfig/ClearTextPasswordConfig.xml" 
    |> shouldEqual
        { PackageSources = 
            [ PackageSource.Nuget { Url = "https://www.nuget.org/api/v2/"; Auth = None }
                                                                  
              PackageSource.Nuget 
                    { Url = "https://nuget/somewhere/"
                      Auth = Some { Username = AuthEntry.Create "myUser"; Password = AuthEntry.Create "myPassword" } } ]
          PackageRestoreEnabled = false
          PackageRestoreAutomatic = false }