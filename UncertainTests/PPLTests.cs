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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using System.Collections.Generic;

namespace UncertainTests
{

    [TestClass]
    public class PPLTests
    {

        private static bool ApproxEqual(double a, double b, double eps = 0.05)
        {
            //const double eps = 0.05;
            var diff = a - b;
            return diff > 0.0 - eps && diff < 0.0 + eps;
        }


        [TestMethod]
        public void MontyHall()
        {
            var doors = Enumerable.Range(0, 3);
            var cardoor = new Multinomial<int>(doors);

            var program = from carDoorNum in new Multinomial<int>(doors)
                          from chosenDoor in new Multinomial<int>(doors)
                          let match = carDoorNum == chosenDoor
                          let possibleOpenDoors2 = doors.Where(i => i != carDoorNum).ToList()
                          let possibleOpenDoors1 = doors.Where(i => i != carDoorNum && i != chosenDoor).ToList()
                          let nextdoor = match ? new Multinomial<int>(possibleOpenDoors1) : new Multinomial<int>(possibleOpenDoors2)
                          from otherDoorContainsCar in nextdoor
                          let chosenDoorContainsCar = chosenDoor == carDoorNum
                          select chosenDoorContainsCar;
            // note chosenDoorContainsCar is never used but implicitly impacts the result
            // because we need to compute over the nextdoor probabilistic choice.

            var ans = program.Inference();
            var result = new[] { 0.66, 0.33 };

            foreach (var item in ans.Support().OrderBy(k => k.Value).Zip(result, Tuple.Create))
                Assert.IsTrue(ApproxEqual(item.Item1.Probability, item.Item2));

            var sampled = program.SampledInference(10000);

            foreach (var item in sampled.Support().OrderBy(k => k.Value).Zip(result, Tuple.Create))
                Assert.IsTrue(ApproxEqual(item.Item1.Probability, item.Item2));
        }

        [TestMethod]
        public void Example5()
        {
            var program = from i in new Flip(0.3)
                          from d in new Flip(0.4)
                          let gb = !i && !d ? new Flip(0.7) :
                                    !i && d ? new Flip(0.95) :
                                      i && !d ? new Flip(0.1) :
                                        new Flip(0.5)
                          let sb = !i ? new Flip(0.05) : new Flip(0.8)
                          from g in gb
                          let lb = !g ? new Flip(0.1) : new Flip(0.6)
                          from s in sb
                          from l in lb
                          select Tuple.Create(i, d, g, s, l);

            var ans = program.Inference().Support().OrderByDescending(k => k.Value).ToList();
            foreach (var item in ans)
                Console.WriteLine(item);
            Console.WriteLine();

            var sampled = program.SampledInference(10000).Support().OrderByDescending(k => k.Value).ToList();
            foreach (var item in sampled)
                Console.WriteLine(item);
            Console.WriteLine();
        }

        [TestMethod]
        public void Example6()
        {
            var program = from i in new Flip(0.3)
                          from d in new Flip(0.4)
                          let gb = !i && !d ? new Flip(0.7) :
                                    !i && d ? new Flip(0.95) :
                                      i && !d ? new Flip(0.1) :
                                        new Flip(0.5)
                          let sb = !i ? new Flip(0.05) : new Flip(0.8)
                          from g in gb
                          where g
                          let lb = !g ? new Flip(0.1) : new Flip(0.6)
                          from s in sb
                          from l in lb
                          select Tuple.Create(i, d, g, s, l);

            var ans = program.Inference().Support().OrderByDescending(k => k.Value).ToList();
            foreach (var item in ans)
                Console.WriteLine(item);
            Console.WriteLine();

            var sampled = program.SampledInference(100000).Support().OrderByDescending(k => k.Value).ToList();
            foreach (var item in sampled)
                Console.WriteLine(item);
            Console.WriteLine();
        }

        
        static Uncertain<int> Example7Helper(int inx)
        {
            Func<int, bool, int> func = (x, coin) =>
            {
                if (x == 0)
                    if (coin) return 1; else return 2;
                else if (x == 1)
                    if (coin) return 3; else return 4;
                else if (x == 2)
                    if (coin) return 5; else return 6;
                else if (x == 3)
                    if (coin) return 1; else return 11;
                else if (x == 4)
                    if (coin) return 12; else return 13;
                else if (x == 5)
                    if (coin) return 14; else return 15;
                else if (x == 6)
                    if (coin) return 16; else return 2;
                throw new Exception("Should not be here");
            };

            //if (inx == 11)
            //    return inx;

            var program = from coin in new Flip(0.5)
                          let newx = func(inx, coin)
                          let next = newx < 11 ? Example7Helper(newx) : newx
                          from item in next
                          select item;
            return program;
        }
        [TestMethod]
        public void Example7()
        {
            // note cannot use exact inference: each value 11-16 should be 1/6
            foreach (var item in Example7Helper(0).SampledInference(10000).Support().OrderBy(k => k.Value))
                Assert.IsTrue(ApproxEqual(item.Probability, 1.0 / 6.0));
        }

        [TestMethod]
        public void Example9()
        {
            var program = from skilla in new Gaussian(100, 10)
                          from skillb in new Gaussian(100, 10)
                          from skillc in new Gaussian(100, 10)

                          from perfA1 in new Gaussian(skilla, 15)
                          from perfB1 in new Gaussian(skillb, 15)
                          where perfA1 > perfB1

                          from perfB2 in new Gaussian(skillb, 15)
                          from perfC2 in new Gaussian(skillc, 15)
                          where perfB2 > perfC2

                          from perfA3 in new Gaussian(skilla, 15)
                          from perfC3 in new Gaussian(skillc, 15)
                          where perfA3 > perfC3

                          select Tuple.Create(skilla, skillb, skillc);

            //var infer = program.SampledInference(10000);
            var meana = (from a in program select a.Item1).ExpectedValueWithConfidence(10000);
            var meanb = (from a in program select a.Item2).ExpectedValueWithConfidence(10000);
            var meanc = (from a in program select a.Item3).ExpectedValueWithConfidence(10000);

            // note cannot use exact inference; should be N(105, 0.11), N(100,0.11), N(94.3,0.11)

            // note mean can fluctuate a lot!
            Assert.IsTrue(ApproxEqual(105, meana.Mean, 2));
            Assert.IsTrue(ApproxEqual(100, meanb.Mean, 2));
            Assert.IsTrue(ApproxEqual(94, meanc.Mean, 3));

            //int re = 10;
        }

        static double EstimateLogEGFR(double logScr, double age, bool isFemale, bool isAA)
        {
            double k, alpha, f = 4.94;

            if (isFemale)
            {
                k = -0.357;
                alpha = -0.328;
            }
            else
            {
                k = -0.105;
                alpha = -0.411;
            }
            if (logScr < k)
                f = alpha * (logScr - k);
            else
                f = -1.209 * (logScr - k);

            f = f - 0.007 * age;

            if (isFemale) f = f + 0.017;
            if (isAA) f = f + 0.148;

            return f;
        }
        public void TestMadan()
        {
            IEnumerable<int> A = null;
            IEnumerable<int> B = null;
            IEnumerable<int> C = null;

            var program = from a in A
                          from b in B
                          from c in C
                          let d = a + b + c
                          group d by d into g
                          select new { D = g.Key, Count = g.Count() };
        }

        [TestMethod]
        public void Example11()
        {
            double logScr = 0.5;
            double age = 20;
            bool isFemale = true;
            bool isAA = false;

            double f1 = EstimateLogEGFR(logScr, age, isFemale, isAA);

            var nlogScr = from a in new Uniform<double>(-0.1, 0.1) select logScr + a;
            var nAge = from a in new Uniform<double>(-1, 1) select age + a;
            var nIsFemale = from a in new Flip(0.01)
                            let b = isFemale
                            let c = a ? !b : b
                            select c;
            var nIsAA = from a in new Flip(0.01)
                        let b = isAA
                        let c = a ? !isAA : b
                        select c;

            var f2 = from a in nlogScr
                     from b in nAge
                     from c in nIsFemale
                     from d in nIsAA
                     select EstimateLogEGFR(a, b, c, d);

            //bool bigChange = false;
            var bigchange = from f2item in f2
                            let tmp1 = f1 - f2item >= 0.1
                            let tmp2 = f2item - f1 >= 0.1
                            select tmp1 || tmp2;
            // Pr False ~ 0.82
            Assert.IsTrue((bigchange == false).Pr(0.7));
        }

        [TestMethod]
        public void Example15()
        {
            var isburglary = from earthquate in new Flip(0.001)
                             from burglary in new Flip(0.01)
                             let alarm = earthquate || burglary
                             from phoneworking in earthquate ? new Flip(0.6) : new Flip(0.99)
                             from marywakes in alarm && earthquate ? new Flip(0.8) : alarm ? new Flip(0.6) : new Flip(0.2)
                             let called = marywakes && phoneworking
                             where called
                             select burglary;

            Assert.IsFalse((isburglary == true).Pr());
            Assert.IsTrue((isburglary == false).Pr(0.9));
        }

        [TestMethod]
        public void TestMulti()
        {
            var program = from a in new FiniteEnumeration<int>(Enumerable.Range(0, 10).ToList())
                          from b in new FiniteEnumeration<int>(Enumerable.Range(0, 10).ToList())
                          select a + b;

            var allpaths = program.Support().ToList();
            var posterior = program.Inference().Support().OrderByDescending(p => p.Probability).ToList();


            var sampledPosterior = program.SampledInference(10000).Support().OrderByDescending(p => p.Probability).ToList();

            var test = program < 10;

            if (test.Pr(0.5))
            {
                Console.WriteLine("Assertion is true");
            }

            int x = 10;
        }
    }
}
