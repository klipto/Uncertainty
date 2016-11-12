//using System;
//using System.Collections.Generic;
//using System.Diagnostics.Contracts;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//
//using Microsoft.Research.Uncertain;
//using Microsoft.Research.Uncertain.InferenceDebugger;
//using Microsoft.Research.Uncertain.Inference;
//		
//namespace ComplexInferenceBenchmark
//{
//	class Complex<T>
//	{
//		public List<TestGeneric<T>> l;
//
//		public Complex() {
//			l = new List<TestGeneric<T>> ();
//		}
//
//		public void AddItem<T>(TestGeneric<T> t) {
//			l.Add ((TestGeneric<T> )t);
//		}
//
//		public static Tuple<double,double> Inference1(Uncertain<double>program1, Uncertain<double> program2, Uncertain<double> program3, Uncertain<bool> flip) {
//        	var g1 = program1.SampledInference(1000).Support().ToList ();
//            var g2 = program2.SampledInference(1000).Support().ToList ();
//			var g3 = program3.SampledInference(1000).Support().ToList ();       
//			        
//			List<Tuple<double, double>> intermediate1 = new List<Tuple<double,double>> ();			
//				
//			// adding the elements per index
//			for(int x=0;x< g1.Count;x++) {
//				intermediate1.Add (Tuple.Create (g1.ElementAt(x).Value + g2.ElementAt(x).Value + g3.ElementAt(x).Value, g1.ElementAt(x).Probability * g2.ElementAt(x).Probability * 
//				                                 g3.ElementAt(x).Probability));     
//			}
//
//			List<Tuple<double, double>> intermediate2 = new List<Tuple<double, double>>();
//
//			for (int x=0;x<g1.Count;x++) {
//				intermediate2.Add (Tuple.Create (g1.ElementAt(x).Value + g2.ElementAt(x).Value , g1.ElementAt(x).Probability * g2.ElementAt(x).Probability));     
//			}
//
//			List<Tuple<double, double>> intermediate3 = new List<Tuple<double, double>> ();
//
//			for (int x=0;x<intermediate1.Count;x++) {
//				intermediate3.Add (Tuple.Create(intermediate1.ElementAt(x).Item1 + intermediate2.ElementAt(x).Item1, intermediate1.ElementAt(x).Item2 * intermediate2.ElementAt(x).Item2));
//			}
//
//			var exponential1 = new Exponential (0.5).SampledInference(1000).Support().ToList();
//			var exponential2 = new Exponential (0.2).SampledInference(1000).Support().ToList();
//
//			var choice = flip.SampledInference (1).Support().ToList(); // flip a coin to choose between the two exponentials
//
//			List<Tuple<double,double>> enumerate = new List<Tuple<double, double>> ();
//
//			if (Convert.ToInt32(choice.ElementAt(0).Value) == 1) {
//				Console.WriteLine ("Flip value: " +  Convert.ToInt32(choice.ElementAt(0).Value));
//				for(int x=0;x<exponential1.Count;x++) {
//					enumerate.Add (Tuple.Create(exponential1.ElementAt(x).Value+intermediate3.ElementAt(x).Item1, 
//					                            exponential1.ElementAt(x).Probability * intermediate3.ElementAt(x).Item2));
//				}	
//			} 
//
//			else {
//				Console.WriteLine ("Flip value: " + Convert.ToInt32(choice.ElementAt(0).Value));
//				for(int x=0;x<exponential2.Count;x++) {
//					enumerate.Add (Tuple.Create(exponential2.ElementAt(x).Value+intermediate3.ElementAt(x).Item1, 
//					                            exponential2.ElementAt(x).Probability * intermediate3.ElementAt(x).Item2));
//				}
//			}
//
//			var mean = enumerate.Select(i=>i.Item1).Sum ()/enumerate.Count;			
//			var variance = enumerate.Select(i=>(i.Item1-mean)*(i.Item1-mean)).Sum()/enumerate.Count;
//			return Tuple.Create(mean,variance);
//		}
//
//		public static Tuple<double,double> Inference2(Uncertain<double>program1, Uncertain<double> program2, Uncertain<double> program3, Uncertain<bool> flip) {
//
//			var intermediate1 = from p1 in program1
//						  from p2 in program2
//						  from p3 in program3	
//						  select (p1 + p2 + p3); // N(1, sqrt(11))
//
//			var intermediate2 = from p1 in program1
//							   from p2 in program2
//					select p1 + p2; // N(0, sqrt(2))
//
//			var intermediate3 = from i1 in intermediate1
//							    from i2 in intermediate2
//					select i1 + i2;
//
//			var exponential1 = new Exponential (0.5);
//			var exponential2 = new Exponential (0.2);
//
//			var choice = flip.SampledInference (1).Support().ToList(); // flip a coin to choose between the two exponentials
//		
//			List<Weighted<double>> enumerate = new List<Weighted<double>> ();
//			if (Convert.ToInt32(choice.ElementAt(0).Value) == 1) {
//				Console.WriteLine ("Flip value: " + Convert.ToInt32(choice.ElementAt(0).Value));
//				var final = from e in exponential1
//							from i3 in intermediate3
//						select e + i3;
//				enumerate = final.SampledInference (1000).Support ().ToList();
//			} else {
//				Console.WriteLine ("Flip value: " + Convert.ToInt32(choice.ElementAt(0).Value));
//				var final = from e in exponential2
//							from i3 in intermediate3
//						select e + i3;
//				enumerate = final.SampledInference (1000).Support ().ToList();
//			}
//
//			var mean = enumerate.Select (i => i.Value * i.Probability).Sum ();
//			var variance = enumerate.Select (i => i.Value*i.Value*i.Probability).Sum () - (mean* mean);
//			return Tuple.Create(mean, variance);
//
//
//		}
//
//		public static void IgnoreDependence0 ()
//		{
//			// We consider a probabilistic program that studies the growth rate in a cyanobacteria called "Oscillatoria agardhii". 
//			// http://plankt.oxfordjournals.org/content/7/4/487.abstract
//			// The paper says that the growth rate depends on temperature and light intensity.
//			// They found an equation to represent the relationship between these three factors and showed that it was a continuous function. 
//			// let us assume for now that the light intensity and temperature values have identical standard normal distributions. 
//
//			Gaussian temperature = new Gaussian(0,1); 
//			Gaussian light_intensity = new Gaussian(0,1); 
//
//			double a = 0.0022;
//			Uncertain<double> b = -0.012;
//			Uncertain<double> c = 0.009;
//
//			// usual way to do it without considering dependence between t and l
//			Uncertain<double> growth_rate_given_t_i = from t in temperature
//													  from li in light_intensity
//					select (0.0022 * t -0.012) * li / (li + ((0.0022 * t -0.012) / 0.009));
//
//			var rate = growth_rate_given_t_i.ExpectedValue();
//		
//			// right way of doing it considering the dependence between temperature and light. Light is independent, temperature is proportional to light.
//			// We don't know the exact relationship, but consider that the constant of proportionality is 0.5.
//			Uncertain<double> temperature_given_intensity = from intensity in light_intensity
//				select intensity/2;
//		
//			Uncertain<double> growth_rate_given_t_i_correct = from li in light_intensity
//															  from t_given_li in temperature_given_intensity
//					select (0.0022 * t_given_li  -0.012) * li / (li + ((0.0022 * t_given_li  -0.012) / 0.009));
//
//			var correct_rate = growth_rate_given_t_i_correct.ExpectedValue();
//
//		}
//
//		public static void IgnoreDependence1 ()
//		{
//
//			Gaussian temperature = new Gaussian (2, 3);
//			Gaussian humidity = new Gaussian (2, 3);
//
//
//
//			double[] temperature_data = new double[5];
//			temperature_data[0]=(32.3);
//			temperature_data[1]=(57.9);
//			temperature_data[2]=(89.5);
//			temperature_data[3]=(77.3);
//			temperature_data[4]=(110.7);
//			var temperatures = new FiniteEnumeration <double>(temperature_data);	
//
//			string[] colors = new string[5];
//			colors[0] = "red";
//			colors[1] = "green";
//			colors[2] = "blue";
//			colors[3] = "orange";
//			colors[4] = "pink";
//			var u_colors = new FiniteEnumeration<string> (colors).SampledInference(100).Support().ToList();
//
//		}
//
//		public static void IgnoreDependence2 ()
//		{
//
//			Gaussian gaussian1 = new Gaussian (2, 3);
//
//			// b will have distribution N(4, 6)
//			var gaussian2 = from g in gaussian1
//						    select 2*g;
//
//			// final will have distribution N(6, sqrt(45))
//			var final = from g1 in gaussian1
//				        from g2 in gaussian2
//					select g1 + g2;
//
//			var final1 = from g1 in gaussian1.SampledInference(1000, null)
//					     from g2 in gaussian2.SampledInference(100, null)
//					select g1 + g2;
//
//			var fi = final.ExpectedValueWithConfidence();
//			var fi1 = final1.ExpectedValueWithConfidence();
//
////			var x = new Flip(0.1);
////
////			var y = from a in new Flip (0.001)
////				    from b in new Flip (0.999)
////					where a | b 
////					select Convert.ToInt32 (a) + Convert.ToInt32 (b);
////
////			var inference = from yy in y.Inference ()
////				            from xx in x
////					        select Convert.ToInt32 (yy) + Convert.ToInt32 (xx);
////
////			var inf = inference.Inference ().Support ().ToList ();
////
////			var inference1 = from yy in y
////							 from xx in x
////					         select Convert.ToInt32 (yy) + Convert.ToInt32 (xx);
////
////			var inf1 = inference1.Inference ().Support ().ToList ();
//
//		}
//
//
//	}
//}