# xunit-jet - Jet.com's F# extension to xUnit

**xUnit-Jet** is an extension to **xUnit** that understands native F# types and is capable of doing deep & informative comparisons.

# Deets

## Why?

[**xUnit**](https://github.com/xunit/xunit) is a powerful library that is great for unit testing, but has no understanding of native F# types. If you run `Assert.Equal(<expected>, <actual>)`, it is able to detect inequality between F# types, but provide no meaningful information.

## What it does

Currently, there is one function, `Assert.equalDeep <expected> <actual>`, which is an extension of `Assert.Equal`. It executes `Assert.Equal`, and if inequality is detected, `equalDeep` performs deep introspection of F# types to find the first granular point of inequality. Details are provided on what the differences are.

## What's done

Recursive handling of generics, lists, arrays, sequences, records, discriminated unions, tuples, and primitives.

## What's pending

Handling of maps and sets.

## Known issues

None! Expect this list to grow though...

# Examples

```fsharp

type SampleTypeInner =
    { foo : int option
      bar : string list }

type SampleTypeOuter =
    | SomeLabel
    | SomeData of SampleTypeInner list

let expected =
    [ { SampleTypeInner.foo = None
        SampleTypeInner.bar = [ "one" ; "two" ] }
      { SampleTypeInner.foo = Some 3
        SampleTypeInner.bar = [] } ]
    |> SampleTypeOuter.SomeData


let [<Fact>] ``sample1`` () =
    let actual = SampleTypeOuter.SomeLabel
    equalDeep expected actual
```
<img src="https://github.com/jet/xunit-jet/blob/master/meta/images/Sample1.PNG" width="100%" height="100%" border="10"/>


```fsharp

let [<Fact>] ``sample2`` () =
    let actual =
        [ { SampleTypeInner.foo = None
            SampleTypeInner.bar = [ "one" ; "two" ] } ]
        |> SampleTypeOuter.SomeData
    equalDeep expected actual
```
<img src="https://github.com/jet/xunit-jet/blob/master/meta/images/Sample2.PNG" width="100%" height="100%" border="10"/>
    
```fsharp

let [<Fact>] ``sample3`` () =
    let actual =
        [ { SampleTypeInner.foo = None
            SampleTypeInner.bar = [ "one" ; "xxx" ] }
          { SampleTypeInner.foo = Some 3
            SampleTypeInner.bar = [] } ]
        |> SampleTypeOuter.SomeData
    equalDeep expected actual
```
<img src="https://github.com/jet/xunit-jet/blob/master/meta/images/Sample3.PNG" width="100%" height="100%" border="10"/>

# Maintainer(s)

- Rand Davis ([@randalldavis](https://github.com/randalldavis))

# License

This project is subject to the Apache Licence, Version 2.0. A copy of the license can be found in [LICENSE.txt](LICENSE.txt) at the root of this repo.

# Code of Conduct 

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. This code of conduct has been [adopted by many other projects](http://contributor-covenant.org/adopters/).
