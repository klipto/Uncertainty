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
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.Research.Uncertain
{
    public class Multinomial<T> : RandomPrimitive<T>
    {
        private readonly IEnumerable<Weighted<T>> options;

        public Multinomial(params T[] states) : this(states as IEnumerable<T>) { }

        public Multinomial(IEnumerable<T> states) :
            this(states, Enumerable.Repeat(1.0 / states.Count(), states.Count())) 
        {
            Contract.Requires(states.Count() > 0);
        }

        public Multinomial(IEnumerable<T> states, IEnumerable<double> probs) :
            this(states.Zip(probs, (a, b) => new Weighted<T>() { Value = a, Probability = b }))
        {
            Contract.Requires(states.Count() == probs.Count());
            Contract.Requires(probs.Sum() < 1 + 0.01 && probs.Sum() > 1 - 0.01);
        }

        internal Multinomial(IEnumerable<Weighted<T>> options)
        {
            Contract.Requires(options.Count() > 0);
            this.options = options;
        }

        protected override IEnumerable<Weighted<T>> GetSupport()
        {
            return this.options;
        }

        public override double Score(T t)
        {
            foreach (var item in this.options)
                if (EqualityComparer<T>.Default.Equals(item.Value, t))
                    return item.Probability;
            throw new Exception("T not in options");
        }

        protected override T GetSample()
        {
            var p = 0.0;
            var value = Extensions.NextRandom();
            foreach (var item in this.options)
            {
                p += item.Probability;
                if (value < p)
                {
                    return item.Value;
                }
            }

            throw new Exception("Expected probabilties to sum to 1");
        }

        protected override bool StructuralEquals(RandomPrimitive other)
        {
            if (other is Multinomial<T>)
            {
                var tmp = other as Multinomial<T>;
                if (tmp.options.Count() != this.options.Count())
                    return false;
                foreach (var pairs in this.options.Zip(tmp.options, Tuple.Create))
                    if (pairs.Item1.Value.Equals(pairs.Item2.Value) == false || 
                        pairs.Item1.Probability != pairs.Item2.Probability) // toddm: comparing on double! :(
                        return false;
            }

            return true;
        }

        protected override int GetStructuralHash()
        {
            int hash = 0;
            foreach (var item in this.options)
                hash ^= item.GetHashCode();
            return hash;
        }
    }
}
