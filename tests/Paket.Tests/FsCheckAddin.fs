namespace FsCheck.NUnit.Examples

open NUnit.Core.Extensibility

open FsCheck.NUnit
open FsCheck.NUnit.Addin

[<NUnitAddin(Description = "FsCheck addin")>]
type FsCheckAddin() =        
    interface IAddin with
        override x.Install host = 
            let tcBuilder = new FsCheckTestCaseBuider()
            host.GetExtensionPoint("TestCaseBuilders").Install(tcBuilder)
            true
