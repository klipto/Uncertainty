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
	public class Test
	{
		public Test ()
		{
		}

		public void tttest (){
			
			var v = new Gaussian (0, 1);
			var s = v.SampledInference (10);
			var t = s.Support();
			var y = t.ToList();
		}
	}
}

