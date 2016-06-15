namespace projectA

open NUnit.Framework
[<TestFixture>]
type Class1() = 
    static member X = "F#"
    [<Test>]
    member x.Test() =
      Assert.Ignore()