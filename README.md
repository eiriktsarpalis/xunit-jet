# xunit-jet - Jet.com's F# extension to xUnit

**xUnit-Jet** is an extension to **xUnit** that understands native F# types and is capable of doing deep & informative comparisons.

# Deets

## Why?

[**xUnit**](https://github.com/xunit/xunit) is a powerful library that is great for unit testing, but has no understanding of native F# types. If you run `Assert.Equal(<expected>, <actual>)`, it is able to detect inequality between F# types, but provide no meaningful information.

## What it does

Currently, there is one function, `Assert.equalDeep <expected> <actual>`, which is an extension of `Assert.Equal`. It executes `Assert.Equal`, and if inequality is detected, `equalDeep` performs deep introspection of F# types to find the first granular point of inequality. Deep details are provided on what the differences are.

## What's done

## What's pending

# Examples


# Maintainer(s)

- Rand Davis ([@randalldavis](https://github.com/randalldavis))

# License

This project is subject to the Apache Licence, Version 2.0. A copy of the license can be found in [LICENSE.txt](LICENSE.txt) at the root of this repo.

# Code of Conduct 

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. This code of conduct has been [adopted by many other projects](http://contributor-covenant.org/adopters/).
