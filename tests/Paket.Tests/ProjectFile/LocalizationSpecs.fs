module Paket.ProjectFile.LocalizationSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.Xml
open System.Xml.Linq
open TestHelpers

[<Test>]
let ``finds language names`` () =
    ensureDir ()
    let actual = ProjectFile.TryLoad("./ProjectFile/TestData/LocalizedLib.csprojtest")
                            .Value
                            .FindLocalizedLanguageNames()
    let expected = 
        [
            "sv"
            "sv-FI"
        ]
    CollectionAssert.AreEqual(expected, actual)

[<Test>]
let ``returns empty when no localization`` () =
    ensureDir ()
    let actual = ProjectFile.TryLoad("./ProjectFile/TestData/NewSilverlightClassLibrary.csprojtest")
                            .Value
                            .FindLocalizedLanguageNames()
    CollectionAssert.IsEmpty(actual)

