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

        public override string ToString()
        {
            return String.Format("<{0} {1} {2}>", this.Location, this.Sample, this.Reused);
        }
    }

    internal class TraceEntryComparer : IEqualityComparer<IList<TraceEntry>>
    {
        public bool Equals(IList<TraceEntry> x, IList<TraceEntry> y)
        {
            if (x.Count != y.Count)
            {
                return false;
            }

            for (int i = 0; i < x.Count; i++)
            {
                var a = x[i];
                var b = y[i];
                if (a.Location != b.Location)
                    return false;
                if (StructuralComparisons.StructuralEqualityComparer.Equals(a.Erp, b.Erp) == false)
                    return false;
            }

            return true;
        }

        public int GetHashCode(IList<TraceEntry> obj)
        {
            var hashcode = 0;
            foreach(var e in obj)
            {
                var a = StructuralComparisons.StructuralEqualityComparer.GetHashCode(e.Location);
                var b = StructuralComparisons.StructuralEqualityComparer.GetHashCode(e.Erp);
                hashcode ^= a * b;
            }
            return hashcode;
        }
    }

    internal class ObjectArrayComparer : IEqualityComparer<object[]>
    {
        public bool Equals(object[] x, object[] y)
        {
            if (x.Length != y.Length)
                return false;

            for(int i = 0; i < x.Length; i++)
            {
                if (!x[i].Equals(y[i]))
                    return false;
            }

            return true;
        }

        public int GetHashCode(object[] obj)
        {
            var hashcode = obj.Length;
            foreach (var e in obj)
            {
                var a = e.GetHashCode();
                hashcode ^= a;
            }
            return hashcode;
        }
    }

    internal class SingleSampler : IUncertainVisitor
    {
        internal object Sample { get; private set; }

        public void Visit<T>(Where<T> where)
        {
            throw new NotImplementedException();
        }

        public void Visit<T>(RandomPrimitive<T> erp)
        {
            this.Sample = erp.Sample(-1);
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

    public class MarkovChainMonteCarloSampler<T> : IUncertainVisitor, ISampler<T>
    {
        private readonly Uncertain<T> source;
        private List<TraceEntry> trace, oldTrace;
        private object sample;
        protected int generation;
        private Address stack;
        private readonly IDictionary<object[], int> cache;

        public MarkovChainMonteCarloSampler(Uncertain<T> source)
        {
            this.source = source;
            this.generation = 1;
            this.stack = Tuple.Create(0, 0, 0);
            this.trace = new List<TraceEntry>();
            this.oldTrace = new List<TraceEntry>();

            this.cache = new Dictionary<object[], int>(new ObjectArrayComparer());
        }

        private static void Swap<T1>(ref T1 lhs, ref T1 rhs)
        {
            T1 temp;
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

        private static double Accept(IList<TraceEntry> trace, IList<TraceEntry> oldTrace, int regenFrom)
        {
            double traceScore = 0, oldScore = 0, fw = -Math.Log(oldTrace.Count), bw = -Math.Log(trace.Count);
            
            for(int i = 0; i < Math.Max(trace.Count, oldTrace.Count); i++)
            {
                if (i < trace.Count)
                {
                    traceScore += trace[i].Score;
                    fw += i >= regenFrom && trace[i].Reused == false ? trace[i].Score : 0;
                }
                
                if (i < oldTrace.Count)
                {
                    oldScore += oldTrace[i].Score;
                    var nc = FindChoice(trace, oldTrace[i].Location, oldTrace[i].Erp);
                    if (nc.HasValue && !nc.Value.Reused)
                    {
                        bw += oldTrace[i].Score;
                    }
                }
            }
            /// Used to easily explain above code
            //var traceScore = trace.Select(e => e.Score).Sum();   // note could read from this.sample
            //var oldScore = oldTrace.Select(e => e.Score).Sum();  // note could read from this.oldSample

            //var fw = -Math.Log(oldTrace.Count());
            //fw += trace.Skip(regenFrom).Where(e => e.Reused == false).Select(e => e.Score).Sum();

            //var bw = -Math.Log(trace.Count());
            //foreach(var e in oldTrace)
            //{
            //    var nc = FindChoice(trace, e.Location, e.Erp);
            //    if (nc.HasValue && !nc.Value.Reused)
            //    {
            //        bw += e.Score;
            //   }
            //}

            return Math.Min(0, traceScore - oldScore + bw - fw);
        }

        private IEnumerable<Weighted<T1>> GetEnumerator<T1>(Uncertain<T1> uncertain)
        {
            var regenFrom = 0;

            RandomPrimitive sampled = null;
            var initstack = this.stack;
            var sampler = new SingleSampler();

            do
            {
                this.stack = initstack;

                // Run the program.
                uncertain.Accept(this);

                //var samples = (from e in this.trace select e.Sample).ToArray();
                //int count;
                //if (!this.cache.TryGetValue(samples, out count))
                //{
                //    count = 0;
                //} 
                //else
                //{
                //    count = count;
                //}
                //this.cache[samples] = count + 1;

                // TODO: should we return 0 mass samples?
                //       0 IS a reasonable weight if all 
                //       one wants to do is know about a
                //       posterior - need to compute the score
                //       from the Weighted<T1> object rather
                //       than the trace.
                var returnval = (Weighted<T1>)this.sample;

                //if (returnval.Probability > 0)
                //    yield return returnval;
                yield return returnval;

                var roll = Extensions.NextRandom();
                var acceptance = Accept(trace, oldTrace, regenFrom);
                if (this.generation > 1 && !(Math.Log(roll) < acceptance))
                {
                    // rollback proposal
                    trace.Clear();
                    trace.AddRange(oldTrace);                    
                }

                Swap(ref trace, ref oldTrace);
                if (oldTrace.Count > 0)
                {
                    // A programmer induced dependence implies the trace can have
                    // more than one copy of any given Random Primitive
                    // only sample from the set of distinct RandomPrimitives
                    // to avoid oversampling any particular RandomPrimitive.
                    //var dict = new Dictionary<RandomPrimitive, IList<int>>();
                    //var pos = 0;
                    //foreach(var e in oldTrace)
                    //{
                    //    IList<int> lst;
                    //    if (! dict.TryGetValue(e.Erp, out lst))
                    //    {
                    //        lst = new List<int>();
                    //        dict[e.Erp] = lst;
                    //    }
                    //    lst.Add(pos++);
                    //}
                    //regenFrom = (int)Math.Floor(Extensions.NextRandom() * dict.Keys.Count);
                    //sampled = dict.Keys.ElementAt(regenFrom);
                    var distinct = oldTrace.Select(e => e.Erp).Distinct().ToList();
                    regenFrom = (int)Math.Floor(Extensions.NextRandom() * distinct.Count);
                    sampled = distinct[regenFrom];                    
                    // force regeneration of this random primitive
                    // note we reset this to false in the 
                    // Visit(RandomPrimitive) method
                    sampled.ForceRegen = true;


                    sampled.Accept(sampler);
                    //var key = (from e in this.trace select e.Sample).ToArray();
                }                
                trace.Clear();

                this.generation++;
            } while (true);
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
                var tmp = sample.Value;
                if (where.Predicate(sample.Value))
                {                    
                    this.sample = sample;
                    return;
                }
            }
        }

        public void Visit<T1>(RandomPrimitive<T1> erp)
        {
            if (erp is Constant<T1>)
            {
                // do not add to trace as we do not want to 
                // resample constants
                this.sample = new Weighted<T1>(erp.Sample(-1));
                return;
            }

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
                // keep from forcing a regeneration of this RandomPrimitive
                // either on this iteration (i.e., because of a programmer
                // induced dependence) or in future iterations.
                ((RandomPrimitive)erp).ForceRegen = false;
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
