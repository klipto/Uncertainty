using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.Uncertain
{
    /**
     * A germetric distribution within a range (a,b]. This is used in the debugger to ensure that smaller sample sizes are preferred, by maximizing the likelihood.
     */
    public class TruncatedGeometric : Geometric
    {
        private readonly int a, b;
        public TruncatedGeometric(double p, int a, int b)
            : base(p)
        {
            this.a = a;
            this.b = b;
        }

        public double CDF(int n)
        {
            return (1 - Math.Pow(1 - base.p, n + 1));
        }


        public override int GetSample()
        {
            Random rand = new Random();
            if (b > a)
            {
                var sample = (Math.Log((rand.NextDouble() * (CDF(a) - CDF(b)) / Math.Pow((1 - p), a)) + 1) / Math.Log(1 - p)) + a - 1;
                return (int)(sample);
            }
            else if (a > b)
            {
                var sample = (Math.Log((rand.NextDouble() * (CDF(b) - CDF(a)) / Math.Pow((1 - p), b)) + 1) / Math.Log(1 - p)) + b - 1;
                return (int)(sample);
            }
            else return 0;
        }

        public override double Score(int n)
        {
            if (b > a)
            {
                if (n > a && n <= b)
                {
                    return base.Score(n) / (CDF(b) - CDF(a));
                }
                else return 0.0;
            }
            else if (a > b)
            {
                if (n > b && n <= a)
                {
                    return base.Score(n) / (CDF(a) - CDF(b));
                }
                else return 0.0;
            }
            else return 0.0;
        }
    }
}
