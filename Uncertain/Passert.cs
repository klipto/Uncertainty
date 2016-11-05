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

		public virtual bool passert (Uncertain<bool> condition, double probability) {

			if (condition.Pr (probability))
				return true;
			else
				return false;
		}

		public virtual bool passert (params Uncertain<T> [] v) {
			return false;
		}
	}
}

