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

		public static Tuple<double,double> Inference1(Uncertain<double>program1, Uncertain<double> program2, Uncertain<double> program3, Uncertain<bool> flip) {
            var g1 = program1.SampledInference(10).Support().ToList ();
            var g2 = program2.SampledInference(10).Support().ToList ();
			var g3 = program3.SampledInference(10).Support().ToList ();       
        
			List<Tuple<double, double>> intermediate1 = new List<Tuple<double,double>> ();			
			foreach (var g in g1) 
            {
                foreach (var h in g2)
                {
					foreach (var j in g3) 
					{
						intermediate1.Add (Tuple.Create (g.Value + h.Value + j.Value, g.Probability * h.Probability* j.Probability));     
					}
                }
			}

			List<Tuple<double, double>> intermediate2 = new List<Tuple<double, double>>();

			foreach (var g in g1) 
			{
				foreach (var h in g2)
				{
					intermediate2.Add (Tuple.Create (g.Value + h.Value , g.Probability * h.Probability));     
				}
			}

			List<Tuple<double, double>> intermediate3 = new List<Tuple<double, double>> ();

			foreach (var i1 in intermediate1) {
				foreach (var i2 in intermediate2) {
					intermediate3.Add (Tuple.Create(i1.Item1 + i2.Item1, i1.Item2 * i2.Item2));
				}
			}

			var exponential1 = new Exponential (0.5).SampledInference(10).Support().ToList();
			var exponential2 = new Exponential (0.2).SampledInference(10).Support().ToList();

			var choice = flip.SampledInference (1).Support().ToList(); // flip a coin to choose between the two exponentials

			List<Tuple<double,double>> enumerate = new List<Tuple<double, double>> ();
			if (Convert.ToInt32(choice.ElementAt(0).Value) == 1) {
				Console.WriteLine ("Flip value: " +  Convert.ToInt32(choice.ElementAt(0).Value));
				foreach (var e1 in exponential1) {
					foreach (var i3 in intermediate3) {
						enumerate.Add (Tuple.Create(e1.Value+i3.Item1, e1.Probability* i3.Item2));
					}
				}

			} else {
				Console.WriteLine ("Flip value: " + Convert.ToInt32(choice.ElementAt(0).Value));
				foreach (var e1 in exponential2) {
					foreach (var i3 in intermediate3) {
						enumerate.Add (Tuple.Create(e1.Value+i3.Item1, e1.Probability* i3.Item2));
					}
				}
			}

			var mean = enumerate.Select(i=>i.Item1 * i.Item2).Sum ();			
			var variance = enumerate.Select(i=>i.Item1*i.Item1*i.Item2).Sum()-(mean*mean);
			return Tuple.Create(mean,variance);
		}

		public static Tuple<double,double> Inference2(Uncertain<double>program1, Uncertain<double> program2, Uncertain<double> program3, Uncertain<bool> flip) {

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
				Console.WriteLine ("Flip value: " + Convert.ToInt32(choice.ElementAt(0).Value));
				var final = from e in exponential1
							from i3 in intermediate3
						select e + i3;
				enumerate = final.SampledInference (1000).Support ().ToList();
			} else {
				Console.WriteLine ("Flip value: " + Convert.ToInt32(choice.ElementAt(0).Value));
				var final = from e in exponential2
							from i3 in intermediate3
						select e + i3;
				enumerate = final.SampledInference (1000).Support ().ToList();
			}

			var mean = enumerate.Select (i => i.Value * i.Probability).Sum ();
			var variance = enumerate.Select (i => i.Value*i.Value*i.Probability).Sum () - (mean* mean);
			return Tuple.Create(mean, variance);
		}

		public static void Main (string[] args)
		{
			var program1 = new Gaussian (0, 1);
			var program2 = new Gaussian (0, 1);
			var program3 = new Gaussian (1, 3);
			var flip = new Flip (0.2);

			var watch1 = System.Diagnostics.Stopwatch.StartNew ();
			var t1= Inference1(program1, program2, program3, flip);
			watch1.Stop ();
			var elaspedTime1 = watch1.ElapsedMilliseconds;
			System.Console.WriteLine("Inference at ERPs: " + t1.Item1 + " : " + t1.Item2 + " time: " + elaspedTime1);

			var watch2 = System.Diagnostics.Stopwatch.StartNew ();
			var t2=Inference2(program1, program2, program3, flip);			
			watch2.Stop ();
			var elaspedTime2 = watch2.ElapsedMilliseconds;
			System.Console.WriteLine("Inference as far from ERP as possible: " + t2.Item1 + " : " + t2.Item2 + " time: " + elaspedTime2);	

			System.Console.WriteLine ("True mean and variance when Flip = 0 are 6, 42" );
		}
	}
}