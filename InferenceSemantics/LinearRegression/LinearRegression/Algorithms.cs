using MathNet.Numerics.LinearAlgebra;
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using System;
using System.Collections;
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
	public class Algorithms
	{
		public Algorithms ()
		{
		}

		static double GaussianLikelihood(double mu, double stdev, double t)
		{
			var a = 1.0 / (stdev * Math.Sqrt(2 * Math.PI));
			var b = Math.Exp(-Math.Pow(t - mu, 2) / (2 * stdev * stdev));
			return a * b;
		}

		static double Likelihood(Vector<Double> y, Vector<Double> yhat, double sigma)
		{
			Contract.Requires(y.Count == yhat.Count);

			var likelihood = 0.0;
			for (int i = 0; i < y.Count; i++)
			{
				likelihood += GaussianLikelihood(y[i], sigma, yhat[i]);
			}
			return likelihood;
		}

		static Uncertain<Vector<Double>> MaximumLikelihoodLearner(Matrix<Double> X, Vector<Double> Y, double alpha, double tolerance = 0.00001)
		{
			// w = (X^t.X)^-1 . X^t . y
			Vector<Double> w = Vector<Double>.Build.Dense(X.ColumnCount, 0);
			Vector<Double> guess = X * w;
			double error = (guess - Y).L2Norm();
			var count = 0;
			while (true)
			{
				w = w - alpha * (guess - Y) * X; // grad
				guess = X * w;
				var thiserror = (guess - Y).L2Norm();
				if (count > 100 && Math.Abs(error - thiserror) < tolerance)
				{
					break;
				}
				error = thiserror;
				count++;
			}
			// note cast to Uncertain<Vector<Double>>
			return w;
		}
	}
}

