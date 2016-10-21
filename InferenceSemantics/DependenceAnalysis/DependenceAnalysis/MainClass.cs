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
	public class MainClass
	{
		public static void Main(string[] args)
		{		
			DependenceAnalyzer<double> analyzer = new DependenceAnalyzer<double> ();
			TwoExponentials t1 = new TwoExponentials ();
			t1.UncertainProgram ().Accept(analyzer);
			var result = DependenceAnalyzer<double>.correlationCalculator(analyzer.random_primitives);
		}
	}
}

