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
	public class ParametersOfERPs
	{
		protected internal int ID;
		protected internal List<Weighted<double>> d_samples;
		protected internal List<Weighted<int>> i_samples;
		protected internal List<Weighted<object>> samples;
		protected internal double sum;
		protected internal double stddev;

		public ParametersOfERPs (int id, List<Weighted<double>> samples, double sum, double stddev)
		{
			this.ID = id;
			this.d_samples = samples;
			this.sum = sum;
			this.stddev = stddev;
			this.samples = new List<Weighted<object>> ();
			foreach (var d in this.d_samples) {
				this.samples.Add(new Weighted<object> ((object)d.Value, d.Probability));			
			}
		}

		public ParametersOfERPs (int id, List<Weighted<int>> samples, double sum, double stddev)
		{
			this.ID = id;
			this.i_samples = samples;
			this.sum = sum;
			this.stddev = stddev;
			this.samples = new List<Weighted<object>> ();
			foreach (var i in this.i_samples) {
				this.samples.Add (new Weighted<object> ((object)i.Value, i.Probability));
			}
		}
	}
}

