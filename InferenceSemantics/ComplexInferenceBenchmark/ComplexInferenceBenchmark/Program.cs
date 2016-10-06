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
	class MainClass
	{
		public static double Inference1(Uncertain<double>program1, Uncertain<double> program2, Uncertain<double> program3) {
			var exp_enum = program2.SampledInference(1000).Support().ToList();
			List<double> final_sample_values = new List<double> ();
			var g1 = program1.SampledInference (1000).Support ().ToList ();
			foreach (var g in g1) {
				foreach (var e in exp_enum) {
					final_sample_values.Add (g.Value + e.Value);
				}
			}
			double sum = 0.0;
			foreach (var v in final_sample_values) {
				sum = sum + v;

			}
			double mean1 = sum / final_sample_values.Count;
			return mean1;
		}

		public static double Inference2(Uncertain<double>program1, Uncertain<double> program2, Uncertain<double> program3) {

			var program = from p1 in program1.SampledInference(1000, null)
						  from p2 in program2.SampledInference(1000, null)
						  select (p1 + p2);

			//var enumprog = from p in program.SampledInference (1000, null)
			//			   from p3 in program3.SampledInference (1000, null)
			//		select (p + p3);

			var final_program = program.SampledInference (1000).Support ().ToList ();
			double sum = 0.0;
			foreach (var v in final_program) {
				sum = sum + v.Value;
			}
			double mean2 = sum / (double)final_program.Count;
			return mean2;
		}

		public static void Main (string[] args)
		{
			var program1 = new Gaussian (0, 1);
			var program2 = new Exponential (5);
			var program3 = new Uniform<double> (0,1);

			var t1= Inference1(program1, program2, program3);
			System.Console.WriteLine("mean1: " + t1);
			var t2=Inference2(program1, program2, program3);			
			System.Console.WriteLine(" mean2: " + t2);	

		}


	}
}