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

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Research.Uncertain;
using System.Linq;

namespace UncertainTests {
    [TestClass]
    public class UniformTests {
        [TestMethod]
        public void Uniform_Sample() {
            // arrange
            double Start = 4.0, End = 6.0;
            Uncertain<double> X = new Uniform<double>(Start, End);
            var sampler = Sampler.Create(X);
            foreach(var p in sampler.Take(100))
            {
                double s = p.Value;
                // assert
                Assert.IsTrue(s >= Start && s <= End);
            }
        }

        [TestMethod]
        public void Uniform_BNN_Sample() {
            // arrange
            Uncertain<double> X = new Uniform<double>(1.0, 3.0);
            Uncertain<double> Y = new Uniform<double>(4.0, 5.0);
            Uncertain<double> Z = from x in X from y in Y select x + y;
            var sampler = Sampler.Create(Z);
            // act
            foreach (var p in sampler.Take(100))
            {
                // act
                double s = p.Value;
                // assert
                Assert.IsTrue(s >= 1.0 && s <= 8.0);
            }
        }

        [TestMethod]
        public void Uniform_Bernoulli_Sample() {
            // arrange
            Uncertain<double> X = new Uniform<double>(1.0, 3.0);
            Uncertain<double> Y = new Uniform<double>(4.0, 5.0);
            var Z = X > Y;
            var sampler = Sampler.Create(Z);
            foreach (var p in sampler.Take(100))
            {
                // act
                bool s = p.Value;
                // assert
                Assert.IsFalse(s);
            }
        }

        [TestMethod]
        public void Uniform_Bernoulli_Conditional() {
            // arrange
            Uncertain<double> X = new Uniform<double>(1.0, 3.0);
            Uncertain<double> Y = new Uniform<double>(4.0, 5.0);
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
