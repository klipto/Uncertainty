using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.InferenceDebugger;
using Microsoft.Research.Uncertain.Inference;

namespace DependenceAnalyzer
{
	// this analyzer checks if two Random primitives have any correlation.
	public class DependenceAnalysis : IUncertainVisitor
	{
		private int generation = 0;
		private object sample;

		public List<RandomPrimitive> random_primitives{ get; private set;}

		public IList<object> Results { get; private set; }

		public DependenceAnalysis()
		{
			this.Results = new List<object>();
			this.random_primitives = new List<RandomPrimitive> ();
		}

		public void Visit<T>(RandomPrimitive<T> erp)
		{
			this.sample = erp.Sample(this.generation++);
			random_primitives.Add (erp);
		}

		public void Visit<T>(Where<T> where)
		{
			where.source.Accept(this);
		}

		public void Visit<TSource, TResult>(Select<TSource, TResult> select)
		{
			throw new NotImplementedException();
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
			this.Results.Add(inference);
			inference.Source.Accept(this);
		}

		public void correlationCalculator(List<RandomPrimitive> primitives)  
		{
			// compute correlation among the pairs in primitives.
		}
		public static void Main(string[] args)
		{		
			Uncertain<double> x = new Exponential(2);
			Uncertain<double> y = new Exponential (3);

			var p = from a in x
				    from b in new Flip(0.9)
					select a | b;

			Uncertain<bool> q = from i in p.Inference()
								where i
								from j in x
								select j | i;

			var analyzer = new DependenceAnalysis();
			x.Accept(analyzer);
			var result = analyzer.Results;
			List<RandomPrimitive> primitives = analyzer.random_primitives;
			//q.ExpectedValue();
			int xx = 10;
		}
	}
}
	
	