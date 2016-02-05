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
        raise (System.Exception("unimplemented")) // TODO

    override this.StructuralEquals other =
        false  // TODO

    override this.GetStructuralHash () =
        0  // TODO
        // hash entries

    override this.Score value =
        // TODO
        0.0
