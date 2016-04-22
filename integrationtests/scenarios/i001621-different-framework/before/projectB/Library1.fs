namespace projectB
open NUnit.Framework
[<TestFixture>]
type Class1() = 
    static member X = projectA.Class1.X
    [<Test>]
    member x.Test() =
      printfn "%s" Class1.X
      let a = projectA.Class1()
      a.Test()
      Assert.Ignore()