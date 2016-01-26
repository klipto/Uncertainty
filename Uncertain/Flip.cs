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
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Microsoft.Research.Uncertain
{
    public class Flip : RandomPrimitive<bool>
    {
        private readonly double p;

        public Flip(double p)
        {
            Contract.Requires(p >= 0 && p <= 1);
            this.p = p;
        }

        protected override IEnumerable<Weighted<bool>> GetSupport()
        {
            yield return new Weighted<bool>() { Value = true, Probability = p };
            yield return new Weighted<bool>() { Value = false, Probability = 1 - p };
        }

        public override double Score(bool t)
        {
            return t ? this.p : 1 - this.p;
        }

        protected override bool GetSample()
        {
            return Extensions.rand.NextDouble() < this.p;
        }

        protected override bool StructuralEquals(RandomPrimitive other)
        {
            if (other is Flip)
                return ((Flip)other).p == this.p;
            return false;
        }

        protected override int GetStructuralHash()
        {
            return this.p.GetHashCode();
        }
    }
}
