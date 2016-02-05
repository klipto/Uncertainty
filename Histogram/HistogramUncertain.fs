namespace Microsoft.Research.Uncertain.Histogram
open Microsoft.Research.Uncertain

// Functional utilities for implementing HistogramUncertain.
module Histogram =
    let rec getScore entries value =
        match entries with
        | (v, p)::es -> (if v = value then p else getScore es value)
        | _ -> 0.0

    let rec sampleIndex index entries =
        match entries with
        | (v, p)::es -> (if index <= p
                         then Some v
                         else sampleIndex (index - p) entries)
        | _ -> None


// A convenient F# type alias for "partial distributions."
type 'a unc = Uncertain<'a option>


// Our combinator library.
module public Lifting =
    // Propagate uncertainty through an 'a -> 'b.
    let lift (f: 'a -> 'b) (ua: 'a unc) : 'b unc =
        ua.Select(fun a -> Option.map f a)


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