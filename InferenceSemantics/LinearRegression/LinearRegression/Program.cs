using libsvm;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using Microsoft.Research.Uncertain.InferenceDebugger;

namespace LinearRegression
{
    class Program
    {
       
        private static void BayesianTrain(Matrix<double> X, Matrix<double> Y)
        {
            double lambda = 0.5;
            double noise_sigma_square = 1;
            var mu = (lambda * Matrix<double>.Build.SparseIdentity(X.ColumnCount, X.ColumnCount) + X.TransposeThisAndMultiply(X)).Inverse() * X.Transpose() * Y;
            Matrix<double> s_sq = noise_sigma_square * (X.TransposeThisAndMultiply(X) +
                (lambda * Matrix<double>.Build.SparseIdentity(X.ColumnCount, X.ColumnCount))).Inverse();
            var I = Matrix<double>.Build.Sparse(1, 1, 1);
            
            var w = new Microsoft.Research.Uncertain.MultivariateNormal(mu, s_sq, I);
            Matrix<double> weight_sample = w.GetSample(); // sample from this posterior 
            var I_noise = Matrix<double>.Build.SparseIdentity(X.RowCount, X.RowCount);

            var TEST_FILE = "ijcnn11.t";
            var data = ProblemHelper.ReadAndScaleProblem(TEST_FILE);
            var numexamples = data.l;
            var numfeatures = (from example in data.x
                               from column in example
                               select column.index).Max() + 1;

            var Xt = Matrix<double>.Build.Sparse(numexamples, numfeatures);
            var Yt = Matrix<double>.Build.Dense(numexamples, 1);

            for (int i = 0; i < data.l; i++)
            {
                foreach (var column in data.x[i])
                {
                    Xt[i, column.index] = (float)column.value;
                }
                Yt[i, 0] = (float)data.y[i];
            }

            var y_posterior_predictive_distribution = new Microsoft.Research.Uncertain.MultivariateNormal(Xt * weight_sample, I_noise, I);
            var y_likelihoods = y_posterior_predictive_distribution.GetSample();

            var non_uncertain_success = TotalSuccessCounter(y_likelihoods, Yt);           

            var weight_samples = new Microsoft.Research.Uncertain.MultivariateNormal(mu, s_sq, I).SampledInference(1000);
            List<Tuple<int, Matrix<double>>> success_list = new List<Tuple<int,Matrix<double>>>();
            foreach (var sample in weight_samples.Inference().Support())
            {
                var predictive_dist = new Microsoft.Research.Uncertain.MultivariateNormal(Xt * sample.Value, I_noise, I);
                var y_likelihood = predictive_dist.GetSample();
                var successes = TotalSuccessCounter(y_likelihood, Yt);
                success_list.Add(Tuple.Create(successes, sample.Value));
            }
            var uncertain_success = success_list.OrderByDescending(i=> i.Item1);


            Func<int, Uncertain<Matrix<Double>>> F = (k) =>
              from a in new Microsoft.Research.Uncertain.MultivariateNormal(mu, s_sq, I).SampledInference(k) // p(w|y)~N(mu, s)
              select a;
            Debugger<Matrix<double>> doubleDebugger = new Debugger<Matrix<double>>(0.001, 100, 1000);
			var hyper = from k1 in ((TruncatedHyperParameterModel)doubleDebugger.hyperParameterModel).truncatedGeometric
				select Tuple.Create(k1, ((TruncatedHyperParameterModel)doubleDebugger.hyperParameterModel).truncatedGeometric.Score(k1));
			var best_hyper_parameter = doubleDebugger.ComplexDebugSampleSize((TruncatedHyperParameterModel)doubleDebugger.hyperParameterModel, F, mu, s_sq , hyper);

            List<Tuple<int, Matrix<double>>> meta_inferred_success_list = new List<Tuple<int, Matrix<double>>>();
            
            foreach (var sample in best_hyper_parameter.Item4)
            {
                var predictive_dist = new Microsoft.Research.Uncertain.MultivariateNormal(Xt * sample.Value, I_noise, I);
                var y_likelihood = predictive_dist.GetSample();
                var successes = TotalSuccessCounter(y_likelihood, Yt);
                meta_inferred_success_list.Add(Tuple.Create(successes, sample.Value));
            }
            var meta_inferred_uncertain_success = meta_inferred_success_list.OrderByDescending(i => i.Item1);
    }

        private static int TotalSuccessCounter(Matrix<double> estimates, Matrix<double> actuals)
        {
            int counter = 0;
            List<int> sign_diffs = new List<int>();
            for (int x = 0; x < estimates.RowCount; x++)
            {
                var diff = Math.Sign(estimates[x, 0]) - Math.Sign(actuals[x, 0]);
                sign_diffs.Add(diff);
            }
            foreach (var sign in sign_diffs) 
            {
                if (sign == 0) counter++;
            }
            return counter;
        }

        private static Tuple<int,Matrix<double>> MaximumLikelihoodTrain(Matrix<double> X, Matrix<double> Y, double alpha, double tolerance)
        {
			Matrix<Double> w = Matrix<Double>.Build.Dense(X.ColumnCount, Y.ColumnCount, 0);
			Matrix<Double> guess = X * w;
			var rand = new Random(0);
			double error = (guess - Y).L2Norm();
			Console.WriteLine ("original error:" + error);
			var count = 0;

			while (true)
			{
				//w = w - alpha * X.Transpose() *(guess - Y) ;
				var index = rand.Next(0, X.RowCount);
				var xi = X.Row(index).ToColumnMatrix();
				var yi = Y.Row(index).ToColumnMatrix();

				w = w - alpha * xi * (xi.TransposeThisAndMultiply(w) - yi);
				guess = X * w;
				var thiserror = (guess - Y).L2Norm();
				Console.WriteLine ("this_error: " + thiserror);
				Console.WriteLine ("error difference: "+Math.Abs(thiserror - error));
				if (Math.Abs(error - thiserror) < tolerance) 
				{
					break;
				}
				error = thiserror;
				count++;
			}
			Console.WriteLine ("count:"+ count);
			// note cast to Uncertain<Vector<Double>>
			var ret = Tuple.Create (count, w);
			return ret;

			/*var w = Matrix<double>.Build.Dense(X.ColumnCount, Y.ColumnCount, 0F);
            var rand = new Random(0);
			var error = w * X;
            var count = 0;
            while (true)
            //for (int i = 0; i < X.RowCount; i++)
            {
                count++;

                var index = rand.Next(0, X.RowCount);
                var xi = X.Row(index).ToColumnMatrix();
                var yi = Y.Row(index).ToColumnMatrix();

                w -= alpha * xi * (xi.TransposeThisAndMultiply(w) - yi);
                if (count % 1000 == 0)
                {
                    var tmp = (X * w - Y);
                    var new_error = tmp.TransposeThisAndMultiply(tmp)[0, 0] / (float)X.RowCount;
                    Console.WriteLine(error);
                }
            }
            return w;*/
        }

        static void Main(string[] args)
        {
			var TRAINING_FILE = "ijcnn11";                        
			var data = ProblemHelper.ReadAndScaleProblem(TRAINING_FILE);
			var numexamples = data.l;
			var numfeatures = (from example in data.x
			                   from column in example
			                   select column.index).Max() + 1;

			var X = Matrix<double>.Build.Sparse(numexamples, numfeatures);
			var Y = Matrix<double>.Build.Dense(numexamples, 1);

			for (int i = 0; i < data.l; i++)
			{
				foreach (var column in data.x[i])
				{
					X[i, column.index] = (float)column.value;
				}
				Y[i, 0] = (float)data.y[i];
			} 

			//BayesianTrain(X, Y);

			Func<double, Tuple<int, Matrix<double>>> F = (a) =>	MaximumLikelihoodTrain (X, Y, a, 0.01);

			List<Weighted<double>> alphas = 
				new List<Weighted<double>> (new Weighted<double>[] {new Weighted<double>(0.001, 0.3), new Weighted<double>(0.01, 0.6), new Weighted<double>(0.1, 0.1)});

			Debugger<double> doubleDebugger = new Debugger<double> (alphas);
			var alpha = from k1 in ((FiniteEnumHyperParameterModel)
			                        doubleDebugger.hyperParameterModel).finiteEnumeration
				select Tuple.Create(k1, ((FiniteEnumHyperParameterModel)doubleDebugger.hyperParameterModel).finiteEnumeration.Score(k1));

			double best_alpha = doubleDebugger.DebugAlphaLearningRate((FiniteEnumHyperParameterModel)doubleDebugger.hyperParameterModel, F, alpha);

			//var w = MaximumLikelihoodTrain(X, Y, 0.001, 0.01);

			DateTime start1 = DateTime.Now;
			var program1 = from a in new Flip (0.9)
						   from b in new Flip (0.9)
						   from c in new Flip (0.9)
					select Convert.ToInt32 (a) + Convert.ToInt32 (b) + Convert.ToInt32 (c);
			var d1 = program1.SampledInference (1000).Support ().ToList ();

			DateTime stop1 = DateTime.Now;
			var difference1 = stop1 - start1;

			DateTime start2 = DateTime.Now;
			var program2 = from a in new Flip(0.9).SampledInference(1000, null)
						   from b in new Flip(0.9).SampledInference(1000, null)
						   from c in new Flip(0.9).SampledInference(1000, null)
					select Convert.ToInt32 (a) + Convert.ToInt32 (b) + Convert.ToInt32 (c);
			var d2 = program2.Inference ().Support ().ToList ();
			DateTime stop2 = DateTime.Now;

			var difference2 = stop2 - start2;

       

        }
    }
}