using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.InferenceDebugger;
using Microsoft.Research.Uncertain.Inference;

namespace DependenceAnalysis
{
	class DependenceAnalyzer<T>: IUncertainVisitor
	{
		private int generation = 0;
		private object sample;

		public List<object> random_primitives{ get; private set;}

		public DependenceAnalyzer()
		{
			this.random_primitives = new List<object> ();

		}

		public void Visit<T>(RandomPrimitive<T> erp)
		{
			this.sample = erp.Sample(this.generation++);
			random_primitives.Add(erp);
		}

		public void Visit<T>(Where<T> where)
		{
			where.source.Accept(this);

		}

		public void Visit<TSource, TResult>(Select<TSource, TResult> select)
		{
			select.source.Accept (this);
		}

		public void Visit<TSource, TCollection, TResult>(SelectMany<TSource, TCollection, TResult> selectmany)
		{
			selectmany.source.Accept(this);
			TSource a = (TSource) this.sample;

			Uncertain<TCollection> otherSampler = (selectmany.CollectionSelector.Compile())((TSource)this.sample);
			otherSampler.Accept(this);
			TCollection b = (TCollection) this.sample;

			Weighted<TResult> result = (selectmany.ResultSelector.Compile())(a, b);

			this.sample = result.Value;
		}

		public void Visit<T>(Inference<T> inference)
		{
			inference.Source.Accept(this);
		}


		// Could we do something more clever here by exploiting more statistics? For now, I think correlation is going to work just fine.
		public static List<Tuple<Tuple<int, int>, double>> correlationCalculator(List<object> primitives)  
		{
			// compute correlation among the pairs in primitives by drawing 1000 sample values and finding the correlation
			List<Tuple<Tuple<int, int>, double>> correlation_coefficients = new List<Tuple<Tuple<int, int>, double>>();
			int sample_size = 1000;

			List<ParametersOfERPs> primitives_parameters = new List<ParametersOfERPs> ();

			foreach (var primitive in primitives) {
				if (primitive.GetType ().BaseType.ToString ().Contains ("RandomPrimitive")
				    && primitive.GetType ().BaseType.ToString ().Contains ("Double")) {

					List<Weighted<double>> samples = ((RandomPrimitive<double>)primitive).SampledInference (sample_size).Support ().ToList ();

					double sum = samples.Select (i => Convert.ToDouble (i.Value)).Sum ();
					double square_sum = samples.Select (i => Convert.ToDouble (i.Value) * Convert.ToDouble (i.Value)).Aggregate ((a, b) => a + b);
					double stddev = Math.Sqrt (sample_size * square_sum - Math.Pow (sum, 2));
					primitives_parameters.Add (new ParametersOfERPs(((RandomPrimitive<double>)primitive).GetStructuralHash (), samples, sum, stddev));

				}

				if (primitive.GetType ().BaseType.ToString ().Contains ("RandomPrimitive")
				    && primitive.GetType ().BaseType.ToString ().Contains ("Int")) {

					List<Weighted<int>> samples = ((RandomPrimitive<int>)primitive).SampledInference (sample_size).Support ().ToList ();

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
	}
}

