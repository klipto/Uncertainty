using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.Uncertain
{
    using Address = Tuple<int, int, int>;

    internal struct TraceEntry
    {
        public Address Location { get; private set; }
        public object Sample { get; set; }
        public double Score { get; set; }
        public bool Reused { get; set; }
        public RandomPrimitive Erp { get; private set; }
        public TraceEntry(Address location, RandomPrimitive erp, object sample, double score, bool reuse = false)
        {
            this.Location = location;
            this.Erp = erp;
            this.Sample = sample;
            this.Score = score;
            this.Reused = reuse;
        }
    }

    internal class RandomPrimitiveSampler : IUncertainVisitor
    {
        public object Sample { get; private set; }
        public double Score { get; private set; }
        

        public void Visit<T>(Where<T> where)
        {
            throw new NotImplementedException();
        }

        public void Visit<T>(RandomPrimitive<T> erp)
        {
            ((RandomPrimitive)erp).ForceRegen = true;
            var sample = erp.Sample(-1);
            var score = erp.Score(sample);
            ((RandomPrimitive)erp).ForceRegen = false;
            this.Sample = sample;
            this.Score = score;
        }

        public void Visit<TSource, TResult>(Select<TSource, TResult> select)
        {
            throw new NotImplementedException();
        }

        public void Visit<TSource, TCollection, TResult>(SelectMany<TSource, TCollection, TResult> selectmany)
        {
            throw new NotImplementedException();
        }
    }


    internal class MarkovChainMonteCarloSampler<T> : IUncertainVisitor, ISampler<T>
    {
        private readonly Uncertain<T> source;
        private List<TraceEntry> trace, oldTrace;
        private object sample;
        protected int generation;
        private Address stack;

        internal MarkovChainMonteCarloSampler(Uncertain<T> source)
        {
            this.source = source;
            this.generation = 1;
            this.stack = Tuple.Create(0, 0, 0);
        }

        private static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp;
            temp = lhs;
            lhs = rhs;
            rhs = temp;
        }

        private static TraceEntry? FindChoice(IEnumerable<TraceEntry> trace, Address stack, RandomPrimitive erp)
        {
            foreach(var e in trace)
            {
                if (StructuralComparisons.StructuralEqualityComparer.Equals(e.Location, stack) &&
                    StructuralComparisons.StructuralEqualityComparer.Equals(e.Erp, erp))
                {
                    return e;
                }
            }

            return null;
        }

        private static double Accept(IEnumerable<TraceEntry> trace, IEnumerable<TraceEntry> oldTrace, int regenFrom)
        {
            var traceScore = trace.Select(e => e.Score).Sum();
            var oldScore = oldTrace.Select(e => e.Score).Sum();

            var fw = -Math.Log(oldTrace.Count());
            fw += trace.Skip(regenFrom).Where(e => e.Reused == false).Select(e => e.Score).Sum();

            var bw = -Math.Log(trace.Count());
            foreach(var e in oldTrace)
            {
                var nc = FindChoice(trace, e.Location, e.Erp);
                if (nc.HasValue && !nc.Value.Reused)
                {
                    bw += e.Score;
                }
            }

            return Math.Min(0, traceScore - oldScore + bw - fw);
        }

        public IEnumerable<Weighted<T1>> GetEnumerator<T1>(Uncertain<T1> source)
        {
            this.trace = new List<TraceEntry>();
            this.oldTrace = new List<TraceEntry>();
            var regenFrom = 0;
            var r = new Random();

            // run once to get a trace
            this.source.Accept(this);
            // put into oldTrace
            Swap(ref trace, ref oldTrace);

            while (true)
            {
                // reset stack
                this.stack = Tuple.Create(0, 0, 0);

                var pathscore = this.trace.Select(e => e.Score).Sum();
                if (Double.IsInfinity(pathscore))
                {
                    continue;
                }

                var roll = r.NextDouble();
                var acceptance = Accept(trace, oldTrace, regenFrom);
                if (this.generation == 1 || !(Math.Log(roll) < acceptance))
                {
                    // rollback or init
                    trace.AddRange(oldTrace);
                }

                // yield sample
                yield return (Weighted<T1>)this.sample;

                Swap(ref trace, ref oldTrace);
                regenFrom = (int)Math.Floor(r.NextDouble() * trace.Count);
                var erp = trace[regenFrom].Erp;
                trace.Clear();

                ((RandomPrimitive)erp).ForceRegen = true;
                this.source.Accept(this);
                ((RandomPrimitive)erp).ForceRegen = false;

                this.generation++;
            }
        }

        public IEnumerator<Weighted<T>> GetEnumerator()
        {
            return this.GetEnumerator(this.source).GetEnumerator();
        }

        public void Visit<T1>(Where<T1> where)
        {
            foreach(var sample in this.GetEnumerator<T1>(where.source))
            {
                if (where.Predicate(sample.Value))
                {
                    this.sample = sample;
                    return;
                }
            }
        }

        public void Visit<T1>(RandomPrimitive<T1> erp)
        {
            var prev = FindChoice(this.oldTrace, this.stack, erp);
            var reuse = !(prev.HasValue == false | ((RandomPrimitive)erp).ForceRegen);
            var sample = reuse ? (T1) prev.Value.Sample : erp.Sample(this.generation);
            this.sample = new Weighted<T1>(sample);
            var score = Math.Log(erp.Score(sample));
            var entry = new TraceEntry(this.stack, erp, sample, score);
            entry.Reused = reuse;
            this.trace.Add(entry);
        }

        public void Visit<TSource, TResult>(Select<TSource, TResult> select)
        {
            this.stack = Tuple.Create(stack.Item1 + 1, stack.Item2, stack.Item3);
            select.source.Accept(this);
            var a = (Weighted<TSource>)this.sample;
            var b = select.Projection(a.Value);
            this.sample = new Weighted<TResult>(b.Value, a.Probability * b.Probability);
        }

        public void Visit<TSource, TCollection, TResult>(SelectMany<TSource, TCollection, TResult> selectmany)
        {
            this.stack = Tuple.Create(stack.Item1, stack.Item2 + 1, stack.Item3);
            selectmany.source.Accept(this);
            var a = (Weighted<TSource>)this.sample;

            var b = selectmany.CollectionSelector(a.Value);

            this.stack = Tuple.Create(stack.Item1, stack.Item2 + 1, stack.Item3);
            b.Accept(this);
            var c = (Weighted<TCollection>)this.sample;

            var result = selectmany.ResultSelector(a.Value, c.Value);
            result.Probability *= (a.Probability * c.Probability);
            this.sample = result;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
