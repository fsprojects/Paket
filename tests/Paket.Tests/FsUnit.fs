namespace FsUnit

open NUnit.Framework
open NUnit.Framework.Constraints


//
[<AutoOpen>]
module TopLevelOperators =
    let Null = NullConstraint()

    let Empty = EmptyConstraint()

    let EmptyString = EmptyStringConstraint()

    let NullOrEmptyString = NullOrEmptyStringConstraint()

    let True = TrueConstraint()

    let False = FalseConstraint()

    let NaN = NaNConstraint()

    let unique = UniqueItemsConstraint()

    let should (f : 'a -> #Constraint) x (y : obj) =
        let c = f x
        let y =
            match y with
            | :? (unit -> unit) -> box (TestDelegate(y :?> unit -> unit))
            | _ -> y
        Assert.That(y, c)
    
    let equal x = EqualConstraint(x)

    let equalWithin tolerance x = equal(x).Within tolerance

    let contain x = ContainsConstraint(x)
    let notContain x = NotConstraint(ContainsConstraint(x))

    let haveLength n = Has.Length.EqualTo(n)

    let haveCount n = Has.Count.EqualTo(n)

    let be = id

    let sameAs x = SameAsConstraint(x)

    let throw = Throws.TypeOf

    let greaterThan x = GreaterThanConstraint(x)

    let greaterThanOrEqualTo x = GreaterThanOrEqualConstraint(x)

    let lessThan x = LessThanConstraint(x)

    let lessThanOrEqualTo x = LessThanOrEqualConstraint(x)

    let shouldFail (f : unit -> unit) =
        TestDelegate(f) |> should throw typeof<AssertionException>

    let endWith (s:string) = EndsWithConstraint s

    let startWith (s:string) = StartsWithConstraint s

    let ofExactType<'a> = ExactTypeConstraint(typeof<'a>)

    let instanceOfType<'a> = InstanceOfTypeConstraint(typeof<'a>)

    let not' x = NotConstraint(x)

    /// Deprecated operators. These will be removed in a future version of FsUnit.
    module FsUnitDepricated =
        [<System.Obsolete>]
        let not x = not' x

open System.Diagnostics
open NUnit.Framework
open NUnit.Framework.Constraints

type Recorder<'T>() =
  let mutable xs = []
  member recorder.Record(x:'T) = xs <- x :: xs
  member recorder.Values = xs

[<AutoOpen>]
module Extensions =
  // like "should equal", but validates same-type
  let shouldEqual (x: 'a) (y: 'a) = Assert.AreEqual(x, y, sprintf "Expected: %A\nActual: %A" x y)
  let shouldBeSmallerThan (x: 'a) (y: 'a) = Assert.GreaterOrEqual(x, y, sprintf "Expected: %A\nActual: %A" x y)
  let shouldBeGreaterThan (x: 'a) (y: 'a) = Assert.Greater(y, x, sprintf "Expected: %A\nActual: %A" x y)
  let notEqual x = new NotConstraint(new EqualConstraint(x))

  let inline spy1 (recorder:Recorder<'T>) f = fun p -> recorder.Record(p); f p
  let inline spy2 (recorder:Recorder<'T1 * 'T2>) f = fun p1 p2 -> recorder.Record( (p1, p2) ); f p1 p2 
