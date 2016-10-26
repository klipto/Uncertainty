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
	public class WrongInferenceLocation
	{
		public WrongInferenceLocation ()
		{
		}

		public Uncertain<double> UncertainProgram () {

			Uncertain<double> w = new Gaussian (0, 1);
			Uncertain<double> v = new Gaussian (0, 1);

			var x = from a in w
				select 2 * a;

			var y = from b in v
				select 3 * b;

			var xx = x.SampledInference (10);
			var yy = y.SampledInference (50);

			var z = from a in xx
					from b in yy
					select a + b;

		
			//Uncertain<double, double> dd= new Uncertain<double, double>;
			return z;
		}
	}
}

