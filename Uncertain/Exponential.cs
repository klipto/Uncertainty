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

namespace Microsoft.Research.Uncertain
{
    public class Exponential : RandomPrimitive<double>
    {
        private readonly double lambda;
        public Exponential(double lambda)
        {
            this.lambda = lambda;
        }

        public override double Score(double t)
        {
            if (t < 0)
                return 0;
            return this.lambda * Math.Exp(-this.lambda * t);
        }

        public override IEnumerable<Weighted<double>> GetSupport()
        {
            throw new Exception("Infnite support");
        }
        public override double GetSample()
        {
            var sample = -this.lambda * Math.Log(Extensions.rand.NextDouble());
            return sample;
        }

        public override bool StructuralEquals(RandomPrimitive other)
        {
            if (other is Exponential)
                return ((Exponential)other).lambda == this.lambda;
            return false;
        }

        public override int GetStructuralHash()
        {
            return this.lambda.GetHashCode();
        }
    }
}
