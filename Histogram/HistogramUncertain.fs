namespace Microsoft.Research.Uncertain.Histogram
open Microsoft.Research.Uncertain

// A convenient F# type alias for "partial distributions."
type 'a unc = Uncertain<'a option>

// Our combinator library.
module public Lifting =
    // Propagate uncertainty through an 'a -> 'b.
    let lift (f: 'a -> 'b) (ua: 'a unc) : 'b unc =
        ua.Select(fun a -> Option.map f a)

// Experiments with combinators that work well in C# land.
module public CSLifting =
    let lift (f: System.Func<'a, 'b>): System.Func<'a unc, 'b unc> =
        let g = Lifting.lift (fun (a: 'a) -> f.Invoke a)
        System.Func<'a unc, 'b unc> g

// A (partial) Uncertain<T> implementaiton wrapping a "top-K-plus-other"
// histogram representation. It is based on the Multinomial primitive from
// the Uncertain library.
type HistogramUncertain<'a> when 'a : equality (topk: seq< 'a * float >) =
    inherit Multinomial< 'a option >(
        // Values.
        seq {
            for value, probability in topk -> Some value;
            yield None
        },

        // Probabilities.
        let probs = seq {
            for value, probability in topk -> probability
        }
        seq {
            yield! probs;
            yield 1.0 - (Seq.fold (+) 0.0 probs)
        }
    )

// Utilities for constructing histograms.
module public Histogram =
    // "Flatten" an arbitrary Uncertain<T> using exhaustive enumeration.
    // There is no "other" part in the resulting histogram.
    let flatten (ua: Uncertain<'a>) : HistogramUncertain<'a> =
        HistogramUncertain(seq {
            for weighted in ua.Support() ->
            weighted.Value, weighted.Probability
        })

    // A similar flattening for arbitrary Uncertain<T>s where T is an
    // option. This is useful when we construct more complex distritbuions
    // out of histograms and want to "re-flatten" them back to histograms
    // for a compact representation. Also uses exhaustive enumeration.
    let reflatten (ua: 'a unc) : HistogramUncertain<'a> =
        HistogramUncertain(seq {
            for weighted in ua.Support() do
            match weighted.Value with
            | Some v -> yield v, weighted.Probability
            | None -> ()
        })
    
    // Like `flatten`, but uses sampling instead of exhaustive enumeration.
    open Microsoft.Research.Uncertain.Inference
    let flattenSample (ua: Uncertain<'a>) samples: HistogramUncertain<'a> =
        let sampled = ua.SampledInference(samples)
        flatten(sampled)