using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.InferenceDebugger;
using Microsoft.Research.Uncertain.Inference;

namespace ComplexInferenceBenchmark
{
	public class MainClass
	{
		public static Func<int, Uncertain<double>> F = (k1) =>
				from a in new Exponential(2.0).SampledInference(k1)
					select a;

		public static double getMean()
		{
			return 0.5;
		}

		public static void testExponential() {
			Debugger<double> doubleDebugger = new Debugger<double>(0.001, 75, 1000);

			var hyper = from k in ((TruncatedHyperParameterModel)doubleDebugger.hyperParameterModel).truncatedGeometric
				select Tuple.Create(k, ((TruncatedHyperParameterModel)doubleDebugger.hyperParameterModel).truncatedGeometric.Score(k));

			var n = doubleDebugger.DebugSampleSize((TruncatedHyperParameterModel)doubleDebugger.hyperParameterModel, F, getMean(), hyper);

			var sample_size = n.Item1;
			var samples = F (n.Item1).Support().ToList();
		}

		public static void Main (string[] args)
		{

			Uncertain<double> vvv = new Gaussian (0, 1);
			List<Weighted<double>> samp = new List<Weighted<double>> ();
			samp = vvv.SampledInference (100).Support ().ToList ();


			Simple s = new Simple ();


			Uncertain<double> v = new Gaussian (0, 1);
			var u = from num in (Uncertain<double>)2.0 
				    from vv in v
					select num + vv;


			var watch_exp_without_mi = System.Diagnostics.Stopwatch.StartNew();
			var samples  = F (1000);
			watch_exp_without_mi.Stop ();
			var time_without_mi = watch_exp_without_mi.ElapsedMilliseconds;


			Console.WriteLine ("without: " + time_without_mi);


			var watch_exp_with_mi = System.Diagnostics.Stopwatch.StartNew();
			testExponential ();
			watch_exp_with_mi.Stop ();
			var time_with_mi = watch_exp_with_mi.ElapsedMilliseconds;


			Console.WriteLine ("with: " + time_with_mi);
			//var samples = F (178);


		//	TestGeneric<double> tg = new TestGeneric<double> (5.5);
		//	Complex<double> mc = new Complex<double> ();
		//	mc.AddItem (tg);

//			IgnoreDependence0 ();
//			IgnoreDependence2 ();
//			var program1 = new Gaussian (0, 1);
//			var program2 = new Gaussian (0, 1);
//			var program3 = new Gaussian (1, 3);
//			var flip = new Flip (0.2);
//
//			var watch1 = System.Diagnostics.Stopwatch.StartNew ();
//			var t1= Inference1(program1, program2, program3, flip);
//			watch1.Stop ();
//			var elaspedTime1 = watch1.ElapsedMilliseconds;
//			System.Console.WriteLine("Inference at ERPs: " + t1.Item1 + " : " + t1.Item2 + " time: " + elaspedTime1);
//
//			var watch2 = System.Diagnostics.Stopwatch.StartNew ();
//			var t2=Inference2(program1, program2, program3, flip);			
//			watch2.Stop ();
//			var elaspedTime2 = watch2.ElapsedMilliseconds;
//
//			System.Console.WriteLine("Inference as far from ERP as possible: " + t2.Item1 + " : " + t2.Item2 + " time: " + elaspedTime2);	
//			System.Console.WriteLine ("True mean and variance when Flip = 0 are 6, 42" );
//
//			Simple s = new Simple (new Gaussian(0,1), new Gaussian(1, 2));
//			//s.learnTemperature ();
//			double m1 = s.finalMean1 ();
//			double m2 = s.finalMean2 ();
		}
	}
}

