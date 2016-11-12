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
	public class MainClass
	{
		public static void Main(string[] args)
		{	
			
			DependenceAnalyzer<double> analyzer = new DependenceAnalyzer<double> ();
	    	Correlation cr = new Correlation ();
			cr.UncertainProgram ().Accept(analyzer);

			var result = analyzer.spearmanCorrelationCalculator (analyzer.random_primitives);

			//var result1 = analyzer.pearsonCorrelationCalculator (analyzer.random_primitives);

			//TwoExponentials t1 = new TwoExponentials ();
			//var output = t1.UncertainProgram ();

	//		output.Accept(analyzer);
//			var result1 = analyzer.correlationCalculator(analyzer.random_primitives);
//
//			WrongInferenceLocation w = new WrongInferenceLocation();
//			w.UncertainProgram ();
//			bool result2 = analyzer.earlyInferenceDetector ();
		}
	}
}

