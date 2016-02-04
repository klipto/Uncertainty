namespace Microsoft.Research.Uncertain.Histogram

open Microsoft.Research.Uncertain

type HistogramUncertain<'a>() =
    inherit RandomPrimitive<'a>()
    let entries = List.empty<Weighted<'a>>

    override this.GetSupport () =
        // TODO entries
        seq {
            yield entries.Head
        }
    
    override this.GetSample () =
        // TODO actually sample
        entries.Head.Value

    override this.StructuralEquals other =
        // TODO
        true

    override this.GetStructuralHash () =
        // TODO
        0

    override this.Score value =
        // TODO
        0.0