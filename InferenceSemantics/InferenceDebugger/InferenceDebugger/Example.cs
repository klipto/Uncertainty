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
        public static Func<int, Uncertain<double>> F = (k1) =>
                 from a in new Gaussian(0, 1).SampledInference(k1)
                 select a;

        public static double getMean()
        {
            return 0;
        }
    }
}