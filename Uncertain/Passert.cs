using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Research.Uncertain
{
	public class Passert<T>
	{
		public Passert ()
		{
		}

		public virtual Tuple<double, double> passert (params Uncertain<T> [] v) {
			return Tuple.Create(0.0, 0.0);
		}
	}
}

