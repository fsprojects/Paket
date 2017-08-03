module Paket.NuGetConfigSpecs

open System.IO
open Pri.LongPath
open Paket
open NUnit.Framework
open FsUnit
open Paket.NuGetConvert
open PackageSources
open System.Security.Cryptography
open System.Text
open System
open Chessie.ErrorHandling
open System.Xml
open TestHelpers

let parse fileName = 
    FileInfo(fileName)
    |> NugetConfig.GetConfigNode
    |> returnOrFail
    |> NugetConfig.OverrideConfig NugetConfig.Empty

[<Test>]
let ``can detect encrypted passwords in nuget.config``() = 
    ensureDir()
    // encrypted password is machine-specific, thus cannot be hardcoded in test file and needs to be generated dynamically
    let encrypted = 
        ProtectedData.Protect(
            Encoding.UTF8.GetBytes "secret", 
            Encoding.UTF8.GetBytes "NuGet", 
            DataProtectionScope.CurrentUser) 
        |> Convert.ToBase64String

    let originalFile = File.ReadAllText("NuGetConfig/PasswordConfig.xml")
    try
        let withPasswordFilled = originalFile.Replace("placeholder-for-password", encrypted)
        if originalFile = withPasswordFilled then
            failwith "could not replace password in NuGetConfig/PasswordConfig.xml"

        File.WriteAllText("NuGetConfig/PasswordConfig.xml", withPasswordFilled)
    
        parse "NuGetConfig/PasswordConfig.xml" 
        |> shouldEqual
            { PackageSources = 
                [ "https://www.nuget.org/api/v2/", ("https://www.nuget.org/api/v2/",None)
                  "tc", ("https://tc/httpAuth/app/nuget/v1/FeedService.svc/", Some(Credentials("notty", "secret"))) ]
                |> Map.ofList
              PackageRestoreEnabled = false
              PackageRestoreAutomatic = false }
    finally
        File.WriteAllText("NuGetConfig/PasswordConfig.xml", originalFile)

[<Test>]
let ``can detect cleartextpasswords in nuget.config``() = 
    ensureDir()
    parse "NuGetConfig/ClearTextPasswordConfig.xml" 
    |> shouldEqual
        { PackageSources =
            [ "https://www.nuget.org/api/v2/", ("https://www.nuget.org/api/v2/",None)
              "somewhere", ("https://nuget/somewhere/",Some (Credentials("myUser", "myPassword"))) ]
            |> Map.ofList 
          PackageRestoreEnabled = false
          PackageRestoreAutomatic = false }

[<Test>]
let ``ignores disabled nuget feed`` () =
    ensureDir()
    parse "NuGetConfig/ConfigWithDisabledFeed.xml"
    |> shouldEqual
        { PackageSources = 
            [ "nuget.org", ("https://www.nuget.org/api/v2/",None) ]
            |> Map.ofList
          PackageRestoreEnabled = true
          PackageRestoreAutomatic = true }

[<Test>]
let ``can parse config in XML node`` () =
    let doc = XmlDocument()
    let file = FileInfo "NuGetConfig/ConfigWithDisabledFeedFromUpstream.xml"
    doc.Load(file.FullName)

[<Test>]
let ``ignores disabled nuget feed from upstream`` () =
    ensureDir()
    let upstream = 
        { NugetConfig.Empty with
            PackageSources = 
                [ "MyGetDuality", ("https://www.myget.org/F/6416d9912a7c4d46bc983870fb440d25/", None) ]
                |> Map.ofList }
    
    let next = NugetConfig.GetConfigNode (FileInfo "NuGetConfig/ConfigWithDisabledFeedFromUpstream.xml") |> Trial.returnOrFail
    let overridden = NugetConfig.OverrideConfig upstream next
    overridden
    |> shouldEqual
        { PackageSources = 
            [ "nuget.org", ("https://www.nuget.org/api/v2/",None) ]
            |> Map.ofList
          PackageRestoreEnabled = true
          PackageRestoreAutomatic = true }