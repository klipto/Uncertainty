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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;

namespace UncertainTests
{
    [TestClass]
    public class MoreBaseTests
    {
        const double eps = 0.05;

        [TestMethod]
        public void Base_LIN0()
        {
            var tmp =
                       from a in new Flip(0.5)
                       select new Weighted<bool>()
                       {
                           Value = a,
                           Probability = a ? 0.9 : 0.1
                       };
            var foo = tmp.Support().ToDictionary(k => k.Value);
            Assert.IsTrue(foo[true].Probability == 0.9 * 0.5);
            Assert.IsTrue(foo[false].Probability == 0.1 * 0.5);

            var tmp0 =
                       from a in new Flip(0.5)
                       let path = a
                       let prob = a ? 0.9 : 0.1
                       select new Weighted<bool>()
                       {
                           Value = path,
                           Probability = prob
                       };
            foo = tmp0.Support().ToDictionary(k => k.Value);
            Assert.IsTrue(foo[true].Probability == 0.9 * 0.5);
            Assert.IsTrue(foo[false].Probability == 0.1 * 0.5);

            var tmp1 =
                       from a in new Flip(0.5)
                       from b in new Flip(0.5)
                       from c in new Flip(0.5)
                       let prob = a ? 0.9 : 0.1
                       select new Weighted<bool>()
                       {
                           Value = a | b | c,
                           Probability = prob
                       };
            foo = tmp1.Inference().Support().ToDictionary(k => k.Value);
            Assert.IsTrue(ApproxEqual(foo[true].Probability, 0.975));
            Assert.IsTrue(ApproxEqual(foo[false].Probability, 0.025));


            var tmp2 =
                       from a in new Flip(0.5)
                       from b in new Flip(0.5)
                       from c in new Flip(0.5)
                       where a
                       let prob = a ? 0.9 : 0.1
                       select new Weighted<bool>()
                       {
                           Value = a | b | c,
                           Probability = prob
                       };
            foo = tmp2.Inference().Support().ToDictionary(k => k.Value);
            Assert.IsTrue(ApproxEqual(foo[true].Probability, 1));
        }

        [TestMethod]
        public void Base_LINQ1()
        {
            var tmp0 =
                from a in new Flip(0.5)
                from b in new Flip(0.5)
                from c in new Flip(0.5)
                select a | b | c;

            var tmp1 =
               from a in new Flip(0.5)
               from b in new Flip(0.5)
               from c in new Flip(0.5)
               let path = a | b | c
               select path;

            var tmp2 =
               from a in new Flip(0.5)
               from b in new Flip(0.5)
               from c in new Flip(0.5)
               where a
               let path = a | b | c
               select path;
        }


        [TestMethod]
        public void Base_LINQ2()
        {
            var tmp =
                       from a in new Flip(0.5)
                       from b in new Flip(0.5)
                       from c in new Flip(0.5)
                       let path = Convert.ToInt32(a) + Convert.ToInt32(b) + Convert.ToInt32(c)
                       select path;            
        }


        [TestMethod]
        public void Base_LINQ3()
        {
            var tmp =
                       from a in new Flip(0.5)
                       from b in new Flip(0.5)
                       from c in new Flip(0.5)
                       select new Weighted<bool>()
                       {
                           Value = a | b | c,
                           Probability = a ? 0.9 : 0.1
                       };
        }


        // Test binomial inference
        [TestMethod]
        public void Base_Binomial()
        {
            Uncertain<int> binomial =  from a in new Flip(0.5)
                                       from b in new Flip(0.5)
                                       from c in new Flip(0.5)
                                       let path = Convert.ToInt32(a) + Convert.ToInt32(b) + Convert.ToInt32(c)
                                       select path;

            var dist = binomial.Inference().Support().OrderBy(k => k.Value).Select(k => k.Probability).ToList();
            var items = new[] { 0.125, 0.375, 0.375, 0.125 };
            for (int i = 0; i < items.Length; i++)
            {
                Assert.IsTrue(items[i] == dist[i]);
            }
        }

        // Test the base Uncertain<T> class rather than subclasses
        [TestMethod]
        public void Base_Sample()
        {
            // arrange
            Uncertain<double> x = new Multinomial<double>(new[] { 5.0 });
            // act
            MeanAndConfidenceInterval m2 = x.ExpectedValueWithConfidence();
            // assert
            Assert.IsTrue(m2.Mean > 5.0 - eps && m2.Mean < 5.0 + eps);
        }

        // Test the implicit conversion from T to Uncertain<T>
        [TestMethod]
        public void Base_Implicit()
        {
            // arrange
            Uncertain<double> X = 5.0;
            Uncertain<double> Y = 6.0;
            // act
            Uncertain<double> Z = from x in X 
                                  from y in Y
                                  select x + y;  // Y is implicitly cast
            
            // assert
            MeanAndConfidenceInterval m2 = Z.ExpectedValueWithConfidence();
            Assert.IsTrue(m2.Mean > 11.0 - eps && m2.Mean < 11.0 + eps);
        }

        // Test independence of operations
        [TestMethod]
        public void Base_TestMultinomial()
        {          
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
        public void Base_SampleCaching()
        {
            var X = new Bernoulli(0.5);
            var NotX = from a in X select !a;
            var XorNotX = from a in X from b in NotX select a | b;
            //var A = X | !X;
            // Without sample caching we get Prob = 0.75
            var Prob = XorNotX.ExpectedValueWithConfidence(100000);
            //Assert.IsTrue(ApproxEqual (Prob.Mean, 1.0));
        }


        public Uncertain<int> Geometric(double p)
        {
            var program = from a in new Flip(p)
                          let count = a ? (Uncertain<int>)0 : Geometric(p)
                          from b in count
                          select 1 + b;
            return program;
        }

        //public Uncertain<int> Geometric2(double p, int depth)
        //{
        //    if (depth == 0)
        //        return from a in new Flip(p) select Convert.ToInt32(a);

        //    return from a in new Flip(p)
        //           from b in Geometric2(p, depth - 1)
        //           let inc = a ? 1 : 0
        //           select inc + b;
        //}

        //[TestMethod]
        //public void TestRecur()
        //{
        //    var p = 0.5;
        //    var output = Geometric(p);
        //    var tmp = output.SampledInference(1000).Support().OrderBy(k => k.Value).ToList();
        //    int x = 10;
        //    //for (int k = 1; k <= tmp.Count; k++)
        //    //{
        //    //    var succ = Math.Pow((1 - p), k - 1) * p;
        //    //    Assert.IsTrue(ApproxEqual(succ, tmp[k - 1].Probability));
        //    //}
        //}

        [TestMethod]
        public void Base_TestEx()
        {
            var p0 = from a in new Flip(0.8)
                     from b in new Flip(0.2)
                     from c in new Flip(0.01)
                     select a | b | c;
            Assert.IsTrue(p0.Pr());
        }

        [TestMethod]
        public void Base_TestExpectedValue()
        {

            var X = new Gaussian(10, 1);
            var t = X.ExpectedValueWithConfidence(1000);
            var m = t.Mean;
            var ci = t.CI;

            Assert.IsTrue(ApproxEqual(m, 10, 0.5));
        }

        enum Gender { Male, Female };

        [TestMethod]
        public void Base_TestExpectedValue2()
        {
            var program = from manheight in new Gaussian(177, 64)
                          from womanheight in new Gaussian(164, 64)                          
                          select womanheight > manheight;
            Assert.IsFalse(program.Pr(0.5));
            //where womanheight > manheight
            var dist = program.SampledInference(10000).Support().ToList();
            
        }

        private static bool ApproxEqual(double a, double b, double eps = 0.05)
        {
            var diff = a - b;
            return diff > 0.0 - eps && diff < 0.0 + eps;
        }

        [TestMethod]
        public void Base_ConditionalProbability()
        {
            var ex = from a in new Bernoulli(0.1)
                     from b in new Bernoulli(0.1)
                     from c in new Bernoulli(0.1)
                     let d = Convert.ToInt32(a) + Convert.ToInt32(b) + Convert.ToInt32(c)
                     where d >= 2
                     select a;

            var tmp1 = ex.Inference().Support().OrderBy(k => k.Value);
            var tmp2 = ex.SampledInference(10000).Support().OrderBy(k => k.Value);

            var result = new Weighted<bool>[] {
                new Weighted<bool>() { Value = false, Probability = 0.3214285714285714},
                new Weighted<bool>() { Value = true, Probability = 0.6785714285714285},
            };

            foreach (var item in tmp1.Zip(result, Tuple.Create))
                Assert.IsTrue(ApproxEqual(item.Item1.Probability, item.Item2.Probability));

            foreach (var item in tmp2.Zip(result, Tuple.Create))
                Assert.IsTrue(ApproxEqual(item.Item1.Probability, item.Item2.Probability));
        }


        [TestMethod]
        public void Base_ConditionalProbability2()
        {
            var ex = from rain in new Bernoulli(0.2)
                     from sprinkler in new Bernoulli(0.2)
                     from w in new Bernoulli(0.05)
                     let wet = rain | sprinkler ? true : w
                     select Tuple.Create(rain, sprinkler);

            var tmp1 = ex.Inference().Support().OrderBy(k => k.Value);
            var result = new Weighted<Tuple<bool,bool>>[] {
                new Weighted<Tuple<bool,bool>>() { Value = Tuple.Create(false,false), Probability = 0.64}, 
                new Weighted<Tuple<bool,bool>>() { Value = Tuple.Create(false,true), Probability = 0.16}, 
                new Weighted<Tuple<bool,bool>>() { Value = Tuple.Create(true,false), Probability = 0.16}, 
                new Weighted<Tuple<bool,bool>>() { Value = Tuple.Create(true,true), Probability = 0.04}, 
            };
            foreach (var item in tmp1.Zip(result, Tuple.Create))
                Assert.IsTrue(ApproxEqual(item.Item1.Probability, item.Item2.Probability));
        }

        [TestMethod]
        public void Base_ConditionalProbability3()
        {
            var ex = from rain in new Bernoulli(0.2)
                     from sprinkler in new Bernoulli(0.2)
                     from w in new Bernoulli(0.05)
                     let wet = rain | sprinkler ? true : w
                     where wet
                     select Tuple.Create(rain, sprinkler);

            var tmp1 = ex.Inference().Support().OrderBy(k => k.Value);
            var result = new Weighted<Tuple<bool, bool>>[] {
                new Weighted<Tuple<bool,bool>>() { Value = Tuple.Create(false,false), Probability = 0.08163265306122447}, 
                new Weighted<Tuple<bool,bool>>() { Value = Tuple.Create(false,true), Probability = 0.4081632653061224}, 
                new Weighted<Tuple<bool,bool>>() { Value = Tuple.Create(true,false), Probability = 0.4081632653061224}, 
                new Weighted<Tuple<bool,bool>>() { Value = Tuple.Create(true,true), Probability = 0.1020408163265306}, 
            };
            foreach (var item in tmp1.Zip(result, Tuple.Create))
                Assert.IsTrue(ApproxEqual(item.Item1.Probability, item.Item2.Probability));
        }

        private static Uncertain<bool> Inner(bool x)
        {
            var ex = from y in new Flip(0.5)
                     let tmp = x ? 1.0 : y ? 0.9 : 0.1
                     from z in new Bernoulli(tmp)
                     where z
                     select y;
            return ex.Inference();
        }

        [TestMethod]
        public void BaseNestedInference()
        {
            var ex = from x in new Flip(0.5)
                     from y in Inner(x)
                     where !y
                     select x;

            var tmp1 = ex.Inference().Support().OrderBy(k => k.Value);
            var result = new Weighted<bool>[] {
                new Weighted<bool>() { Value = false, Probability = 0.1666666666666666 }, 
                new Weighted<bool>() { Value = true, Probability = 0.8333333333333334}, 
            };

            foreach (var item in tmp1.Zip(result, Tuple.Create))
                Assert.IsTrue(ApproxEqual(item.Item1.Probability, item.Item2.Probability));
        }


        [TestMethod]
        public void BaseNestedInference2()
        {
            var ex = from x in new Flip(0.5)
                     let inner =  from y in new Flip(0.5)
                                  let tmp = x ? 1.0 : y ? 0.9 : 0.1
                                  from z in new Bernoulli(tmp)
                                  where z
                                  select y

                     let groups =  from q in inner.Support()
                                   group q by q.Value into summary
                                   let prob = summary.Select(k => k.Probability).Aggregate((a, b) => a + b)
                                   select Tuple.Create(summary.Key, prob)

                     let norm =  (from item in groups select item.Item2).Sum()

                     let normalized = from g in groups 
                                      select new Weighted<bool>() { 
                                          Value = g.Item1, 
                                          Probability = g.Item2 / norm 
                                      }
                     let inferred = new Multinomial<bool>(normalized.ToList())

                     from y in inferred                        
                     where !y
                     select x;

            var tmp1 = ex.Inference().Support().OrderBy(k => k.Value);
            var result = new Weighted<bool>[] {
                new Weighted<bool>() { Value = false, Probability = 0.1666666666666666 }, 
                new Weighted<bool>() { Value = true, Probability = 0.8333333333333334}, 
            };

            foreach (var item in tmp1.Zip(result, Tuple.Create))
                Assert.IsTrue(ApproxEqual(item.Item1.Probability, item.Item2.Probability));
        }

        [TestMethod]
        public void ShowoffUncertainLINQ()
        {
            var program = from a in new Bernoulli(0.5)
                          from b in new Bernoulli(0.5)
                          let z = Convert.ToInt32(a) + Convert.ToInt32(b)
                          where z >= 1
                          select z;

            var output = program.Inference().Support().ToList();
            var output1 = program.SampledInference(1000).Support().ToList();
        }


        [TestMethod]
        public void Base_ExpectedValueGeneric()
        {
            var P = 0.5;
            var X = new Bernoulli(P);
            var E = X.ExpectedValueWithConfidence();
            var Err = Math.Abs(E.Mean - P);
            Assert.IsTrue(Err < eps);
        }
    }
}
