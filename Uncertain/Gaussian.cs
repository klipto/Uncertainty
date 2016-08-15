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
    public class Gaussian : RandomPrimitive<double>
    {
        protected readonly double mu, stdev;
        private double? nextGaussianSample;

        public Gaussian(double mu = 0.0, double stdev = 1.0) 
        {
            this.mu = mu;
            this.stdev = stdev;
        }

        public static double Likelihood(double t, double mu, double stdev)
        {
            var a = 1.0 / (stdev * Math.Sqrt(2 * Math.PI));
            var b = Math.Exp(-Math.Pow(t - mu, 2) / (2 * stdev * stdev));
            return a * b;
        }

        protected double NextGaussian()
        {
            double sample;
            if (nextGaussianSample.HasValue)
            {
                sample = nextGaussianSample.Value;
                nextGaussianSample = null;
            }
            else
            {
                double U1 = Extensions.NextRandom();
                double U2 = Extensions.NextRandom();
                double R = Math.Sqrt(-2 * Math.Log(U1));
                double Theta = 2 * Math.PI * U2;
                nextGaussianSample = R * Math.Sin(Theta);
                sample = R * Math.Cos(Theta);
            }
            return sample;
        }

        protected override IEnumerable<Weighted<double>> GetSupport()
        {
            throw new Exception("Infinite Support!");
        }

        public override double Score(double t)
        {
            var a = 1.0 / (this.stdev * Math.Sqrt(2 * Math.PI));
            var b = Math.Exp(-Math.Pow(t - this.mu, 2) / (2 * this.stdev * this.stdev));
            return a * b;
        }

        protected override double GetSample()
        {
            return this.NextGaussian() * this.stdev + this.mu;
        }

        protected override bool StructuralEquals(RandomPrimitive other)
        {
            if (other is Gaussian)
            {
                var tmp = other as Gaussian;
                return tmp.stdev == this.stdev && tmp.mu == this.mu;
            }

            return false;
        }

        protected override int GetStructuralHash()
        {
            return this.mu.GetHashCode() ^ this.stdev.GetHashCode();
        }
    }
}
