#r "../Uncertain/bin/Debug/Uncertain.dll"
#r "bin/Debug/Histogram.dll"
open Microsoft.Research.Uncertain.Histogram

let foo = new HistogramUncertain<float> ([ (73.0, 0.8); (42.1, 0.15) ])

for weighted in foo.Support() do
    match weighted.Value with
    | Some v -> printfn "%A: %f" v weighted.Probability
    | None -> printfn "other: %f" weighted.Probability