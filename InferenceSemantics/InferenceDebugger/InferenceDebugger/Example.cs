using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading.Tasks;
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
namespace InferenceDebugger
{
    class Example
    {
        private static double threshold = 2;
        internal static Func<Uncertain<double>, bool> CorrectnessCondition = (program) =>
        {
            var vals = program.Inference().Support().ToList();
            int count = 0;
            foreach (var val in vals)
            {
                if (val.Value <= threshold)
                {
                    count++;
                }
            }
            if (count >= 0.90 * vals.Count())
            {
                return true;
            }
            else return false;
        };

        internal static Func<int, int, Uncertain<double>> P = (k1, k2) =>
                     from a in new Gaussian(0, 1).SampledInference(k1, null)
                     from b in new Gaussian(0, 1).SampledInference(k2, null)
                     let delta = Math.Abs(a - b)
                     select delta;        
    }
}