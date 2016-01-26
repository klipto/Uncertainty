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
using System.Collections.Generic;

namespace Microsoft.Research.Uncertain
{
    public class SelectMany<TSource, TCollection, TResult> : Uncertain<TResult>
    {
        internal readonly Uncertain<TSource> source;
        internal Func<TSource, Uncertain<TCollection>> CollectionSelector { get; private set; }

        internal Func<TSource, TCollection, Weighted<TResult>> ResultSelector { get; private set; }
        internal SelectMany(
            Uncertain<TSource> source,
            Func<TSource, Uncertain<TCollection>> collectionSelector,
            Func<TSource, TCollection, Weighted<TResult>> resultSelector)
        {
            this.source = source;
            this.CollectionSelector = collectionSelector;
            this.ResultSelector = resultSelector;
        }

        protected override IEnumerable<Weighted<TResult>> GetSupport()
        {
            foreach (Weighted<TSource> a in this.source.Support())
            {
                foreach (Weighted<TCollection> b in this.CollectionSelector(a.Value).Support())
                {
                    Weighted<TResult> result = this.ResultSelector(a.Value, b.Value);
                    result.Probability *= (a.Probability * b.Probability);
                    yield return result;
                }
            }
        }

        internal override void Accept(IUncertainVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
