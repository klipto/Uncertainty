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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.Research.Uncertain
{
    public static class Extensions
    {
        internal const int EXPECTED_VALUE_SAMPLE_SIZE = 1000;

        /// <summary>
        /// The maximum sample size to try for sequential sampling in <c>Pr()</c> before giving up.
        /// </summary>
        internal const int MAX_SAMPLE_SIZE = 10000;
        /// <summary>
        /// The initial sample size for sequential sampling in <c>Pr()</c>.
        /// </summary>
        internal const int INITIAL_SAMPLE_SIZE = 10;
        /// <summary>
        /// The sample size incrememnt between hypothesis tests for sequential sampling in <c>Pr()</c>.
        /// </summary>
        internal const int SAMPLE_SIZE_STEP = 10;

        internal static Random rand;
        static Extensions()
        {
            Extensions.rand = new Random();
        }
        internal static double NextRandom()
        {
            return Extensions.rand.NextDouble();
        }
        internal static double NextUniform(double a, double b)
        {
            double Range = b - a;
            return NextRandom() * Range + a;
        }
        public static T ExpectedValue<T>(this Uncertain<T> source, int SampleSize = EXPECTED_VALUE_SAMPLE_SIZE)
        {
            var sampler = Sampler.Create(source);
            var data = sampler.Take(SampleSize).ToList();

            // http://en.wikipedia.org/wiki/Weighted_arithmetic_mean
            var N = (double)data.Count();
            var WeightSum = (from k in data select k.Probability).Sum();
            var SumOfSquares = data.Select(x => Math.Pow(x.Probability, 2)).Sum();

            // weighted mean
            var Xbar = data.Select(x => (dynamic)x.Value * x.Probability).Aggregate((a, b) => a + b) / WeightSum;
            return (T)Xbar;
        }


        public static MeanAndConfidenceInterval ExpectedValueWithConfidence<T>(this Uncertain<T> source, int SampleSize = EXPECTED_VALUE_SAMPLE_SIZE)
        //  where T : IConvertible
        {
            const double CI_AT_95 = 1.96;
            var sampler = Sampler.Create(source);

            var data = (from k in sampler.Take(SampleSize)
                        select new Weighted<double>()
                        {
                            Value = Convert.ToDouble(k.Value),
                            Probability = k.Probability
                        }).ToList();


            // http://en.wikipedia.org/wiki/Weighted_arithmetic_mean
            var N = (double)data.Count();
            var WeightSum = (from k in data select k.Probability).Sum();
            var SumOfSquares = data.Select(x => Math.Pow(x.Probability, 2)).Sum();

            // weighted mean
            var Xbar = data.Select(x => x.Value * x.Probability).Sum() / WeightSum;
            // unbiased estimate of sigma
            var NormalizationFactor = (WeightSum - (SumOfSquares / WeightSum));
            // population weighted StdDev
            var SigmaBar = Math.Sqrt(
                 data.Select(
                     x => x.Probability * Math.Pow(x.Value - Xbar, 2)).Sum()
                     / NormalizationFactor);
            // TODO: defaults to 95% confidence interval: 
            // we should really use the T distribution
            // Result is a bias for small number of samples.
            var C = CI_AT_95 * SigmaBar / Math.Sqrt(N);

            return new MeanAndConfidenceInterval
            {
                Mean = Xbar,
                CI = C,
            };
        }

        /// <summary>
        /// Decide if this Bernoulli is true with probability at least <paramref name="Prob"/>.
        /// </summary>
        /// <remarks>
        /// This method implements Wald's Sequential Probability Ratio Test (SPRT) for the special
        /// case where the distribution is a Bernoulli. The log-likelihood is therefore simply a
        /// function of the number of Trues sampled.
        /// </remarks>
        /// <param name="Prob">The probability threshold to compare against</param>
        /// <param name="Alpha">The confidence level of the hypothesis test</param>
        /// <param name="Epsilon">The indifference region for the hypothesis test</param>
        /// <param name="MaxSampleSize">Maximum number of samples to draw before giving up</param>
        /// <param name="InitSampleSize">Initial number of samples to draw</param>
        /// <param name="SampleSizeStep">Number of samples to draw between each hypothesis test</param>
        /// <returns>True if this Bernoulli is true with probability at least <paramref name="Prob"/></returns>
        public static bool Pr(this Uncertain<bool> source, double Prob = 0.5,
                double Alpha = 0.05, double Epsilon = 0.03,
                int MaxSampleSize = MAX_SAMPLE_SIZE,
                int InitSampleSize = INITIAL_SAMPLE_SIZE,
                int SampleSizeStep = SAMPLE_SIZE_STEP)
        {
            int num_samples;
            return source.Pr(out num_samples, Prob, Alpha, Epsilon, MaxSampleSize, InitSampleSize, SampleSizeStep);
        }

        public static bool Pr(this Uncertain<bool> source, out int num_samples, double Prob = 0.5,
                double Alpha = 0.05, double Epsilon = 0.03,
                int MaxSampleSize = MAX_SAMPLE_SIZE,
                int InitSampleSize = INITIAL_SAMPLE_SIZE,
                int SampleSizeStep = SAMPLE_SIZE_STEP)
        {
            // The hypotheses being compared
            double H_0 = Prob - Epsilon;  // H_0 : p <= prob - epsilon
            double H_1 = Prob + Epsilon;  // H_1 : p >= prob + epsilon

            // Decide the log-likelihood thresholds for the test
            double Beta = Alpha;  // We are symmetric w.r.t. false positives/negatives
            double A = Math.Log(Beta / (1 - Alpha));  // Accept H_0 if the log-likelihood is <= a
            double B = Math.Log((1 - Beta) / Alpha);  // Accept H_1 if the log-likelihood is >= b

            // Draw the initial samples
            int K = 0;  // number of successes in n trials

            double WSum = 0.0;
            double WSumTrue = 0.0;

            IEnumerator<Weighted<bool>> enumerator = Sampler.Create(source).GetEnumerator();
            Func<Weighted<bool>> FuncSampler = () =>
            {
                var tmp = enumerator.Current;
                if (enumerator.MoveNext() == false)
                    throw new Exception("Ran out of data!");
                return tmp;
            };
            for (num_samples = 0; num_samples < InitSampleSize; num_samples++)
            {
                var sample = FuncSampler();
                if (sample.Value)
                {
                    K += 1;
                    WSumTrue += sample.Probability;
                }
                WSum += sample.Probability;
            }

            while (num_samples <= MaxSampleSize)
            {
                // Calculate the log-likelihood of the data seen so far
                double LogLikelihood = WSumTrue * Math.Log(H_1 / H_0) + (WSum - WSumTrue) * Math.Log((1 - H_1) / (1 - H_0));

                // If we can accept H_1 then P > Prob, so return true
                if (LogLikelihood >= B)
                {
                    return true;
                }
                // If we can accept H_0 then P < Prob, so return false
                if (LogLikelihood <= A)
                {
                    return false;
                }

                // Otherwise, continue sampling
                for (int i = 0; i < SampleSizeStep; i++)
                {
                    var sample = FuncSampler();
                    if (sample.Value)
                    {
                        K += 1;
                        WSumTrue += sample.Probability;
                    }
                    WSum += sample.Probability;
                }

                num_samples += SampleSizeStep;
            }

            // If the maximum sample size is reached, assume the answer is false. This is an
            // (mostly unjustified) assumption that false positives are more damaging.
            return false;
        }

        public static Uncertain<TResult> Select<TSource, TResult>(
            this Uncertain<TSource> first,
            Func<TSource, Weighted<TResult>> projection)
        {
            return new Select<TSource, TResult>(first, projection);
        }

        public static Uncertain<TResult> Select<TSource, TResult>(
            this Uncertain<TSource> first,
            Func<TSource, TResult> projection)
        {
            return new Select<TSource, TResult>(
                first,
                i => new Weighted<TResult>() { Value = projection(i), Probability = 1.0 }
            );
        }

        public static Uncertain<TResult> SelectMany<TSource, TCollection, TResult>(
            this Uncertain<TSource> first,
            Expression<Func<TSource, Uncertain<TCollection>>> collectionSelector,
            Expression<Func<TSource, TCollection, Weighted<TResult>>> resultSelector)
        {
            return new SelectMany<TSource, TCollection, TResult>(first, collectionSelector, resultSelector);
        }

        public static Uncertain<TResult> SelectMany<TSource, TCollection, TResult>(
            this Uncertain<TSource> first,
            Expression<Func<TSource, Uncertain<TCollection>>> collectionSelector,
            Expression<Func<TSource, TCollection, TResult>> resultSelector)
        {
            return new SelectMany<TSource, TCollection, TResult>(
                first,
                collectionSelector,
                (a, b) => new Weighted<TResult>() { Value = (resultSelector.Compile())(a, b), Probability = 1.0 }
            );
        }
    }
}

namespace Microsoft.Research.Uncertain.Inference
{
    public static class Extensions
    {
        public static Dictionary<object, Tuple<string, int>> inferences = new Dictionary<object, Tuple<string,int>>();

        public static Uncertain<T> RunInference<T>(IList<Weighted<T>> data, IEqualityComparer<T> comparer = null)
        {
            if (comparer == null)
                comparer = EqualityComparer<T>.Default;
            // group by program paths
            var program = data.GroupBy(value => value.Value, comparer).Select(summary =>
            {
                var path = summary.Key;
                var sum = summary.Select(k => k.Probability).Aggregate((a, b) => a + b);
                return Tuple.Create(path, sum);
            });

            // normalization constant
            var numpaths = (double)(from item in program select item.Item2).Sum();

            // normalize
            var weighted = from item in program
                           select new Weighted<T>
                           {
                               Value = item.Item1,
                               Probability = item.Item2 / numpaths
                           };
            return new FiniteEnumeration<T>(weighted.ToList());
        }

        //public static Uncertain<T> Inference2<T>(this Uncertain<T> source, IEqualityComparer<T> comparer = null)
        //{
        //  return new Inference<T>(source, comparer);
        //return RunInference(source.Support().ToList(), comparer);
        //}

        public static Uncertain<T> Inference<T>(this Uncertain<T> source, IEqualityComparer<T> comparer = null)
        {
            inferences.Add(new Inference<T>(source, comparer), new Tuple<string, int>(source.ToString(), 0));
            return RunInference(source.Support().ToList(), comparer);
        }

        public static Uncertain<T> SampledInference<T>(this Uncertain<T> source, int samplesize, IEqualityComparer<T> comparer = null)
        {
            var sampler = Sampler.Create(source);
            // cache data
            var data = sampler.Take(samplesize).ToList();
            inferences.Add(new Inference<T>(source, comparer), new Tuple<string, int>(source.ToString(),samplesize));
            return RunInference(data, comparer);
        }

        public static Uncertain<T> Where<T>(this Uncertain<T> source, Predicate<T> predicate)
        {
            return new Where<T>(source, predicate);
        }

        public class FunctionalList<T> : IEnumerable<T>
        {
            // Creates a new list that is empty
            public FunctionalList()
            {
                IsEmpty = true;
            }
            // Creates a new list containe value and a reference to tail
            public FunctionalList(T head, FunctionalList<T> tail)
            {
                IsEmpty = false;
                Head = head;
                Tail = tail;
            }
            // Is the list empty?
            public bool IsEmpty { get; private set; }
            // Properties valid for a non-empty list
            public T Head { get; private set; }
            public FunctionalList<T> Tail { get; private set; }

            public IEnumerator<T> GetEnumerator()
            {
                return FunctionalList.Helper<T>(this).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return FunctionalList.Helper<T>(this).GetEnumerator();
            }
        }

        // Static class that provides nicer syntax for creating lists
        public static class FunctionalList
        {
            public static FunctionalList<T> Empty<T>()
            {
                return new FunctionalList<T>();
            }
            public static FunctionalList<T> Cons<T>
                    (T head, FunctionalList<T> tail)
            {
                return new FunctionalList<T>(head, tail);
            }

            internal static IEnumerable<T> Helper<T>(FunctionalList<T> lst)
            {
                if (lst.IsEmpty) yield break;
                yield return lst.Head;
                foreach (var item in Helper(lst.Tail))
                    yield return item;
            }

            public static T[] ToArray<T>(FunctionalList<T> lst)
            {
                var array = Helper<T>(lst).ToArray();
                return array;
            }
        }


        public static Uncertain<R[]> USeq<T, R>(this IEnumerable<Uncertain<T>> source, Func<T[], R[]> selector)
        {
            Uncertain<R[]> output = source.Aggregate<Uncertain<T>, Uncertain<FunctionalList<T>>, Uncertain<R[]>>(
                FunctionalList.Empty<T>(),
                (i, j) =>
                {
                    return from lst in i
                           from sample in j
                           select FunctionalList.Cons(sample, lst);
                },
                uncertainlst =>
                {
                    return from sample in uncertainlst
                           let vec = sample.Reverse().ToArray()
                           select selector(vec);
                });
            return output;
        }
        //TODO: implement inference via group by?
        //public static IEnumerable<Tuple<K, Uncertain<T>>> GroupBy<T, K>(this Uncertain<T> source, Func<T, K> keySelector) where K : IComparable
        //public static Uncertain<Tuple<K, T>> GroupBy<T, K>(this Uncertain<T> source, Func<T, K> keySelector) where K : IComparable
        //{
        //    Tuple<K, T> empty = null;

        //    // ugly! goes over data many times with a filter... 
        //    // could cache samples that may match OTHER keys.
        //    Func<K, Uncertain<Tuple<K, T>>> Sampler = k => from i in source
        //                                                   where keySelector(i).Equals(k) // rejection sampling
        //                                                   select Tuple.Create(k, i);

        //    var set = new HashSet<K>(); // keep track of seen keys
        //    Func<K, bool> FindItemInSetOrAdd = k =>
        //    {
        //        if (set.Contains(k) == false)
        //        {
        //            set.Add(k);
        //            return false;
        //        }

        //        return true;
        //    };

        //    var tmp = from item in source
        //              let Key = keySelector(item)
        //              let groupedItems = set.Contains(Key) ? (Uncertain<Tuple<K, T>>)empty : Sampler(Key)
        //              where Object.ReferenceEquals(empty, groupedItems) == false
        //              from g in groupedItems
        //              select g;

        //    return tmp;
        //}
    }
}
