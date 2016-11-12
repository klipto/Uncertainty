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

namespace LinearRegression
{
	public class MLAlgorithms
	{
		public MLAlgorithms ()
		{

		}

		static Uncertain<Vector<Double>> MaximumAPosteriorLearner(Matrix<Double> X, Vector<Double> Y, double alpha, double lambda, double tolerance = 0.00001)
		{
			Vector<Double> w = Vector<Double>.Build.Dense(X.ColumnCount, 0);
			Vector<Double> guess = X * w;
			double error = (Y - guess).L2Norm();
			var count = 0;
			while (true)
			{
				w = w + alpha * ((-lambda * w) + (Y - X * w) * X);
				guess = X * w;
				var thiserror = (Y - guess).L2Norm();
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

		public static Uncertain<Vector<Double>> MaximumLikelihoodLearner(Matrix<Double> X, Vector<Double> Y, double alpha, double tolerance = 0.00001)
		{
			// w = (X^t.X)^-1 . X^t . y
			Vector<Double> w = Vector<Double>.Build.Dense(X.ColumnCount, 0);
			Vector<Double> guess = X * w;
			double error = (guess - Y).L2Norm();
			var count = 0;
			while (true)
			{
				w = w - alpha * (guess - Y) * X;
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

