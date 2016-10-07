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
		// NEVER CALL THIS DEVIL, YOUR COMPUTER WILL DIE.
		public static double Inference1(Uncertain<double>program1, Uncertain<double> program2, Uncertain<double> program3) {
            var g1 = program1.SampledInference(1000).Support().ToList();
            var g2 = program2.SampledInference(1000).Support().ToList();
            var exp_enum = program3.SampledInference(1000).Support().ToList();
            
        
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
			double mean1 = sum / sample_values2.Count;
			return mean1;
		}

		public static double Inference2(Uncertain<double>program1, Uncertain<double> program2, Uncertain<double> program3, Uncertain<bool> flip) {

			var intermediate1 = from p1 in program1
						  from p2 in program2
						  from p3 in program3	
						  select (p1 + p2 + p3); // N(1, sqrt(11))

			var intermediate2 = from p1 in program1
							   from p2 in program2
					select p1 + p2; // N(0, sqrt(2))

			var intermediate3 = from i1 in intermediate1
							    from i2 in intermediate2
					select i1 + i2;

			var exponential1 = new Exponential (0.5);
			var exponential2 = new Exponential (0.2);

			var choice = flip.SampledInference (1).Support().ToList(); // flip a coin to choose between the two exponentials
		
			List<Weighted<double>> enumerate = new List<Weighted<double>> ();
			if (Convert.ToInt32(choice.ElementAt(0).Value) == 1) {
				Console.WriteLine (Convert.ToInt32(choice.ElementAt(0).Value));
				var final = from e in exponential1
							from i3 in intermediate3
						select e + i3;
				enumerate = final.SampledInference (1000).Support ().ToList();
			} else {
				Console.WriteLine (Convert.ToInt32(choice.ElementAt(0).Value));
				var final = from e in exponential2
							from i3 in intermediate3
						select e + i3;
				enumerate = final.SampledInference (1000).Support ().ToList();
			}

			var mean = enumerate.Select(i=>i.Value).Sum () / enumerate.Count;						
			return mean;
		}

		public static void Main (string[] args)
		{
			var program1 = new Gaussian (0, 1);
			var program2 = new Gaussian (0, 1);
			var program3 = new Gaussian (1, 3);
			var flip = new Flip (0.2);

			//var t1= Inference1(program1, program2, program3);
			//System.Console.WriteLine("mean1: " + t1);
			var t2=Inference2(program1, program2, program3, flip);			
			System.Console.WriteLine(" mean2: " + t2);	
		}
	}
}