HistogramUncertain\<T\>
=======================

This extension to Uncertain\<T\> is an experiment with two goals:

* Representing uncertainty *efficiently,* especially when you need to serialize uncertain values for storage or communication.
* Providing a library of techniques to *lift* black-box, uncertainty-unaware code into an Uncertain\<T\>-based statistical program.

The `HistogramUncertain` class here is an `Uncertain` implementation that is basically a multinomial distribution including an "other" category. That is, it contains a set of *k* discrete "top" values, each associated with a probability. Those "top" probabilities can sum to less than 1.0; the remaining probability is allocated to the special "other" value.


Types
-----

The library uses an F# algebraic type, `'a partial`, which is either a concrete value or a special `Other` value:

    type 'a partial = Top of 'a | Other

N.B. `'a partial` can also be written in the C# style, as `partial<T>`.

A `HistogramUncertain<T>` is a subtype of `Uncertain<partial<T>>`. That is, it's a distribution whose domain consists of `T`s *and* the special `Other` placeholder. This means you can mostly use all the normal operations you're used to from the Uncertain\<T\> library, including sampling and combining with other distributions. You just have to be aware that you might get an `Other` value out of the object occasionally.

For convenience in F# land, the ungainly type `Uncertain<partial<T>>` can also be written `'a unc`.


Flattening Operations
---------------------

The library also provides "flattening" operations (see the `Histogram` modules in `HistogramUncertain.fs`) that transform *any* `Uncertain` value (under some restrictions) into a `HistogramUncertain`, effectively compacting its representation.
The three flattening operations are:

* `flatten : Uncertain<T> -> HistogramUncertain<T>`: Exhaustively enumerate the distribution (assuming that's possible) and represent it precisely as a histogram. The "other" probability is 0.0 in the output.
* `flattenSample : Uncertain<T> -> int -> int -> HistogramUncertain<T>`: Use sampling to find the top *k* most likely values from the distribution. The extra parameters are *k* and the number of samples to take.
* `reflatten : Uncertain<partial<T>> -> HistogramUncertain<T>`: Like `flatten`, but for distributions over partial domains. This is useful when you need to take a `HistogramUncertain`, use it to derive a more complicated distribution, and then *re-flatten* it back to a `HistogramUncertain`. The ordinary `flatten` would give you an `Uncertain<partial<partial<T>>>` in this case, which is clearly not what you want.


Lifting Combinators
-------------------

The `Lifting` module contains combinators that transform uncertainty-unaware functions into uncertainty-aware equivalents. The only one I've implemented so far is the simplest possible combinator, called `lift`. If you have a function of type `'a -> 'b`, this combinator will give you a new function with the type `'a unc -> 'b unc`. (It's like the monadic `map` operator.) The idea is that you can then provide the new, transformed with a `HistogramUncertain<T>` and get a new distribution out. Calling `reflatten` on that output gives you a new `HistogramUncertain<T>`.

(The `lift` operator is implemented using standard Uncertain\<T\> components. Its implementation might be instructive to read.)

There's also a companion module, called `CSLifting`, that exposes the same combinators in a way that C# programs can use conveniently. See `UncertainTests/HistogramTests.cs` for an example that uses the `lift` combinator in C#.
