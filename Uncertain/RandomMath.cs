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
    /// <summary>
    /// Provides various random generators useful for Uncertain&lt;T&gt;
    /// </summary>
    public class RandomMath : Random {
        /// <summary>
        /// Each instantiation of RandomMath draws a sample from this Random generator to seed
        /// itself. While probably not cryptographically secure it avoids correlation between
        /// random samples in different random variables.
        /// </summary>
        private static Random SeedSource = new Random();
        
        /// <summary>
        /// Cache the second Gaussian sample from the Box-Muller transform
        /// </summary>
        private double? NextGaussianSample;
        
        /// <summary>
        /// Seed a new RandomMath with a sample from the SeedSource
        /// </summary>
        public RandomMath() : base(SeedSource.Next()) {
        }

        public bool NextBernoulli(double p) {
            if (p < 0 || p > 1) throw new Exception(String.Format("Expected a value in [0,1] but got {0}", p));
            return this.NextDouble() < p;
        }

        /// <summary>
        /// Return a new sample from the Uniform distribution on the interval [a,b).
        /// </summary>
        /// <param name="a">The (inclusive) minimum value</param>
        /// <param name="b">The (exclusive) maximum value</param>
        /// <returns>A random number greater than or equal to <paramref name="a"/> and less than
        /// <paramref name="b"/></returns>
        public double NextUniform(double a, double b) {
            double Range = b - a;
            return this.NextDouble() * Range + a;
        }

        /// <summary>
        /// Return a new sample from the standard Gaussian (Normal) distribution, with mean 0 and
        /// standard deviation 1.
        /// </summary>
        /// <remarks>
        /// Uses the Box-Muller transform, which generates two samples at a time, and so every
        /// second call actually returns a cached sample from the previous call.
        /// </remarks>
        /// <returns>A random number from the standard Gaussian distribution</returns>
        public double NextGaussian() {
            double sample;
            if (NextGaussianSample.HasValue) {
                sample = NextGaussianSample.Value;
                NextGaussianSample = null;
            } else {
                double U1 = this.NextDouble();
                double U2 = this.NextDouble();
                double R = Math.Sqrt(-2 * Math.Log(U1));
                double Theta = 2 * Math.PI * U2;
                NextGaussianSample = R * Math.Sin(Theta);
                sample = R * Math.Cos(Theta);
            }
            return sample;
        }

        /// <summary>
        /// Return a new sample from the Gaussian (Normal) distribution with mean
        /// <paramref name="Mean"/> and standard deviation <paramref name="StDev"/>.
        /// </summary>
        /// <param name="Mean">The mean of the Gaussian distribution</param>
        /// <param name="StDev">The standard deviation of the Gaussian distribution</param>
        /// <returns>A random number from the specified Gaussian distribution</returns>
        public double NextGaussian(double Mean, double StDev) {
            return this.NextGaussian() * StDev + Mean;
        }

        /// <summary>
        /// Return a new sample from the Rayleigh distribution with the given parameter.
        /// </summary>
        /// <remarks>
        /// The Rayleigh distribution is a continuous non-negative single-parameter distribution
        ///     Rayleigh(x; Rho) = (x / Rho^2) exp(-x^2 / (2*Rho^2))
        /// If X and Y are Gaussian(0, Sigma^2), then Sqrt(X,Y) is Rayleigh(Sigma).
        /// </remarks>
        /// <param name="Rho">The parameter of the distribution</param>
        /// <returns>A random number from the specified Rayleigh distribution</returns>
        public double NextRayleigh(double Rho) {
            double X = NextGaussian(0, Rho);
            double Y = NextGaussian(0, Rho);
            return Math.Sqrt(X * X + Y * Y);
        }

        /// <summary>
        /// Draw a sample from the discrete distribution in which each possible value in
        /// <paramref name="States"/> has an associated probability in
        /// <paramref name="Probabilities"/>.
        /// </summary>
        /// <typeparam name="T">The type of the distribution</typeparam>
        /// <param name="States">The possible values of the random variable</param>
        /// <param name="Probabilities">The probabilities of each possible value, which must sum to 1</param>
        /// <returns>A sample from the distribution</returns>
        public T NextMultinomial<T>(IEnumerable<T> States, IEnumerable<double> Probabilities) {
            Contract.Requires(States.Count() == Probabilities.Count());
            Contract.Requires(Probabilities.Sum() > 1.0 - 1e-8 && Probabilities.Sum() < 1.0 + 1e-8);
            var p = 0.0;
            var value = this.NextDouble();
            for (int i = 0; i < Probabilities.Count(); i++) {
                p += Probabilities.ElementAt(i);
                if (value < p) return States.ElementAt(i);
            }
            throw new Exception("Expected probabilties to sum to 1");
        }

        /// <summary>
        /// Draw a sample from the discrete distribution in which each key in <paramref name="Map"/>
        /// occurs with probability equal to its associated value.
        /// </summary>
        /// <typeparam name="T">The type of the distribution</typeparam>
        /// <param name="Map">A map of possible values to probabilities. The probabilities must
        /// sum to 1.</param>
        /// <returns>A sample from the distribution</returns>
        public T NextMultinomialDict<T>(IDictionary<T, double> Map) {
            Contract.Requires(Math.Abs(Map.Values.Sum() - 1.0) < 1e-6);
            var p = 0.0;
            var value = this.NextDouble();
            foreach (var t in Map) {
                p += t.Value;
                if (value < p) return t.Key;
            }
            throw new Exception("Expected probabilities to sum to 1");
        }
    }

    /// <summary>
    /// Samples from a discrete distribution using Vose's aliasing method, which offers O(1)
    /// sampling after an O(n) initialization phase.
    /// </summary>
    /// <typeparam name="T">The type of the values being sampled</typeparam>
    public class DiscreteSampler<T> {
        private int Count;
        private T[] ElemTable;

        private int[] Alias;
        private double[] Prob;
        
        private RandomMath R = new RandomMath();

        /// <summary>
        /// Create a DiscreteSampler that will sample from the given <paramref name="Elements"/>
        /// array according to the given <paramref name="Probabilities"/>. The probabilities need
        /// not sum to 1.
        /// </summary>
        /// <param name="Elements">The list of elements to sample from</param>
        /// <param name="Probabilities">The likelihood of each corresponding element in <paramref name="Elements"/></param>
        public DiscreteSampler(T[] Elements, double[] Probabilities) {
            Contract.Requires(Elements.Length == Probabilities.Length);
            Count = Elements.Length;
            ElemTable = Elements;

            this.Initialize(Probabilities);
        }

        /// <summary>
        /// Create the Prob and Alias tables used to do the sampling.
        /// </summary>
        /// <param name="Probabilities">Probabilities for each element</param>
        private void Initialize(double[] Probabilities) {
            Prob = new double[Count];
            Alias = new int[Count];
            Stack<int> Large = new Stack<int>();  // we need constant-time pop
            Stack<int> Small = new Stack<int>();

            // First, normalise the probabilities and multiply each by n
            double[] P = new double[Count];
            Probabilities.CopyTo(P, 0);
            double TotalProb = P.Sum();
            for (int i = 0; i < Count; i++) {
                P[i] = P[i] * Count / TotalProb;
                if (P[i] < 1) Small.Push(i);
                else Large.Push(i);
            }

            while (Small.Count > 0 && Large.Count > 0) {
                int l = Small.Pop();
                int g = Large.Pop();
                Prob[l] = P[l];
                Alias[l] = g;
                P[g] = (P[g] + P[l]) - 1;
                if (P[g] < 1) Small.Push(g);
                else Large.Push(g);
            }

            while (Large.Count > 0) {
                int g = Large.Pop();
                Prob[g] = 1;
            }

            while (Small.Count > 0) {
                int l = Small.Pop();
                Prob[l] = 1;
            }
        }

        /// <summary>
        /// Draw a sample from the discrete distribution.
        /// </summary>
        /// <returns>A new sample</returns>
        public T Sample() {
            int i = R.Next(Count);
            double p = R.NextDouble();
            int idx = p < Prob[i] ? i : Alias[i];
            return ElemTable[idx];
        }
    }
}
