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
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace UncertainTests
{
    [TestClass]
    public class BaseTests {

        double eps = 0.05;
        private static bool ApproxEqual(double a, double b, double eps = 0.05)
        {
            var diff = a - b;
            return diff > 0.0 - eps && diff < 0.0 + eps;
        }

        struct GeoLocation { }

        // Test the base Uncertain<T> class rather than subclasses
        [TestMethod]
        public void Base_Sample() {

            Uncertain<GeoLocation> roads = null;
            Uncertain<GeoLocation> gps = null;
            Func<GeoLocation, GeoLocation, double> Likelihood = (_) => 1.0;
            var p =
                from pos in gps
                from road in roads
                let prob = Likelihood(pos, road)
                select new Weighted<GeoLocation>(pos, prob);

            // arrange
            Uncertain < double > X = 5.0;
            // act
            var sampler = Sampler.Create(X);
            var S = sampler.First();
            // assert
            Assert.AreEqual(S.Value, 5.0);
        }

        // Test the implicit conversion from T to Uncertain<T>
        [TestMethod]
        public void Base_Implicit() {
            // arrange
            Uncertain<double> X = 5.0;
            Uncertain<double> Y = 6.0;
            // act
            Uncertain<double> Z = from x in X 
                                  from y in Y 
                                  select x + y;
            var sampler = Sampler.Create(Z);
            var s = sampler.First();
            // assert
            Assert.AreEqual(s.Value, 11.0);
        }

        // Test the equality operators
        [TestMethod]
        public void Base_Equality() {
            Uncertain<double> X = 5.0;

            if ((X == null).Pr())
                Assert.Fail();

            if ((null == X).Pr())
                Assert.Fail();
        }

        // Test independence of operations
        [TestMethod]
        public void Base_TestAdd() {
            //var r = new RandomMath();
            var age = new Multinomial<int>(new[] { 0, 1, 2, 3 }, new[] { 0.1, 0.6, 0.1, 0.2 });
            // sum of binomial's should = 1.0 because of the law of total probabilty. 
            var tmp = from a in age
                      from b in age
                      from c in age
                      from d in age
                      select a == 0 | b == 1 | c == 2 | d == 3;
            var sum = tmp.Inference().Support().Select(k => k.Probability).Sum();
            Assert.IsTrue(sum > 1.0 - eps && sum < 1.0 + eps);
        }

        [TestMethod]
        public void Base_SampleCaching() {
            var X = new Bernoulli(0.5);
            var NotX = from a in X select !a;
            var XorNotX = from a in X from b in NotX select a | b;
            
            // Without sample caching we get Prob = 0.75
            var Prob = XorNotX.ExpectedValueWithConfidence(10000);
            Assert.IsTrue(ApproxEqual(Prob.Mean, 1.0));
        }



        [TestMethod]
        public void Base_TestMarginal() {
            var X = new Multinomial<int>(Enumerable.Range(0, 4), new[] { 0.1, 0.6, 0.1, 0.2 });
            var Y = new Multinomial<int>(Enumerable.Range(0, 4), new[] { 0.6, 0.1, 0.2, 0.1 });
            //P(X,Y)
            var joint = from x in X
                        from y in Y
                        select x == 0 ? Tuple.Create(x, 1) : Tuple.Create(x, y);
            // p(Y)
            Func<int, Uncertain<bool>> marginal = y =>
                from pair in joint
                select pair.Item2 == y;

            // \sum_{y} p(y) == 1
            var tmp = from a in marginal(0)
                      from b in marginal(1)
                      from c in marginal(2)
                      from d in marginal(3)
                      select a | b | c | d;
            var sum = tmp.Inference().Support().Select(k => k.Probability).Sum();
            //(marginal(0) | marginal(1) | marginal(2) | marginal(3)).Prob();
            Assert.IsTrue(sum > 1.0 - eps && sum < 1.0 + eps);

            // Here is the entire joint distribution p(x,y):
            //                     X
            //         -------------------------
            //         |  0  |  1  |  2  |  3  |
            //   ------|------------------------
            //   |  0  |  0  | 0.36| 0.06| 0.12|  0.54
            // Y |  1  | 0.1 | 0.06| 0.01| 0.02|  0.19
            //   |  2  |  0  | 0.12| 0.02| 0.04|  0.18
            //   |  3  |  0  | 0.06| 0.01| 0.02|  0.09
            //   -------------------------------
            //           0.1   0.6   0.1   0.2    1.0

            var m0 = marginal(0).ExpectedValueWithConfidence().Mean;  // p(y=0) = 0.54
            Assert.IsTrue(m0 > 0.54 - eps && m0 < 0.54 + eps);

            var m1 = marginal(1).ExpectedValueWithConfidence().Mean;  // p(y=1) = 0.19
            Assert.IsTrue(m1 > 0.19 - eps && m1 < 0.19 + eps);

            var m2 = marginal(2).ExpectedValueWithConfidence().Mean;  // p(y=2) = 0.18
            Assert.IsTrue(m2 > 0.18 - eps && m2 < 0.18 + eps);

            var m3 = marginal(3).ExpectedValueWithConfidence().Mean;  // p(y=3) = 0.09
            Assert.IsTrue(m3 > 0.09 - eps && m3 < 0.09 + eps);
        }

        [TestMethod]
        public void Base_ExpectedValue() {
            var Mu = 5.0;
            var X = new Gaussian(Mu, 2.0);
            var E = X.ExpectedValueWithConfidence().Mean;
            var Err = Math.Abs(E - Mu) / Mu;
            Assert.IsTrue(Err < eps);
        }

        [TestMethod]
        public void Base_ExpectedValueGeneric() {
            var P = 0.5;
            var X = new Bernoulli(P);
            double E = X.ExpectedValueWithConfidence().Mean;
            var Err = Math.Abs(E - P);
            Assert.IsTrue(Err < eps);
        }

        [TestMethod]
        public void Base_TestExpectedValue()
        {

            var X = new Gaussian(10, 1);
            var t = X.ExpectedValueWithConfidence(1000);
            var m = t.Mean;
            var ci = t.CI;

            Assert.IsTrue(m - ci < 10 && 10 < m + ci);
        }
    }
}
