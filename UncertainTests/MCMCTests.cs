using System;
using System.Linq;
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace UncertainTests
{
    [TestClass]
    public class MCMCTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            var program = from a in new Flip(0.9)
                          from b in new Flip(0.9)
                          from c in new Flip(0.9)
                          //where !a
                          select Convert.ToInt32(a) + Convert.ToInt32(b) + Convert.ToInt32(c);

            var sampler = new MarkovChainMonteCarloSampler<int>(program);

            var tmp = Microsoft.Research.Uncertain.Inference.Extensions.RunInference(sampler.Take(100000).ToList()).Support().ToList();

            var correct = program.Inference().Support().ToList();

            int x = 10;
        }

        [TestMethod]
        public void TestMethod4()
        {
            var program = from a in new Flip(0.9)
                          from b in new Flip(0.9)
                          from c in new Flip(0.9)
                          let d = Convert.ToInt32(a) + Convert.ToInt32(b) + Convert.ToInt32(c)
                          select new Weighted<int>(d, a ? 0.0 : 1);

            var sampler = new MarkovChainMonteCarloSampler<int>(program);

            var tmp = Microsoft.Research.Uncertain.Inference.Extensions.RunInference(sampler.Take(100000).ToList()).Support().ToList();

            var correct = program.Inference().Support().ToList();

            int x = 10;
        }

        public Uncertain<int> Geometric(double p)
        {
            var program = from a in new Flip(p)
                          let count = a ? (Uncertain<int>)0 : Geometric(p)
                          from b in count
                          select 1 + b;
            return program;
        }

        [TestMethod]
        public void TestMethod2()
        {
            var program = from i in Geometric(0.9)
                          where i <= 5
                          select i;

            var sampler = new MarkovChainMonteCarloSampler<int>(program);
            var tmp = Microsoft.Research.Uncertain.Inference.Extensions.RunInference(sampler.Take(100000).ToList()).Support().ToList();

            int x = 10;
        }

        [TestMethod]
        public void TestDan()
        {
            var r = new RandomMath();
            int N = 50;
            // Generate an array of independent estimates of whether a signal
            // is high or low
            Uncertain<bool>[] data = (from i in Enumerable.Range(0, N)
                                      let noise = r.NextGaussian(0, 0.01)
                                      let vad = i > 15 && i < 30 ? 0.9 : 0.01
                                      let param = Math.Abs(vad + noise)
                                      let f = new Flip(param > 1 ? 1 : param)
                                      select f).ToArray();
            // history operator we chatted about
            Uncertain<bool[]> history = data.History(N);

            // Inference computes a weighted bool[] object: effectively a histogram
            // The call to SampledInference needs to know (i) how many samples to take and how to compare bool[]
            Uncertain<bool[]> posterior = history.SampledInference(10000, new BoolArrayEqualityComparer());

            // now inspect by materializing a list
            List<Weighted<bool[]>> top5 = posterior
                .Support() // enumerate the histogram                
                .OrderByDescending(k => k.Probability) // sorted by probability
                .Take(5) // just top 5
                .ToList(); // produce list

            // set breakpoint
            int x = 10;
        }
    }
    public class FunctionalList<T>
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

        private static IEnumerable<T> Helper<T>(FunctionalList<T> lst)
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
    public static class MyExtensions
    {
        public static Uncertain<T[]> History<T>(this IEnumerable<Uncertain<T>> source, int num)
        {
            Uncertain<T[]> output = source.Take(num).Aggregate<Uncertain<T>, Uncertain<FunctionalList<T>>, Uncertain<T[]>>(
                FunctionalList.Empty<T>(),
                (i, j) =>
                {
                    return i.SelectMany(acc => j, (f, g) => FunctionalList.Cons(g, f));
                },
                k =>
                {
                    return (from lst in k
                            let asArray = FunctionalList.ToArray(lst)
                            select asArray);
                });
            return output;
        }
    }

    internal class BoolArrayEqualityComparer : IEqualityComparer<bool[]>
    {
        public bool Equals(bool[] x, bool[] y)
        {
            if (object.ReferenceEquals(x, y))
                return true;

            if (x.Length != x.Length)
                return false;

            for (int i = 0; i < x.Length; i++)
                if (x[i] != y[i])
                    return false;
            return true;
        }

        public int GetHashCode(bool[] obj)
        {
            var hash = obj.Length;
            for (int i = 0; i < obj.Length; i++)
                hash ^= obj[i] ? 11 : 13;
            return hash;
        }
    }

}
