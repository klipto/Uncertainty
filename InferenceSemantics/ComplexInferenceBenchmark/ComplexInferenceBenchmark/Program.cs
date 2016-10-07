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
            var g1 = program1.SampledInference(100).Support().ToList();
            var g2 = program2.SampledInference(100).Support().ToList();
            var exp_enum = program3.SampledInference(100).Support().ToList();
            
        
			List<Tuple<double, double>> sample_values = new List<Tuple<double,double>> ();			
			foreach (var g in g1) 
            {
                foreach (var h in g2)
                {
                    sample_values.Add(Tuple.Create(g.Value + h.Value, g.Probability*h.Probability));  // these values are N(0,2)               
                }
			}
            List<Tuple<double, double>> sample_values1 = new List<Tuple<double, double>>();
            foreach (var e in exp_enum)
            {
                foreach (var f in sample_values)
                {
                    sample_values1.Add(Tuple.Create((e.Value + f.Item1),e.Probability*f.Item2));
                }
            }

            List<Tuple<double, double>> sample_values2 = new List<Tuple<double, double>>();
            foreach (var g in g1)
            {
                foreach (var s in sample_values1)
                {
                    sample_values2.Add(Tuple.Create(g.Value + s.Item1, g.Probability*s.Item2));
                }
            }

			double sum = 0.0;
			foreach (var v in sample_values2) {
				sum = sum + v.Item1;

			}
			double mean1 = sum / sample_values1.Count;
			return mean1;
		}

		public static double Inference2(Uncertain<double>program1, Uncertain<double> program2, Uncertain<double> program3) {

			var program = from p1 in program1.SampledInference(1000, null)
						  from p2 in program2.SampledInference(1000, null)
						  select (p1 + p2);

			//var enumprog = from p in program.SampledInference (1000, null)
			//			   from p3 in program3.SampledInference (1000, null)
			//		select (p + p3);

			var final_program = program.SampledInference(1000).Support ().ToList ();
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
			var program2 = new Gaussian (0, 1);
			var program3 = new Exponential(5);

			var t1= Inference1(program1, program2, program3);
			System.Console.WriteLine("mean1: " + t1);
			var t2=Inference2(program1, program2, program3);			
			System.Console.WriteLine(" mean2: " + t2);	
		}
	}
}