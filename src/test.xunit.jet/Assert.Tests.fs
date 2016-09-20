namespace test.xunit.jet

open System
open Xunit
open Xunit.Sdk
open xunit.jet.Assert

module Assert =

        type Ta = { a : string ; b : string }
        type Tb = { c : string }
        type Tc = D of Ta | E of Tb
        type Td = { a : string option }
        type Te = { d : Uri option }
        type Tf = { e : string list }
        type Tg = { g : Ta ; h : Ta }
        type Th = { i : Tg ; j : Tc }

        let [<Fact>] ``equalDeep succeeds for simple record`` () =
            let e = { a = "a" ; b = "b" }
            let a = { a = "a" ; b = "b" }
            equalDeep e a

        let [<Fact>] ``equalDeep fails for simple record`` () =
            let e = { a = "a" ; b = "b" }
            let a = { a = "a" ; b = "c" }
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep succeeds for discriminated union`` () =
            let e = D { a = "a" ; b = "b" }
            let a = D { a = "a" ; b = "b" }
            equalDeep e a

        let [<Fact>] ``equalDeep fails for discriminated union with differing values`` () =
            let e = D { a = "a" ; b = "b" }
            let a = D { a = "a" ; b = "c" }
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep fails for discriminated union with differing values with a null`` () =
            let e = D { a = "a" ; b = "b" }
            let a = D { a = "a" ; b = null }
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep fails for differing discriminated union`` () =
            let e = D { a = "a" ; b = "b" }
            let a = E { c = "c" }
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep succeeds for records with primitive options with values`` () =
            let e = { a = Some "a" }
            let a = { a = Some "a" }
            equalDeep e a

        let [<Fact>] ``equalDeep succeeds for records with primitive options without values`` () =
            let e = { a = None }
            let a = { a = None }
            equalDeep e a

        let [<Fact>] ``equalDeep fails for records with primitive options expecting Some`` () =
            let e = { a = Some "a" }
            let a = { a = None }
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep fails for records with primitive options expecting None`` () =
            let e = { a = None }
            let a = { a = Some "a" }
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep uses original error for unsupported type`` () =
            let e = new Uri("http://localhost.com")
            let a = new Uri("http://localhostB.com")
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep succeeds for unsupported type within option expecting Some`` () =
            let e = { d = new Uri("http://localhost.com") |> Some }
            let a = { d = new Uri("http://localhost.com") |> Some }
            equalDeep e a

        let [<Fact>] ``equalDeep fails for unsupported type within option expecting None`` () =
            let e = { d = None }
            let a = { d = new Uri("http://localhost.com") |> Some }
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep fails for unsupported type within option of mismatched values`` () =
            let e = { d = None }
            let a = { d = new Uri("http://localhost.com") |> Some }
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep succeeds for list within record`` () =
            let e = { e = [ "a" ; "b" ; "c" ] }
            let a = { e = [ "a" ; "b" ; "c" ] }
            equalDeep e a

        let [<Fact>] ``equalDeep fails for list within record`` () =
            let e = { e = [ "a" ; "b" ; "c" ] }
            let a = { e = [ "a" ; "z" ; "c" ] }
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep fails for list with differing values`` () =
            let e = [ "a" ; "b" ; "c" ]
            let a = [ "a" ; "z" ; "c" ]
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep fails for list with differing lengths where actual is shorter than expected`` () =
            let e = [ "a" ; "b" ; "c" ]
            let a = [ "a" ; "b" ]
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep fails for list with differing lengths where actual is longer than expected`` () =
            let e = [ "a" ; "b" ]
            let a = [ "a" ; "b" ; "c" ]
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep succeeds for simple sequence`` () =
            let e = seq { yield "a" ; yield "b" }
            let a = seq { yield "a" ; yield "b" }
            equalDeep e a

        let [<Fact>] ``equalDeep fails for simple sequence of differing string values`` () =
            let e = seq { yield "a" ; yield "b" }
            let a = seq { yield "a" ; yield "c" }
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep fails for simple sequence of differing values`` () =
            let e = seq { yield 1 ; yield 2 }
            let a = seq { yield 1 ; yield 3 }
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep succeeds for simple array of strings`` () =
            let e = [| "a" ; "b" |]
            let a = [| "a" ; "b" |]
            equalDeep e a

        let [<Fact>] ``equalDeep succeeds for simple array of primitives`` () =
            let e = [| 1 ; 2 |]
            let a = [| 1 ; 2 |]
            equalDeep e a

        let [<Fact>] ``equalDeep fails for simple array of strings with differing values`` () =
            let e = [| "a" ; "b" |]
            let a = [| "a" ; "c" |]
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep fails for simple array of primitives with differing values`` () =
            let e = [| 1 ; 2 |]
            let a = [| 1 ; 3 |]
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep succeeds for tuple of primitive types`` () =
            let e = (1, 2)
            let a = (1, 2)
            equalDeep e a

        let [<Fact>] ``equalDeep fails for tuples of primitive types of differing values`` () =
            let e = (1, 2)
            let a = (1, 3)
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)

        let [<Fact>] ``equalDeep fails for deeply nested records of differing values`` () =
            let e = { i = { g = { a = "a" ; b = "b" } ; h = { a = "c" ; b = "d" } } ; j = D { a = "e" ; b = "f" } }
            let a = { i = { g = { a = "a" ; b = "b" } ; h = { a = "z" ; b = "d" } } ; j = D { a = "e" ; b = "f" } }
            Assert.Throws<EqualDeepException> (fun _ -> equalDeep e a)




        // sample unit tests to understand failure capabilities / messaging

        type SampleTypeInner =
            { primitiveOption : int option
              stringList : string list
              intTuple : int * int }

        type SampleTypeOuter =
            | SomeLabel
            | SomeData of SampleTypeInner list

        let expected =
            [ { SampleTypeInner.primitiveOption = None
                SampleTypeInner.stringList = [ "stringOne" ; "stringTwo" ]
                SampleTypeInner.intTuple = (101, 102) }
              { SampleTypeInner.primitiveOption = Some 3
                SampleTypeInner.stringList = []
                SampleTypeInner.intTuple = (201, 202) } ]
            |> SampleTypeOuter.SomeData


        let [<Fact>] ``inequality of record value`` () =
            let actual =
                [ { SampleTypeInner.primitiveOption = None
                    SampleTypeInner.stringList = [ "stringOne" ; "stringTwo" ]
                    SampleTypeInner.intTuple = (101, 102) }
                  { SampleTypeInner.primitiveOption = None
                    SampleTypeInner.stringList = []
                    SampleTypeInner.intTuple = (201, 202) } ]
                |> SampleTypeOuter.SomeData
            equalDeep expected actual

        let [<Fact>] ``inequality of discriminated union label`` () =
            let actual = SampleTypeOuter.SomeLabel
            equalDeep expected actual

        let [<Fact>] ``inequality of list size`` () =
            let actual =
                [ { SampleTypeInner.primitiveOption = None
                    SampleTypeInner.stringList = [ "stringOne" ; "stringTwo" ]
                    SampleTypeInner.intTuple = (101, 102) } ]
                |> SampleTypeOuter.SomeData
            equalDeep expected actual

        let [<Fact>] ``inequality of value within a list`` () =
            let actual =
                [ { SampleTypeInner.primitiveOption = None
                    SampleTypeInner.stringList = [ "stringOne" ; "stringXXX" ]
                    SampleTypeInner.intTuple = (101, 102) }
                  { SampleTypeInner.primitiveOption = Some 3
                    SampleTypeInner.stringList = []
                    SampleTypeInner.intTuple = (201, 202) } ]
                |> SampleTypeOuter.SomeData
            equalDeep expected actual

        let [<Fact>] ``inequality of value within a tuple`` () =
            let actual =
                [ { SampleTypeInner.primitiveOption = None
                    SampleTypeInner.stringList = [ "stringOne" ; "stringTwo" ]
                    SampleTypeInner.intTuple = (101, 102) }
                  { SampleTypeInner.primitiveOption = Some 3
                    SampleTypeInner.stringList = []
                    SampleTypeInner.intTuple = (201, 999) } ]
                |> SampleTypeOuter.SomeData
            equalDeep expected actual
