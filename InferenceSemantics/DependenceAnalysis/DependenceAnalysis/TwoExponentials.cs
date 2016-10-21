using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.InferenceDebugger;
using Microsoft.Research.Uncertain.Inference;

namespace DependenceAnalysis
{
	public class TwoExponentials
	{
		public TwoExponentials ()
		{
		}

		public Uncertain<double> UncertainProgram() {
			Uncertain<double> x = new Exponential (2);
			Uncertain<double> y = new Exponential (3);

			var p = from a in x
					from b in y
					select a + b;
			return p;
		}
	}
}

