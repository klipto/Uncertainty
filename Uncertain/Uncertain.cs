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

    public static class Uncertain
    {
        public static Multinomial<T> CreateInstance<T>(IEnumerable<T> states)
        {
            return new Multinomial<T>(states);
        }

    }

    public abstract class Uncertain<T>
    {
        public Uncertain() {}

        public abstract IEnumerable<Weighted<T>> GetSupport();
        public IEnumerable<Weighted<T>> Support()
        {
            return this.GetSupport();
        }

        public abstract void Accept(IUncertainVisitor visitor);

        private static bool? CheckForNull(Uncertain<T> lhs, Uncertain<T> rhs)
        {
            // deal with null
            if ((lhs ?? Null<T>.Instance) is Null<T> && (rhs ?? Null<T>.Instance) is Null<T>)
                return true;
            if ((lhs ?? Null<T>.Instance) is Null<T>)
                return false;
            if ((rhs ?? Null<T>.Instance) is Null<T>)
                return false;

            return null;
        }

        public static Uncertain<bool> operator ==(Uncertain<T> lhs, Uncertain<T> rhs)
        {
            var arenull = CheckForNull(lhs, rhs);
            if (arenull.HasValue) return arenull.Value;

            var tmp = from a in lhs
                      from b in rhs
                      select EqualityComparer<T>.Default.Equals(a, b);
            return tmp;
        }

        public static Uncertain<bool> operator !=(Uncertain<T> lhs, Uncertain<T> rhs)
        {
            var arenull = CheckForNull(lhs, rhs);
            if (arenull.HasValue) return arenull.Value;

            return from a in lhs == rhs select !a;
        }

        public static Uncertain<bool> operator <(Uncertain<T> lhs, Uncertain<T> rhs)
        {
            var arenull = CheckForNull(lhs, rhs);
            if (arenull.HasValue) return arenull.Value;

            var tmp = from a in lhs
                      from b in rhs
                      select Comparer<T>.Default.Compare(a, b) < 0;
            return tmp;
        }

        public static Uncertain<bool> operator >(Uncertain<T> lhs, Uncertain<T> rhs)
        {
            var arenull = CheckForNull(lhs, rhs);
            if (arenull.HasValue) return arenull.Value;

            var tmp = from a in lhs
                      from b in rhs
                      select Comparer<T>.Default.Compare(a, b) > 0;
            return tmp;
        }

        public static Uncertain<bool> operator <=(Uncertain<T> lhs, Uncertain<T> rhs)
        {
            var arenull = CheckForNull(lhs, rhs);
            if (arenull.HasValue) return arenull.Value;

            var tmp = from a in lhs
                      from b in rhs
                      select Comparer<T>.Default.Compare(a, b) <= 0;
            return tmp;
        }

        public static Uncertain<bool> operator >=(Uncertain<T> lhs, Uncertain<T> rhs)
        {
            var arenull = CheckForNull(lhs, rhs);
            if (arenull.HasValue) return arenull.Value;

            var tmp = from a in lhs
                      from b in rhs
                      select Comparer<T>.Default.Compare(a, b) >= 0;
            return tmp;
        }

        public static implicit operator Uncertain<T>(T t)
        {
            return new Constant<T>(t);
        }
    }

    // internal class only used to work around
    // override of ==, which makes it difficult
    // to test for null
    internal class Null<T> : RandomPrimitive<T>
    {
        internal static Null<T> Instance { get; private set; }
        static Null()
        {
            Null<T>.Instance = new Null<T>();
        }
        private Null()
        {
        }
        public override T GetSample()
        {
            throw new NotImplementedException();
        }

        public override double Score(T t)
        {
            throw new NotImplementedException();
        }

        public override int GetStructuralHash()
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<Weighted<T>> GetSupport()
        {
            throw new NotImplementedException();
        }

        public override bool StructuralEquals(RandomPrimitive other)
        {
            throw new NotImplementedException();
        }
    }
}
