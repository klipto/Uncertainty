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

namespace Microsoft.Research.Uncertain
{
    public class ForwardSampler<T> : IUncertainVisitor, ISampler<T>
    {
        internal object sample;
        protected readonly Uncertain<T> source;
        protected int generation;
        private IDictionary<object, Tuple<int, object>> cache;

        public ForwardSampler(Uncertain<T> source)
        {
            this.source = source;
            this.generation = 1;
            this.cache = new Dictionary<object, Tuple<int, object>>();
        }

        private IEnumerable<Weighted<T1>> GetEnumerator<T1>(Uncertain<T1> fromsource)
        {
            while (true)
            {
                this.generation++;
                fromsource.Accept(this);
                yield return (Weighted<T1>)this.sample;
            }
        }

        public IEnumerator<Weighted<T>> GetEnumerator()
        {
            return this.GetEnumerator(this.source).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void Visit<T1>(Where<T1> where)
        {
            // rejection sampling! 
            foreach (var item in this.GetEnumerator<T1>(where.source))
            {
                if (where.Predicate(item.Value))
                {
                    this.sample = item;
                    break;
                }
            }
        }

        public void Visit<T1>(RandomPrimitive<T1> erp)
        {
            var sample = erp.Sample(this.generation);
            this.sample = new Weighted<T1>(sample);
        }

        public void Visit<TSource, TResult>(Select<TSource, TResult> select)
        {
            select.source.Accept(this);
            var a = (Weighted<TSource>)this.sample;
            var b = select.Projection(a.Value);
            this.sample = new Weighted<TResult>(b.Value, a.Probability * b.Probability);
        }

        public void Visit<TSource, TCollection, TResult>(SelectMany<TSource, TCollection, TResult> selectmany)
        {
            selectmany.source.Accept(this);
            var a = (Weighted<TSource>)this.sample;

            var b = selectmany.CollectionSelector(a.Value);
            b.Accept(this);
            var c = (Weighted<TCollection>)this.sample;

            var result = selectmany.ResultSelector(a.Value, c.Value);
            result.Probability *= (a.Probability * c.Probability);
            this.sample = result;
        }
    }
}
