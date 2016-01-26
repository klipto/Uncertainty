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
            this.trace = new List<TraceEntry>();
            this.oldTrace = new List<TraceEntry>();
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

        private IEnumerable<Weighted<T1>> GetEnumerator<T1>(Uncertain<T1> uncertain)
        {
            var regenFrom = 0;
            var r = new Random();
            var initstack = this.stack;

            // run once to get a trace
            uncertain.Accept(this);
            
            // put into oldTrace
            if (oldTrace.Count == 0)
                Swap(ref trace, ref oldTrace);
            
            while (true)
            {
                this.generation++;

                // reset stack
                this.stack = initstack;

                var roll = r.NextDouble();
                var acceptance = Accept(trace, oldTrace, regenFrom);
                if (this.generation == 2 /* init */ || !(Math.Log(roll) < acceptance))
                {
                    // rollback proposal
                    trace.Clear();
                    trace.AddRange(oldTrace);
                }

                // yield sample
                yield return (Weighted<T1>)this.sample;

                Swap(ref trace, ref oldTrace);
                regenFrom = (int)Math.Floor(r.NextDouble() * oldTrace.Count);
                var erp = oldTrace[regenFrom].Erp;
                trace.Clear();

                ((RandomPrimitive)erp).ForceRegen = true;
                uncertain.Accept(this);
                ((RandomPrimitive)erp).ForceRegen = false;                
            }
        }

        public IEnumerator<Weighted<T>> GetEnumerator()
        {
            return this.GetEnumerator(this.source).GetEnumerator();
        }

        public void Visit<T1>(Where<T1> where)
        {
            this.stack = Tuple.Create(stack.Item1, stack.Item2, stack.Item3 + 1);
            foreach (var sample in this.GetEnumerator<T1>(where.source))
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
            var reuse = prev.HasValue && ((RandomPrimitive)prev.Value.Erp).ForceRegen == false;
            if (reuse)
            {
                var entry = prev.Value;
                this.sample = new Weighted<T1>((T1)entry.Sample);
                entry.Reused = true;
                this.trace.Add(entry);
            }
            else
            {
                var sample = erp.Sample(this.generation);
                var score = Math.Log(erp.Score(sample));
                this.sample = new Weighted<T1>(sample);
                var entry = new TraceEntry(this.stack, erp, sample, score, false);
                this.trace.Add(entry);
            }
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
