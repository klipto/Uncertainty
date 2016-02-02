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
    public class Uniform<T> : RandomPrimitive<T>
    {
        private readonly dynamic min, max;

        public Uniform(T min, T max)
        {
            this.min = min;
            this.max = max;
            if (this.min >= this.max)
                throw new Exception("Min >= Max");
        }


        public override double Score(T inp)
        {
            dynamic t = inp;
            if (t >= this.min && t <= this.max)
                return 1.0 / (this.max - this.min);
            return 0.0;
        }

        protected override IEnumerable<Weighted<T>> GetSupport()
        {
            foreach (var i in Enumerable.Range(min, max - min))
                yield return new Weighted<T>(i);
            //throw new Exception("Infnite support");
        }

        protected override T GetSample()
        {
            var Range = this.max - this.min;
            dynamic sample = Extensions.NextRandom();
            return (sample * Range) + this.min;
        }
        protected override bool StructuralEquals(RandomPrimitive other)
        {
            if (other is Uniform<T>)
            {
                var tmp = other as Uniform<T>;
                return tmp.min == this.min && tmp.max == this.max;
            }
            return false;
        }

        protected override int GetStructuralHash()
        {
            return this.min.GetHashCode() ^ this.max.GetHashCode();
        }
    }
}
