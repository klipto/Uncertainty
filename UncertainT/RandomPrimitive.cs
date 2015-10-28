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

namespace Microsoft.Research.Uncertain
{

    // TODDM: Note we require structural equatable for new 
    // not yet complete MCMC sampler.
    public interface RandomPrimitive : IStructuralEquatable 
    {
        bool ForceRegen { get; set; }
        void Accept(IUncertainVisitor visitor);
    }

    public abstract class RandomPrimitive<T> : Uncertain<T>, RandomPrimitive
    {
        private Tuple<int, T> cached;

        protected RandomPrimitive() 
        {
            ((RandomPrimitive)this).ForceRegen = false;
            this.cached = Tuple.Create(0, default(T));
        }

        protected abstract T GetSample();
        public T Sample(int generation)
        {
            if (this.cached.Item1 == generation && ((RandomPrimitive)this).ForceRegen == false)
                return this.cached.Item2;
            this.cached = Tuple.Create(generation, this.GetSample());
            return this.cached.Item2;
        }

        protected abstract bool StructuralEquals(RandomPrimitive other);
        protected abstract int GetStructuralHash();

        public abstract double Score(T t);

        internal override void Accept(IUncertainVisitor visitor)
        {
            visitor.Visit(this);
        }

        public bool Equals(object other, IEqualityComparer comparer)
        {            
            if (other == null)
                return false;

            if (object.ReferenceEquals(this, other))
                return true;

            if (other is RandomPrimitive<T>)
                return this.StructuralEquals(other as RandomPrimitive<T>);

            return false;
        }

        public int GetHashCode(IEqualityComparer comparer)
        {
            return this.GetStructuralHash();
        }

        void RandomPrimitive.Accept(IUncertainVisitor visitor)
        {
            this.Accept(visitor);
        }

        bool RandomPrimitive.ForceRegen { get; set; }
    }
}
