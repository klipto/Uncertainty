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

namespace UncertainTests
{
    [TestClass]
    public class ConstantTests
    {
        [TestMethod]
        public void Constant_Sample()
        {
            // arrange
            Uncertain<double> X = 5.0;
            var sampler = Sampler.Create(X);
            // act
            var S = sampler.First();
            // assert
            Assert.AreEqual(S.Value, 5.0);
        }

        [TestMethod]
        public void Constant_BNN_Sample()
        {
            // arrange
            Uncertain<double> X = new Constant<double>(5.0);
            Uncertain<double> Y = new Constant<double>(6.0);
            Uncertain<double> Z = from x in X
                                  from y in Y
                                  select x + y;
            var sampler = Sampler.Create(Z);
            // act
            var S = sampler.First();
            // assert
            Assert.AreEqual(S.Value, 11.0);
        }

        [TestMethod]
        public void Constant_Bernoulli_Sample()
        {
            // arrange
            Uncertain<double> X = new Constant<double>(5.0);
            Uncertain<double> Y = new Constant<double>(6.0);
            var Z = X > Y;
            var sampler = Sampler.Create(Z);
            // act
            var S = sampler.First();
            // assert
            Assert.AreEqual(S.Value, false);
        }

        [TestMethod]
        public void Constant_Bernoulli_Conditional()
        {
            // arrange
            Uncertain<double> X = new Constant<double>(5.0);
            Uncertain<double> Y = new Constant<double>(6.0);
            // act
            if ((X > Y).Pr())
            {
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

        [TestMethod]
        public void Constant_Bernoulli_Equal() {
            // arrange
            Uncertain<double> X = new Constant<double>(5.0);
            Uncertain<double> Y = new Constant<double>(5.0);
            // act
            if ((X < Y).Pr()) {
                Assert.Fail("X < Y evaluates true, incorrectly");
            }
            if ((X > Y).Pr()) {
                Assert.Fail("X > Y evaluates true, incorrectly");
            }
        }
    }
}
