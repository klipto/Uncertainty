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


// Our combinator library.
module Lifting =
    type 'a unc = Uncertain<'a option>

    let lift (f: 'a -> 'b) (ua: 'a unc) : 'b unc =
        query {
            for a in ua do
            let b = f a in
            select b
        }


// An Uncertain<T> implementaiton wrapping a "top-K-plus-other" representation.
type HistogramUncertain<'a> when 'a : equality (topk: seq< 'a * float >) =
    inherit RandomPrimitive< 'a option >()

    // A list of object/probability pairs.
    let entries =
        List.ofSeq topk

    // The "non-top" probability.
    member this.otherProbability () =
        let allProbs = seq { for value, probability in entries -> probability } in
        let totalProb = Seq.fold (+) 0.0 allProbs in
        let topkProb = 0.0 in
        1.0 - topkProb
    
    member this.getEntries () = entries
    

    // The Uncertain<T> interface.

    override this.GetSupport () =
        seq {
            for value, probability in entries
            -> Weighted(Some value, probability);
            yield Weighted(None, this.otherProbability())
        }
    
    override this.GetSample () =
        let rnd = System.Random()
        Histogram.sampleIndex (rnd.NextDouble()) entries

    override this.StructuralEquals other =
        match other with
        | :? HistogramUncertain<'a> as hu ->
            entries = hu.getEntries ()  // TODO Not really structural.
        | _ -> false

    override this.GetStructuralHash () =
        hash entries

    override this.Score value =
        match value with
        | Some v -> Histogram.getScore entries v
        | None -> 0.0
