/* ------------------------------------------ START OF LICENSE -----------------------------------------
* UncertainT
*
* Copyright(c) Microsoft Corporation
*
* All rights reserved.
*
* MIT License
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the ""Software""), to 
* deal in the Software without restriction, including without limitation the 
* rights to use, copy, modify, merge, publish, distribute, sublicense, and/or 
* sell copies of the Software, and to permit persons to whom the Software is 
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in 
* all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
* SOFTWARE.
* ----------------------------------------------- END OF LICENSE ------------------------------------------
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Research.Uncertain
{
    public class FiniteEnumeration<T> : RandomPrimitive<T>
    {
        private readonly DiscreteSampler<T> table;
        private readonly IDictionary<T, double> sampleMap;

        public FiniteEnumeration(IList<T> space) :
            this(space.Zip(Enumerable.Repeat(1.0 / space.Count(), space.Count()), (a, b) => new Weighted<T>() { Value = a, Probability = b }).ToList()) { }

        public FiniteEnumeration(IList<Weighted<T>> space)
        {
            this.sampleMap = new Dictionary<T, double>();

            var samples = new T[space.Count];
            var probs = new double[space.Count];
            for(int i = 0; i < space.Count; i++)
            {
                samples[i] = space[i].Value;
                probs[i] = space[i].Probability;
                this.sampleMap[samples[i]] = probs[i];
            }
            this.table = new DiscreteSampler<T>(samples, probs);
        }

        public override double Score(T t)
        {
            return this.sampleMap[t];
        }

        protected override T GetSample()
        {
            return this.table.Sample();
        }

        protected override int GetStructuralHash()
        {
            int hash = 0;
            foreach (var item in this.sampleMap)
            {
                hash ^= item.Value.GetHashCode() * item.Key.GetHashCode();
            }
            return hash;
        }

        protected override IEnumerable<Weighted<T>> GetSupport()
        {
            foreach(var item in sampleMap)
            {
                yield return new Weighted<T> { Value = item.Key, Probability = item.Value };
            }
        }

        protected override bool StructuralEquals(RandomPrimitive other)
        { 
            if (other is FiniteEnumeration<T>)
            {
                var tmp = other as FiniteEnumeration<T>;
                if (tmp.sampleMap.Count() != this.sampleMap.Count())
                {
                    return false;
                }

                foreach (var pairs in this.sampleMap.Zip(tmp.sampleMap, Tuple.Create))
                {
                    if (pairs.Item1.Key.Equals(pairs.Item2.Key) == false ||
                        pairs.Item1.Value != pairs.Item2.Value) // toddm: comparing on double! :(
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
