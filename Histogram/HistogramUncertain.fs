namespace Microsoft.Research.Uncertain.Histogram
open Microsoft.Research.Uncertain

type HistogramUncertain<'a> (topk: seq< 'a * float >) =
    inherit RandomPrimitive<'a>()

    // A list of object/probability pairs.
    let entries =
        List.ofSeq topk

    // The "non-top" probability.
    member this.otherProbability () =
        let allProbs = seq { for value, probability in entries -> probability } in
        let totalProb = Seq.fold (+) 0.0 allProbs in
        let topkProb = 0.0 in
        1.0 - topkProb
    

    // The Uncertain<T> interface.

    override this.GetSupport () =
        raise (System.Exception("infinite support"))
    
    override this.GetSample () =
        // TODO actually sample
        let value, probability = entries.Head in
        value

    override this.StructuralEquals other =
        // TODO
        true

    override this.GetStructuralHash () =
        // TODO
        0

    override this.Score value =
        // TODO
        0.0
