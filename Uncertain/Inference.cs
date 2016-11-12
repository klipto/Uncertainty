using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Research.Uncertain
{
    public class Inference<T> : Uncertain<T>
    {

        public Uncertain<T> source { get; private set; }
		public int sample_size { get; private set;}
	
		public List<int> inference_dependencies{ get; private set;}
	
        public readonly IEqualityComparer<T> comparer;

        public Inference(Uncertain<T> source, int size)
        {
            this.source = source;            
			this.sample_size = size;
			this.inference_dependencies = new List<int> ();
        }
        public Inference(Uncertain<T> source, IEqualityComparer<T> comparer, int size)
        {
            this.source = source;
            this.comparer = comparer;
			this.sample_size = size;
			this.inference_dependencies = new List<int> ();
        }

        public override IEnumerable<Weighted<T>> GetSupport()
        {
            throw new NotImplementedException();
        }

        public override void Accept(IUncertainVisitor visitor)
        {
			visitor.Visit(this);
        }
	
    }
}
