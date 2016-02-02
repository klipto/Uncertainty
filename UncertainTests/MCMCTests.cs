using System;
using System.Linq;
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Collections;

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

        static Uncertain<string[]> HMM(string[] obs)
        {
            var states = new[] { "Healthy", "Fever" };

            var emits = new[] { "normal", "cold", "dizzy" };

            var start_probability = new Multinomial<string>(states, new[] { 0.6, 0.4 });

            Func<string, Multinomial<string>> transition_probability = state =>
            {
                if (state == "Healthy")
                    return new Multinomial<string>(states, new[] { 0.7, 0.3 });
                if (state == "Fever")
                    return new Multinomial<string>(states, new[] { 0.4, 0.6 });

                throw new Exception("Unknown state");
            };

            Func<string, Multinomial<string>> emission_probability = state =>
            {
                if (state == "Healthy")
                    return new Multinomial<string>(emits, new[] { 0.5, 0.4, 0.1 });
                if (state == "Fever")
                    return new Multinomial<string>(emits, new[] { 0.1, 0.3, 0.6 });

                throw new Exception("Unknown state");
            };

            return
                from prior in start_probability

                from state0 in transition_probability(prior)
                from emit0 in emission_probability(state0)
                where obs[0] == emit0

                from state1 in transition_probability(state0)
                from emit1 in emission_probability(state1)
                where obs[1] == emit1

                from state2 in transition_probability(state1)
                from emit2 in emission_probability(state2)
                where obs[2] == emit2

                select new[] { state0, state1, state2 };
        }

        [TestMethod]
        public void TestHMM2()
        {
            var comparer = new SequenceComparer<string>();
            var states = new[] { "Healthy", "Fever" };
            var emits = new[] { "normal", "cold", "dizzy" };

            Uncertain<string>[] source = new[] {
                new Multinomial<string>(emits, new [] {0.8, 0.1, 0.1}),
                new Multinomial<string>(emits, new [] {0.2, 0.7, 0.1}),
                new Multinomial<string>(emits, new [] {0.05, 0.05, 0.9}),
            };

            Uncertain<string[]> observations = source.USeq(3);

            var program = from obs in observations
                          from result in HMM(obs)
                          select result;

            var program1 = from obs in observations
                          from result in HMM(obs).Inference(comparer)
                          select result;

            var program2 = from obs in observations.Inference(comparer)
                           from result in HMM(obs).Inference(comparer)
                           select result;

            var output = program.Inference(comparer).Support().OrderByDescending(k => k.Probability).ToList();
            var output1 = program1.Inference(comparer).Support().OrderByDescending(k => k.Probability).ToList();
            var output2 = program2.Inference(comparer).Support().OrderByDescending(k => k.Probability).ToList();

            var timer = new System.Diagnostics.Stopwatch();

            timer.Start();
            var sampled = program.SampledInference(10000, comparer).Support().OrderByDescending(k => k.Probability).ToList();
            timer.Stop();
            System.Diagnostics.Debug.WriteLine(timer.Elapsed);
            timer.Reset();
            timer.Start();
            var sampled1 = program1.SampledInference(10000, comparer).Support().OrderByDescending(k => k.Probability).ToList();
            timer.Stop();
            System.Diagnostics.Debug.WriteLine(timer.Elapsed);
            timer.Reset();
            timer.Start();
            var sampled2 = program2.SampledInference(10000, comparer).Support().OrderByDescending(k => k.Probability).ToList();
            timer.Stop();
            System.Diagnostics.Debug.WriteLine(timer.Elapsed);

            //Assert.AreEqual(output[0].Value, sampled[0].Value);
            //Assert.IsTrue(Math.Abs(output[0].Probability - sampled[0].Probability) < 0.1);

            int x = 10;
        }

        struct Audio { }

        public static bool[] Intervalize(bool[] x)
        {
            return x;
        }
        public static Uncertain<bool[]> PIntervalize(Uncertain<bool>[] x)
        {
            return null;
        }


        [TestMethod]
        public void VAD()
        {
            Func<IEnumerable<Audio>> ReadMicrophone = () => Enumerable.Empty<Audio>();
            Func<Audio, bool> Detect_Speech_Sample = _ => true;
            Func<Audio, Uncertain<bool>> Detect_Speech_Sample_Uncertain = _ => true;

            var speaking = from audio in ReadMicrophone()
                           select Detect_Speech_Sample(audio);
            var program1 = from speaking_history in speaking.History(50)
                           select Intervalize(speaking_history);

            var speakingu = from audio in ReadMicrophone()
                            select Detect_Speech_Sample_Uncertain(audio);
            var program2 = from speaking_historyu in speakingu.History(50)
                           select speaking_historyu.USeq2(Intervalize);

            var program3 = from speaking_historyu in speakingu.History(50)
                           from item in speaking_historyu.USeq(50)
                           select Intervalize(item);

            var program4 = from speaking_historyu in speakingu.History(50)
                           select PIntervalize(speaking_historyu);

            //var speaking = from sample in audio select Detect_Speech_Sample(sample);
            //var speaking_history = speaking.Take(50).ToArray();
            //var ints = from tmp in speaking_history select Intervalize(tmp);


            //var speaking_uncertain = from sample in audio select Detect_Speech_Sample_Uncertain(sample);
            //var speaking_history_uncertain = speaking_uncertain.Take(50).ToArray();


            //var program2 = from audio in (Uncertain<Audio>)ReadMicrophone()
            //               from speaking_history in Detect_Speech_Sample(audio).History(50)
            //               let interval = Intervalize(speaking_history)
            //               select interval;

            //var program3 = from audio in (Uncertain<Audio>)ReadMicrophone()
            //               let speaking_history = Detect_Speech_Sample(audio)
            //               select ProbabilisticIntervalize(speaking_history);
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
                                      let vad = i > 15 && i < 30 ? 0.9 : 0.1
                                      let param = Math.Abs(vad + noise)
                                      let f = new Bernoulli(param > 1 ? 1 : param)
                                      select f).ToArray();
            // history operator we chatted about
            Uncertain<bool[]> history = data.USeq(N);

            // Inference computes a weighted bool[] object: effectively a histogram
            // The call to SampledInference needs to know (i) how many samples to take and how to compare bool[]
            Uncertain<bool[]> posterior = history.SampledInference(10000, new SequenceComparer<bool>());

            // now inspect by materializing a list
            List<Weighted<bool[]>> top5 = posterior
                .Support() // enumerate the histogram                
                .OrderByDescending(k => k.Probability) // sorted by probability
                .Take(5) // just top 5
                .ToList(); // produce list

            //var program = from bools in history
            //              let sum = bools.Select(Convert.ToInt32).Sum()
            //              from prior in new Gaussian(20, 0.01)
            //              where sum == (int) prior
            //              select bools;
            //Uncertain<bool[]> posterior1 = program.SampledInference(10000, new BoolArrayEqualityComparer());

            Func<bool[], bool[]> Intervalize = _ => _;

            var program = from bools in data.USeq(N)
                          select Intervalize(bools);

            // now inspect by materializing a list
            List < Weighted < bool[] >> top51 = posterior
                .Support() // enumerate the histogram                
                .OrderByDescending(k => k.Probability) // sorted by probability
                .Take(5) // just top 5
                .ToList(); // produce list


            // set breakpoint
            int x = 10;
        }
    }

    public static class StreamExtensions
    {
        public static IEnumerable<T[]> History<T>(this IEnumerable<T> source, int num)
        {
            return null;
        }
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

    public static class MyExtensions
    {
        public static Uncertain<T[]> USeq<T>(this IEnumerable<Uncertain<T>> source, int num)
        {
            Uncertain<T[]> output = source.Take(num).Aggregate<Uncertain<T>, Uncertain<FunctionalList<T>>, Uncertain<T[]>>(
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
                           select sample.Reverse().ToArray();
                });
            return output;
        }

        public static Uncertain<R[]> USeq2<T, R>(this Uncertain<T>[] source, Func<T[], R[]> selector)
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

    }

    internal class SequenceComparer<T> : IEqualityComparer<T[]> where T : IComparable<T>
    {
        public bool Equals(T[] x, T[] y)
        {
            if (object.ReferenceEquals(x, y))
                return true;

            if (x.Length != x.Length)
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

}
