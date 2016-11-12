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
	class DependenceAnalyzer<T>: IUncertainVisitor
	{
		private int generation = 0;
		private object sample;

		public List<object> random_primitives{ get; private set;}
		public List<int> dependencies;

		public DependenceAnalyzer()
		{
			this.random_primitives = new List<object> ();
			this.dependencies = new List<int>();
		}

		public void Visit<T>(RandomPrimitive<T> erp)
		{
			this.sample = erp.Sample(this.generation++);
			random_primitives.Add(erp);

		}

		public void Visit<T>(UList<T> ulist)
		{
			foreach (var erp in ulist) {
				Console.WriteLine (erp.GetType().ToString());
				if (erp.GetType ().BaseType.ToString ().Contains ("RandomPrimitive")) {
					Visit ((RandomPrimitive<T>)erp);
				}  
			}
		}

		public void Visit<T> (Tuple<Uncertain<T>, Uncertain<T>> tuple) {
			Visit (tuple.Item1);
			Visit (tuple.Item2);
		}

		public void Visit<T> (Uncertain<T> v) {

			Visit (v);
			//Console.WriteLine (v.GetType().ToString());
		}

		public void Visit<T>(Where<T> where)
		{
			where.source.Accept(this);
		}

		public void Visit<TSource, TResult>(Select<TSource, TResult> select)
		{
			select.source.Accept (this);
			dependencies.Add (select.source.GetHashCode());
		}

		public void Visit<TSource, TCollection, TResult>(SelectMany<TSource, TCollection, TResult> selectmany)
		{

			selectmany.source.Accept(this);
			TSource a = (TSource) this.sample;

			Uncertain<TCollection> otherSampler = (selectmany.CollectionSelector.Compile())((TSource)this.sample);
			otherSampler.Accept(this);
			//dependencies.Add ();
		}

		public void Visit<T>(Inference<T> inference)
		{
			dependencies.Clear ();

			inference.source.Accept (this);
			inference.inference_dependencies.AddRange(this.dependencies);
		}

		public List<Tuple<Tuple<int, int>, double>> spearmanCorrelationCalculator(List<object> primitives)
		{
			// compute Spearman's correlation among the pairs in primitives by drawing 1000 sample values and finding the correlation
			List<Tuple<Tuple<int, int>, double>> correlation_coefficients = new List<Tuple<Tuple<int, int>, double>>();
			int sample_size = 1000;

			List<ParametersOfERPs> primitives_parameters = new List<ParametersOfERPs> ();

			foreach (var primitive in primitives) {
				if (primitive.GetType ().BaseType.ToString ().Contains ("RandomPrimitive")
					&& primitive.GetType ().BaseType.ToString ().Contains ("Double")) {

					var samples = ((RandomPrimitive<double>)primitive).SampledInference (sample_size).Support ().ToList ();
					Console.WriteLine ("count: " +samples.Count);
					Microsoft.Research.Uncertain.Inference.Extensions.inferences.RemoveAt (Microsoft.Research.Uncertain.Inference.Extensions.inferences.Count-1);
					samples.OrderBy (i=>i.Value);
					List<Tuple<double, double>> ranks = new List<Tuple<double, double>>();
					foreach (var sample in samples) {
						double rank = 1.0;
						ranks.Add (Tuple.Create(sample.Value, rank));
						rank = rank + 1.0;
					}
					primitives_parameters.Add (new ParametersOfERPs(((RandomPrimitive<double>)primitive).GetStructuralHash (), ranks));	
				}
			}

			foreach (var primitive1 in primitives_parameters) {
				foreach (var primitive2 in primitives_parameters) {
					double sum_of_differences_sq = 0.0;
					for (int x = 0; x<primitive1.ranks.Count; x++) {
						sum_of_differences_sq = sum_of_differences_sq + (Math.Pow (primitive1.ranks.ElementAt (x).Item2 - primitive2.ranks.ElementAt (x).Item2, 2));
					}
					double correlation = 1.0- ((6.0* sum_of_differences_sq)/(sample_size *(Math.Pow(sample_size,2.0)-1.0)));
					correlation_coefficients.Add (Tuple.Create (Tuple.Create (primitive1.ID, primitive2.ID), correlation));
				}
			}
			return correlation_coefficients;
		}

		public List<Tuple<Tuple<int, int>, double>> pearsonCorrelationCalculator(List<object> primitives)  
		{
			// compute Pearson's correlation among the pairs in primitives by drawing 1000 sample values and finding the correlation
			List<Tuple<Tuple<int, int>, double>> correlation_coefficients = new List<Tuple<Tuple<int, int>, double>>();
			int sample_size = 1000;

			List<ParametersOfERPs> primitives_parameters = new List<ParametersOfERPs> ();

			foreach (var primitive in primitives) {
				if (primitive.GetType ().BaseType.ToString ().Contains ("RandomPrimitive")
				    && primitive.GetType ().BaseType.ToString ().Contains ("Double")) {

					List<Weighted<double>> samples = ((RandomPrimitive<double>)primitive).SampledInference (sample_size).Support ().ToList ();
					Microsoft.Research.Uncertain.Inference.Extensions.inferences.RemoveAt (Microsoft.Research.Uncertain.Inference.Extensions.inferences.Count-1);

					double sum = samples.Select (i => Convert.ToDouble (i.Value)).Sum ();
					double square_sum = samples.Select (i => Convert.ToDouble (i.Value) * Convert.ToDouble (i.Value)).Aggregate ((a, b) => a + b);
					double stddev = Math.Sqrt (sample_size * square_sum - Math.Pow (sum, 2));
					primitives_parameters.Add (new ParametersOfERPs(((RandomPrimitive<double>)primitive).GetStructuralHash (), samples, sum, stddev));

				}

				if (primitive.GetType ().BaseType.ToString ().Contains ("RandomPrimitive")
				    && primitive.GetType ().BaseType.ToString ().Contains ("Int")) {

					List<Weighted<int>> samples = ((RandomPrimitive<int>)primitive).SampledInference (sample_size).Support ().ToList ();
					Microsoft.Research.Uncertain.Inference.Extensions.inferences.RemoveAt (Microsoft.Research.Uncertain.Inference.Extensions.inferences.Count-1);

					double sum = samples.Select (i => Convert.ToInt32 (i.Value)).Sum ();
					double square_sum = samples.Select (i => Convert.ToInt32 (i.Value) * Convert.ToInt32 (i.Value)).Aggregate ((a, b) => a + b);
					double stddev = Math.Sqrt (sample_size * square_sum - Math.Pow (sum, 2));
					primitives_parameters.Add (new ParametersOfERPs(((RandomPrimitive<int>)primitive).GetStructuralHash (), samples, sum, stddev));
				}
			}

			foreach (var primitive1 in primitives_parameters) {
				foreach (var primitive2 in primitives_parameters) {
					double product_sum = 0.0;
					double d_val1=1.0, d_val2=1.0;
					int i_val1=1, i_val2=1;
					for (int x=0; x<sample_size; x++) {
						if (primitive1.samples [x].Value.GetType ().ToString ().Contains ("Double")) {
							d_val1 = (double)(primitive1.samples [x].Value); 
						}
						if (primitive1.samples [x].Value.GetType ().ToString ().Contains ("Int")) {
							i_val1 = (int)(primitive1.samples [x].Value); 
						}
						if (primitive2.samples [x].Value.GetType ().ToString ().Contains ("Double")) {
							d_val2 = (double)(primitive2.samples [x].Value); 
						}
						if (primitive2.samples [x].Value.GetType ().ToString ().Contains ("Int")) {
							i_val2 = (int)(primitive2.samples [x].Value); 
						}
						product_sum = product_sum + (d_val1 * d_val2 * i_val1 * i_val2);
						d_val1 = 1.0;
						d_val2=1.0;
						i_val1 = 1;
						i_val2=1;
					}
					double correlation = ((sample_size * product_sum) - (primitive1.sum * primitive2.sum)) / (primitive1.stddev * primitive2.stddev);
					correlation_coefficients.Add (Tuple.Create (Tuple.Create (primitive1.ID, primitive2.ID), correlation));
				}
			}
			return correlation_coefficients;
		}

		public bool earlyInferenceDetector() {
			bool wrong_inference = false;
			var inferences = Microsoft.Research.Uncertain.Inference.Extensions.inferences;
			Dictionary<int, List<int>> inferences_with_dependencies = new Dictionary<int, List<int>>();
			foreach (var inference in inferences) {
				if (inference.GetType ().ToString ().Contains ("Microsoft.Research.Uncertain.Inference")) {
					if (inference.GetType ().ToString ().Contains ("Double")) {
						((Microsoft.Research.Uncertain.Inference<double>)inference).Accept (this);
						inferences_with_dependencies.Add(((Microsoft.Research.Uncertain.Inference<double>)inference).GetHashCode(),
						                                 ((Microsoft.Research.Uncertain.Inference<double>)inference).inference_dependencies);
					}
					else if (inference.GetType ().ToString ().Contains ("Int")) {
						((Microsoft.Research.Uncertain.Inference<int>)inference).Accept (this);
						inferences_with_dependencies.Add(((Microsoft.Research.Uncertain.Inference<double>)inference).GetHashCode(),
						                                 ((Microsoft.Research.Uncertain.Inference<double>)inference).inference_dependencies);

					}
				}
			}
			foreach(var inference1 in inferences_with_dependencies.Keys) {
				foreach (var inference2 in inferences_with_dependencies.Keys) {
					if (!inference1.Equals (inference2)) {
						if (inferences_with_dependencies [inference1].ToList().Intersect (inferences_with_dependencies[inference2].ToList()).Any ()) {
							wrong_inference = true;
							break;
						} else {
							continue;
						}
					}
				}
			}
			return wrong_inference;
		}
	}
}

