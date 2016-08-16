using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Research.Uncertain
{
    public class Geometric: RandomPrimitive<int>
    {
        private readonly double p;
        public Geometric (double p) 
        {
            this.p = p;
        }

        public override double Score(int n)
        {
            if (n < 0)
                return 0;
            else
                return this.p * Math.Pow((1-this.p), n);
        }
        public override bool StructuralEquals(RandomPrimitive other)
        {
            if (other is Geometric)
            {
                return (other as Geometric).p == this.p;
            }

            return false;
        }
        public override int GetStructuralHash()
        {
            return this.p.GetHashCode();
        }
        public override IEnumerable<Weighted<int>> GetSupport()
        {
            throw new Exception("Infinite support");
        }
        public override int GetSample()
        {
            var sample = Math.Log(Extensions.rand.NextDouble()/this.p) / Math.Log(1-this.p);
            return (int)sample;
        }
    }
}
