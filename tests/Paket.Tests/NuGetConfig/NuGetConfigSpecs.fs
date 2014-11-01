module Paket.NuGetConfigSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.NuGetConvert
open System.Xml
open PackageSources

let emptyConfig : NugetConfig =
    { PackageSources = [] 
      PackageRestoreEnabled = false 
      PackageRestoreAutomatic = false }

let parse (fileName:string) =
    let doc = XmlDocument() 
    doc.Load(fileName)
    NuGetConvert.applyConfig emptyConfig doc


[<Test>]
let ``can detect passwords in nuget.config``() = 
    parse "NuGetConfig/PasswordConfig.xml" 
    |> shouldEqual
        { PackageSources = 
            [ PackageSource.Nuget { Url = "https://www.nuget.org/api/v2/"; Auth = None }
                                                                  
              PackageSource.Nuget 
                    { Url = "https://tc/httpAuth/app/nuget/v1/FeedService.svc/"
                      Auth = Some { Username = AuthEntry.Create "notty"; Password = AuthEntry.Create "adfdsfadsadadfsafdsadfsafsd" } } ]
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