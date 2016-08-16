using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;

namespace InferenceDebugger
{
    class Example2
    {
        const double BernoulliP = 0.05;

        public static Func<int, Uncertain<int>> F1 = (k1) =>
              from a in new Flip(BernoulliP).SampledInference(k1)
              select Convert.ToInt32(a);

        public static double getMean()
        {
            return BernoulliP;
        }
    }
}
