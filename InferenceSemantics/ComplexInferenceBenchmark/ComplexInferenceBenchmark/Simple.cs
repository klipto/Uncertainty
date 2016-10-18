using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using Microsoft.Research.Uncertain.InferenceDebugger;

namespace ComplexInferenceBenchmark
{
	public class Simple
	{
		private Gaussian gaussian1;
		private Gaussian gaussian2;

		public Simple (Gaussian g1, Gaussian g2)
		{
			gaussian1 = g1;
			gaussian2 = g2;
		}

		public void LearnTemperature() 
		{
			double[] temperature_data = new double[5];
			temperature_data[0]=(32.3);
			temperature_data[1]=(57.9);
			temperature_data[2]=(89.5);
			temperature_data[3]=(77.3);
			temperature_data[4]=(110.7);
			var temperatures = new FiniteEnumeration <double>(temperature_data);	
			ModelTemperature (temperatures);		
		}

		public void ModelTemperature (Uncertain<double> temperatures) {
			StatisticalInterpretation (temperatures);
			KalmanFilter (temperatures);
//			passert (predicted_temperature < 95), 0.9, 95%;
		}

		public void StatisticalInterpretation(Uncertain<double> temperatures) {
		}

		public void KalmanFilter(Uncertain<double> temperatures) {
		}

		public double finalMean1()
		{
			var x = this.gaussian1.SampledInference (1000).Support ().ToList ();
			var y = this.gaussian2.SampledInference (1000).Support ().ToList ();


			List<double> z = new List<double> ();

			for (int i=0; i < x.Count; i++) 
			{
				z.Add(x.ElementAt (i).Value + y.ElementAt (i).Value);
			}

			double count = z.Count();
			double final_mean1 = z.Sum()/count;
			double variance1 = z.Select (i => Math.Pow(i - final_mean1,2)).Sum () / z.Count ();
			Console.WriteLine(final_mean1);
			Console.WriteLine (variance1);
			return final_mean1;
		}

		public double finalMean2() {
			var z = from g in this.gaussian1
					from h in this.gaussian2 
					select g + h;
			var k = z.SampledInference (1000).Support ().ToList ();
			double final_mean2 = k.Select(i=>i.Value).Sum ()/k.Count();
			double variance2 = k.Select(i=>Math.Pow(i.Value-final_mean2,2)).Sum()/k.Count();
			Console.WriteLine(final_mean2);
			Console.WriteLine (variance2);
			return final_mean2;
		}
	}
}