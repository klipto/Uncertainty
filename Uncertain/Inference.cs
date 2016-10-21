using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Research.Uncertain
{
    public class Inference<T> : Uncertain<T>
    {

        public Uncertain<T> Source { get; private set; }
        public readonly IEqualityComparer<T> comparer;

        public Inference(Uncertain<T> source)
        {
            this.Source = source;            
        }
        public Inference(Uncertain<T> source, IEqualityComparer<T> comparer)
        {
            this.Source = source;
            this.comparer = comparer;
        }

        public override IEnumerable<Weighted<T>> GetSupport()
        {
            throw new NotImplementedException();
        }

        public override void Accept(IUncertainVisitor visitor)
        {
            this.Source.Accept(visitor);
        }
    }
}
