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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace UncertainTests
{
    [TestClass]
    public class GaussianTests {
        [TestMethod]
        public void Gaussian_Sample() {
            // arrange
            Uncertain<double> X = new Gaussian(5.0, 2.0);
            var sampler = Sampler.Create(X);
            foreach (var s in sampler.Take(100))
            { 
                // assert
                Assert.IsTrue(s.Value >= -3 && s.Value <= 13);
            }
        }

        [TestMethod]
        public void Gaussian_Mean() {
            // arrange
            double Sum = 0.0;
            Uncertain<double> X = new Gaussian(5.0, 1.0);
            var sampler = Sampler.Create(X);
            // act
            foreach (var s in sampler.Take(100))
                Sum += s.Value;
            Sum /= 100;
            // assert
            // If everything is working, this has about a 0.003% chance of a false positive
            // (99.9997% confidence interval with n=100, sigma=1.0 is +/- 0.4)
            Assert.IsTrue(Sum >= 4.6 && Sum <= 5.4);
        }

        [TestMethod]
        public void Gaussian_BNN_Sample() {
            // arrange
            Uncertain<double> X = new Gaussian(1.0, 1.0);
            Uncertain<double> Y = new Gaussian(4.0, 1.0);
            Uncertain<double> Z = from x in X
                                  from y in Y
                                  select x + y;
            var sampler = Sampler.Create(Z);
            foreach (var s in sampler.Take(100))
            { 
                // assert
                Assert.IsTrue(s.Value >= -3 && s.Value <= 13.0);
            }
        }

        [TestMethod]
        public void Gaussian_BNN_Mean() {
            // arrange
            Uncertain<double> X = new Gaussian(1.0, 1.0);
            Uncertain<double> Y = new Gaussian(4.0, 2.0);
            Uncertain<double> Z = from x in X
                                  from y in Y
                                  select x + y;
            double Sum = 0.0;
            // act
            var sampler = Sampler.Create(Z);
            foreach (var s in sampler.Take(100))
                Sum += s.Value;
            Sum /= 100.0;
            // assert
            // Z is known to be Gaussian(5.0, sqrt(5))
            // If everything is working, this has about a 0.003% chance of a false positive
            // (99.9997% confidence interval with n=100, sigma=sqrt(5) is +/- 0.89)
            Assert.IsTrue(Sum >= 4.11 && Sum <= 5.89);
        }

        [TestMethod]
        public void Gaussian_Bernoulli_Mean() {
            // arrange
            Uncertain<double> X = new Gaussian(1.0, 1.0);
            Uncertain<double> Y = new Gaussian(3.0, 2.0);
            var  Z = X > Y;
            var sampler = Sampler.Create(Z);
            int k = 0;
            // act
            
            foreach (var s in sampler.Take(100))
                if (s.Value) k += 1;
            // assert
            // Y - X is Gaussian(1, sqrt(5)), and has a 32.74% chance of being < 0
            // (i.e. 32.74% chance that X > Y)
            // The normal approximation to the binomial distribution says that the
            // 99.9997% confidence interval with n=100, p=0.3274 is +/- 0.1883.
            // So the confidence interval is roughly [0.13, 0.52].
            Assert.IsTrue(k >= 13 && k < 52); // flaky at N = 100!
        }

        [TestMethod]
        public void Gaussian_Bernoulli_Conditional() {
            // arrange
            Uncertain<double> X = new Gaussian(1.0, 1.0);
            Uncertain<double> Y = new Gaussian(4.0, 2.0);
            // act
            if ((X > Y).Pr()) {
                Assert.Fail("X > Y evaluates true, incorrectly");
            }
            if ((Y < X).Pr()) {
                Assert.Fail("Y < X evaluates true, incorrectly");
            }
            if (!(Y > X).Pr()) {
                Assert.Fail("Y > X evaluates false, incorrectly");
            }
            if (!(X < Y).Pr()) {
                Assert.Fail("X < Y evaluates false, incorrectly");
            }
        }
    }
}
