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
using System.Collections.Generic;
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

        // Test the base Uncertain<T> class rather than subclasses
        [TestMethod]
        public void Base_Sample() {
            // arrange
            Uncertain<double> X = 5.0;
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

        struct Document : IComparable<Document>, IComparer<Document> {
            public int rank, machine; public double score;

            public int Compare(Document x, Document y)
            {
                return x.CompareTo(y);
            }

            public int CompareTo(Document other)
            {
                //var a = Tuple.Create(this.machine, this.rank, this.score);
                //var b = Tuple.Create(other.machine, other.rank, other.score);
                //return Comparer<Tuple<int, int, double>>.Default.Compare(a, b);

                var a = Tuple.Create(this.machine, this.rank);
                var b = Tuple.Create(other.machine, other.rank);
                return Comparer<Tuple<int, int>>.Default.Compare(a, b);
            }

            public override string ToString()
            {
                return String.Format("{0}:{1}-{2:0.00}", this.machine, this.rank, this.score);
            }
        }

        internal class SequenceComparer<T> : IComparer<T[]>, IEqualityComparer<T[]> where T : IComparable<T>
        {
            public int Compare(T[] x, T[] y)
            {
                if (object.ReferenceEquals(x, y))
                    return 0;

                if (x.Length < y.Length)
                    return -1;
                if (x.Length > y.Length)
                    return 1;

                foreach(var pair in x.Zip(y, Tuple.Create))
                {
                    var r = pair.Item1.CompareTo(pair.Item2);
                    if (r == 0)
                        continue;
                    return r;
                }

                return 0;
            }

            public bool Equals(T[] x, T[] y)
            {
                if (object.ReferenceEquals(x, y))
                    return true;

                if (x.Length != y.Length)
                    return false;

                for (int i = 0; i < x.Length; i++)
                    if (!x[i].Equals(y[i]))
                        return false;
                return true;
            }

            public int GetHashCode(T[] obj)
            {
                var hash = obj.Length;
                for (int i = 0; i < obj.Length; i++)
                    hash ^= obj[i].GetHashCode();
                return hash;
            }
        }
        [TestMethod]
        public void LinearRegression()
        {
            const int N = 50;
            const int F = 4;
            var noise = new RandomMath();
            var x = (from i in Enumerable.Range(0, N)
                    let xi = from j in Enumerable.Range(0, F)
                             let feature = j == 0 ? 1.0 : noise.NextGaussian(j, 1)
                             select feature
                    select xi.ToArray()).ToArray();

            var y = (from xi in x
                    select xi.Select((f, index) => f * (index + 1)).Aggregate(noise.NextGaussian(0, 1), (a,b) => a + b)).ToArray();
            //var y = x.Select(pair => pair[0] * 3 + pair[1] * 5 + noise.NextGaussian(0, 1)).ToArray();
            //var weights = Enumerable.Range(0, 10).Select(_ => new Gaussian(0, 20)).USeq();

            //Func < double, double, IEnumerable<Tuple<double, double>>> Evaluate = (w0, w1) => from i in Enumerable.Range(0, N)
            //                                                                                let yhat = w0 * x[i][0] + w1 * x[i][1]
            //                                                                                select Tuple.Create(yhat, y[i]);

            //Func<double, double, double, double> Score = (w0, w1, sigma) => (from i in Enumerable.Range(0, N)
            //                                                                 let yhat = w0 * x[i][0] + w1 * x[i][1]
            //                                                                 let likelihood = Gaussian.Likelihood(yhat, y[i], sigma)
            //                                                                 select likelihood).Sum();
            Func<double[], double, double> Score = (weights, sigma) => (from i in Enumerable.Range(0, N)
                                                                        let yhat = (from j in Enumerable.Range(0, F)
                                                                                    select weights[j] * x[i][j]).Sum()
                                                                        let likelihood = Gaussian.Likelihood(yhat, y[i], sigma)
                                                                        select likelihood).Sum();

            Uncertain<Tuple<double[], double>> model = from sigma in new Uniform<double>(0.5, 2)
                                                             from weights in Enumerable.Range(0, F).Select(_ => new Gaussian(0, 20)).USeq()
                                                             let likelihood = Score(weights, sigma)
                                                             select new Weighted<Tuple<double[], double>>
                                                             {
                                                                 Value = Tuple.Create(weights, sigma),
                                                                 Probability = likelihood
                                                             };

            

            //Uncertain<Tuple<double, double, double>> model = FunctionFromProgrammer(x, y);

            //var foo = from result in (model . Inference())
            //          let tmp2 = Evaluate(result.Item1, result.Item2).Select(i => i.Item1).Sum()
            //          select tmp2;

            //if ((foo > 2).Pr(0.9))
            //{
            //    Console.Write("Here");
            //}

            var tmp = model.SampledInference(1000000).Support().OrderByDescending(i => i.Probability).Take(10).ToList();

            //var sampler = Sampler.Create(model);
            //Tuple<double, double, double> best = null;
            //double score = double.NegativeInfinity;
            //ulong count = 0;
            //foreach (var sample in sampler)
            //{
            //    if (sample.Probability > score)
            //    {
            //        best = sample.Value;
            //        score = sample.Probability;
            //    }
            //    if (count++ % 100000 == 0)
            //    {
            //        System.Diagnostics.Debug.WriteLine(String.Format("{0} {1}:{2}:{3}", score, best.Item1, best.Item2, best.Item3));
            //    }
            //}
            //var tmp = model.SampledInference(1000000).Support().OrderByDescending(i => i.Probability).Take(10).ToList();
            //var y = new[] { 6.27836080009046, 8.2746679801523015, 11.39714940761103, 15.156909876331556, 17.218478842502289, 18.928109597586118, 24.262721976201803, 26.362782501038339, 29.817028452916407, 32.484031601922716};
            int tmp1 = 10;

        }

        // Func < string, Document[]> localsearch = q0 =>
        // {
        //     var docs = from i in Enumerable.Range(0, 3)
        //                let doc = new Document { rank = i, machine = jf, score = rand.NextDouble() }
        //                orderby doc.score descending
        //                select doc;
        //     return docs.ToArray();
        // };

            // Document[] localresult = localsearch(q); // assumes sorted by score

            // Func<double, Uncertain<double>> FiniteRandomScore = (s) =>
            // {
            //     return from bias in new FiniteEnumeration<double>(new[] { -0.01, -0.1, 0, 0.1, 0.01 })
            //            select s + bias;
            // };


            // Uncertain<Document[]> ranks = localresult.Select(doc => FiniteRandomScore(doc.score)).USeq<double, Document>(scores =>
            //{
            //    var tmp = from index in Enumerable.Range(0, scores.Length)
            //              let doc = localresult[index]
            //              orderby scores[index] descending
            //              select doc;

            //    return tmp.ToArray();
            //});

            // return ranks;

        [TestMethod]
        public void Foo1()
        {
            Func<string, int, Uncertain<Document[]>> Search = (q, machineIndex) =>
            {
                var rand = new Random(machineIndex);
                var documents = from documentIndex in Enumerable.Range(0, 5)
                                let score = rand.NextDouble()
                                select new { documentIndex, score };

                var tmp = from bias in new FiniteEnumeration<double>(new[] { -0.01, -0.5, 0, 0.1, 0.05 })
                          //Gaussian(0, 1) //new[] { -0.01, -0.5, 0, 0.1, 0.05 })
                          let docs = from document in documents
                                     let doc = new Document { rank = document.documentIndex, machine = machineIndex, score = document.score + bias }
                                     orderby doc.score descending
                                     select doc
                            select docs.ToArray();
                                     //select new Weighted<Document[]>
                                     //{
                                     //    Value = docs.ToArray(),
                                     //    Probability = docs.Select(i => i.score).Sum()
                                     //};
                                     //select docs.ToArray();
                                     //var alltmp = tmp.Inference(new SequenceComparer<Document>()).Support().OrderByDescending(i => i.Probability).ToList();
                return tmp;
            };

            var query = "foo";
            var sequenceComparer = new SequenceComparer<Document>();

            //var groundTruth = (
            //    from s1 in Search(query, 0)
            //                   from s2 in Search(query, 1)
            //                   from s3 in Search(query, 2)
            //                   let combined = s1.Concat(s2).Concat(s3).ToArray()
            //                   let sorted = combined.OrderByDescending(i => i.score).ToArray()
            //                   select sorted)
            //                  .SampledInference(100,sequenceComparer).Support().OrderByDescending(i => i.Probability).ToArray();

            // If xx is the same as yy return 0
            // else return how different they are
            Func<Document[], Document[], double> AreSame = (xx, yy) =>
            {
                if (xx.Length != yy.Length)
                    throw new Exception();

                var sum = 0;
                foreach (var pair in xx.Zip(yy, Tuple.Create))
                {
                    sum += pair.Item1.CompareTo(pair.Item2) == 0 ? 0 : 1;
                }

                return sum / (double) xx.Length;
            };

            Func<Uncertain<Document[]>, Uncertain<Document[]>, int, double> ApproximatelyCorrect = (control1, treatment, k) =>
            {
                var a = control1.Inference(sequenceComparer).Support().OrderBy(i => i.Value, sequenceComparer).ToList();
                var b = treatment.Inference(sequenceComparer).Support().OrderBy(i => i.Value, sequenceComparer).ToList();

                if (a.Count < k ||  b.Count < k)
                    throw new Exception();

                var sum = 0.0;
                for(int i = 0; i < k; i++)
                {
                    var score = AreSame(a[i].Value, b[i].Value);
                    var prob = a[i].Probability * b[i].Probability;
                    sum += score * prob;
                }

                return 1 - (sum / (double) k);
            };

            // run inference once and cache result
            var control = (from s1 in Search(query, 0)
                           from s2 in Search(query, 1)
                           from s3 in Search(query, 2)
                           let combined = s1.Concat(s2).Concat(s3).ToArray()
                           let sorted = combined.OrderByDescending(i => i.score).ToArray()
                           select sorted).Inference(sequenceComparer);

            var ks = from k in new FiniteEnumeration<int>(new [] { 25, 50, 100 })
                       let treatment = from s1 in Search(query, 0).SampledInference(k, sequenceComparer)
                                       from s2 in Search(query, 1).SampledInference(k, sequenceComparer)
                                       from s3 in Search(query, 2).SampledInference(k, sequenceComparer)
                                       let combined = s1.Concat(s2).Concat(s3).ToArray()
                                       let sorted = combined.OrderByDescending(i => i.score).ToArray()
                                       select sorted
                       let prob = ApproximatelyCorrect(control, treatment, 3)
                       select new Weighted<int> { Value = k, Probability = prob };

            var posterior = ks.Inference().Support().OrderByDescending(i => i.Probability).ToList();

            //.SampledInference(100,sequenceComparer).Support().OrderByDescending(i => i.Probability).ToArray();
            //foreach(var pair in groundTruth.Zip(test, Tuple.Create))
            //{
            //    var control    = String.Format("[{0}]:{1:0.00}", String.Join(" ", pair.Item1.Value), pair.Item1.Probability);
            //    var treatment  = String.Format("[{0}]:{1:0.00}", String.Join(" ", pair.Item2.Value), pair.Item2.Probability);
            //    System.Diagnostics.Debug.WriteLine(control);
            //    System.Diagnostics.Debug.WriteLine(treatment);
            //    System.Diagnostics.Debug.WriteLine(String.Empty);
            //    if (control != treatment)
            //        throw new Exception();
            //}

            int x = 10;
            //var c = from s1 in Search(query, 0)
            //        from s2 in Search(query, 1)
            //        let sorted = Sort(s1, s2)
            //        select sorted;

            //var c1 = from s1 in SearchTopK(query, 0)
            //        from s2 in Search(query, 1)
            //        let sorted = Sort(s1, s2)
            //        select sorted;


            //var correctAnswer = c.Inference().Support().OrderBy(i => i.Probability).ToList();
            //var estimatedAnswer = c1.Inference().Support().OrderBy(i => i.Probability).ToList();
            // do comparison here between correct and estimated

        }


        [TestMethod]
        public void Foo()
        {
            var program =
                          from a in new Flip(0.1)
                          from b in a ? new Flip(0.1) : new Flip(0.5)
                              //from c in new Flip(0.5)
                          select Convert.ToInt32(a) + Convert.ToInt32(b);// + Convert.ToInt32(c);


            var inference = program.SampledInference(10000).Support().OrderByDescending(i => i.Probability).ToList();

            //let prob = a ? 10 : 0
            //select new Weighted<int>
            //{
            //    Value = Convert.ToInt32(a) + Convert.ToInt32(b) + Convert.ToInt32(c),
            //    Probability = prob
            //};

            //where a
            //select Convert.ToInt32(a) + Convert.ToInt32(b) + Convert.ToInt32(c);

            var allpaths = program.Support().ToList();

            //var inference = program.Inference().Support().ToList();



            //var inference = program.SampledInference(10000).Support().OrderByDescending(i => i.Probability).ToList();

            //var tmp = from pair in allpaths
            //          group pair by pair.Value into summary
            //          let sum = summary.Aggregate(i => i.Probability)
            //          select new { pair.Value, pair.Probability / (double)sum };

            int x = 10;
        }
    }
}
