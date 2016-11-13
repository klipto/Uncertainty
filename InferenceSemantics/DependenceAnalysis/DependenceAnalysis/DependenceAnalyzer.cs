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
        public List<object> objs { get; private set; }
        public List<List<object>> tuple_objs { get; private set; }
		public List<int> dependencies;
        public List<Tuple<Tuple<int, int>, double>> correlations_in_list;
        public List<List<Tuple<Tuple<int, int>, double>>> correlations_paired;


		public DependenceAnalyzer()
		{
			this.random_primitives = new List<object> ();
			this.dependencies = new List<int>();
            
            this.objs = new List<object>();
            this.tuple_objs = new List<List<object>>();

            this.correlations_in_list = new List<Tuple<Tuple<int, int>, double>>();
            this.correlations_paired = new List<List<Tuple<Tuple<int, int>, double>>>();
		}

		public void Visit<T>(RandomPrimitive<T> erp)
		{
			this.sample = erp.Sample(this.generation++);
			random_primitives.Add(erp);
		}

        // find correlation between all random variables in a ulist.
		public void Visit<T>(UList<T> ulist)
		{
            foreach (var u in ulist) {
                if (!u.GetType().ToString().Contains("Tuple"))
                {
                    objs.Add((Object)u);
                }
                else if (u.GetType().BaseType.ToString().Contains("Tuple"))
                {
                    var vs = u.SampledInference(1000);
                    List<Weighted<Tuple<double, double>>> samples = (dynamic)vs.Support().ToList();
                    List<Tuple<double, double>> values =samples.Select(i=>i.Value).ToList();
                    List<double> xs = new List<double>();
                    List<double> ys = new List<double>();
                    foreach (var v in values) {
                        xs.Add(((Tuple<double, double>)v).Item1);
                        ys.Add(((Tuple<double, double>)v).Item2);
                    }
                    List<ParametersOfERPs> primitives_parameters = new List<ParametersOfERPs>();
                    List<Tuple<double, double>> x_ranks = new List<Tuple<double, double>>();
                    List<Tuple<double, double>> y_ranks = new List<Tuple<double, double>>();
                    
                    for (int x = 0; x < xs.Count; x++)
                    {
                        x_ranks.Add(Tuple.Create(xs.ElementAt(x), Ranker(xs)[x]));
                    }

                    for (int x = 0; x < ys.Count; x++)
                    {
                        y_ranks.Add(Tuple.Create(ys.ElementAt(x), Ranker(ys)[x]));
                    }
                    var xx = x_ranks.OrderBy(i=>i.Item2);
                    primitives_parameters.Add(new ParametersOfERPs(1, x_ranks));
                    primitives_parameters.Add(new ParametersOfERPs(2, y_ranks));
                    correlations_paired.Add(calculateCorrelation(primitives_parameters, 1000));
                }
            }
            correlations_in_list= spearmanCorrelationCalculator(objs);

			/*foreach (var erp in ulist) {
				Console.WriteLine (erp.GetType().ToString());
				if (erp.GetType ().BaseType.ToString ().Contains ("RandomPrimitive")) {
					Visit ((RandomPrimitive<T>)erp);
                }
                else if (erp.GetType().ToString().Contains("SelectMany"))
                {
                    Visit((SelectMany<T, T, T>)erp);
                }
			}*/

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

        public double[] Ranker(List<double> samples) {
            var rank_list = new double[samples.Count];
            int i, j;
            for (i = 0; i < samples.Count; i++)
            {
                int current_rank = 1;
                for (j = 0; j < i; j++)
                {
                    if (samples.ElementAt(i) > samples.ElementAt(j))
                    {
                        current_rank++;
                    }
                    else
                    {
                        rank_list[j] = rank_list[j] + 1;
                    }
                }
                rank_list[i] = current_rank;
            }
            return rank_list;
        }

		public List<Tuple<Tuple<int, int>, double>> spearmanCorrelationCalculator(List<object> primitives)
		{
			// compute Spearman's correlation among the pairs in primitives by drawing 1000 sample values and finding the correlation
			List<Tuple<Tuple<int, int>, double>> correlation_coefficients = new List<Tuple<Tuple<int, int>, double>>();
			int sample_size = 1000;

			List<ParametersOfERPs> primitives_parameters = new List<ParametersOfERPs> ();

			foreach (var primitive in primitives) {
                if (primitive.GetType().BaseType.ToString().Contains("RandomPrimitive")
                    && primitive.GetType().BaseType.ToString().Contains("Double"))
                {

                    var samples = ((RandomPrimitive<double>)primitive).SampledInference(sample_size).Support().Select(i=>i.Value).ToList();                 

                    Microsoft.Research.Uncertain.Inference.Extensions.inferences.RemoveAt(Microsoft.Research.Uncertain.Inference.Extensions.inferences.Count - 1);

                    List<Tuple<double, double>> ranks = new List<Tuple<double, double>>();

                    var rank_list = Ranker(samples);

                    for (int x = 0; x < samples.Count; x++)
                    {
                        ranks.Add(Tuple.Create(samples.ElementAt(x), rank_list[x]));
                    }
                    primitives_parameters.Add(new ParametersOfERPs(((RandomPrimitive<double>)primitive).GetStructuralHash(), ranks));
                }
                else if (primitive.GetType().ToString().Contains("SelectMany")
                    && primitive.GetType().BaseType.ToString().Contains("Double"))
                {

                    var samples = ((SelectMany<double, double, double>)primitive).SampledInference(sample_size).Support().Select(i=>i.Value).ToList();
                    

                    Microsoft.Research.Uncertain.Inference.Extensions.inferences.RemoveAt(Microsoft.Research.Uncertain.Inference.Extensions.inferences.Count - 1);

                    List<Tuple<double, double>> ranks = new List<Tuple<double, double>>();
                    var rank_list = Ranker(samples);

                    for (int x = 0; x < samples.Count; x++)
                    {
                        ranks.Add(Tuple.Create(samples.ElementAt(x), rank_list[x]));
                    }
                    primitives_parameters.Add(new ParametersOfERPs(((SelectMany<double, double, double>)primitive).GetHashCode(), ranks));
                }
			}

            return calculateCorrelation(primitives_parameters, sample_size);
		}

        public List<Tuple<Tuple<int, int>, double>> calculateCorrelation(List<ParametersOfERPs> primitives_parameters, int sample_size) 
        {
            List<Tuple<Tuple<int, int>, double>> correlation_coefficients = new List<Tuple<Tuple<int,int>,double>>();
            foreach (var primitive1 in primitives_parameters)
            {
                foreach (var primitive2 in primitives_parameters)
                {
                    double sum_of_differences_sq = 0.0;
                    if (primitive1.ranks.Count == primitive2.ranks.Count)
                    {
                        for (int x = 0; x < primitive1.ranks.Count; x++)
                        {
                            sum_of_differences_sq = sum_of_differences_sq + (Math.Pow(primitive1.ranks.ElementAt(x).Item2 - primitive2.ranks.ElementAt(x).Item2, 2));
                        }
                        double correlation = 1.0 - ((6.0 * sum_of_differences_sq) / (sample_size * (Math.Pow(sample_size, 2.0) - 1.0)));
                        correlation_coefficients.Add(Tuple.Create(Tuple.Create(primitive1.ID, primitive2.ID), correlation));
                    }
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

