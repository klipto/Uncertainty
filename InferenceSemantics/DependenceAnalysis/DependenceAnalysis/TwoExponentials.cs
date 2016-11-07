using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
//using Microsoft.Research.Uncertain.InferenceDebugger;
using Microsoft.Research.Uncertain.Inference;

namespace DependenceAnalysis
{
	public class TwoExponentials: Passert<double>
	{
		public TwoExponentials ()
		{

		}

		public Uncertain<double> UncertainProgram() {
			double v = 0.5;
			Uncertain<double> uv = (Uncertain<double>) v;

			Uncertain<double> x = new Exponential (2);
			Uncertain<double> y = new Exponential (3);

			var p = from a in x
					from b in y
					select a + b;
	
			var tt = (p  < 1);
			var interval = passert (tt, 0.9);
			return p;
		}


	}
}
