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

            Correlation cr = new Correlation();

             //UList<double> vals = cr.UncertainProgram();             
            var vals = cr.UncertainProgram2();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            bool b = analyzer.earlyInferenceDetector();
            //vals.Accept(analyzer);
            timer.Stop();
            var time = timer.ElapsedMilliseconds;
            // var result = analyzer.correlations_in_list;

            /*var g = new Gaussian(0,1);
            double d1 = 15.5;
            double d2 = 16.7;
            double h = 45.7;
            var t = from gg in g
                    from dd in (Uncertain<double>)d1
                    select gg + dd;

            var tt1 = new Gaussian(d1, 1).SampledInference(10);
            var tt2 = new Gaussian(d2, 1).SampledInference(10);
            
            UList<Uncertain<double>> ll = new UList<Uncertain<double>>();
            ll.Add(tt1); ll.Add(tt2);
            
            ll.Accept(analyzer);*/

           /* var u = from gg in g
                    from hh in (Uncertain<double>)h
                    select gg + hh;

            var uu = new Gaussian(h, 1).SampledInference(10).Support().ToList();

            var v = from gg in g
                    from dd in (Uncertain<double>)d1
                    from hh in (Uncertain<double>)h
                    select Tuple.Create(gg+dd, gg + hh);

            var vv = v.SampledInference(10);
			
			vv.Accept(analyzer);*/

	    	

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

