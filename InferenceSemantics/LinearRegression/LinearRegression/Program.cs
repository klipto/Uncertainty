﻿using libsvm;
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
            Matrix<double> s = noise_sigma_square * (X.TransposeThisAndMultiply(X) +
                (lambda * Matrix<double>.Build.SparseIdentity(X.ColumnCount, X.ColumnCount))).Inverse();
            var I = Matrix<double>.Build.Sparse(1, 1, 1);

            Func<int, Uncertain<Matrix<Double>>> F = (k) =>
                from a in new Microsoft.Research.Uncertain.MultivariateNormal(mu, s, I).SampledInference(k) // p(w|y)~N(mu, s)
                select a;

            Debugger<Matrix<double>> doubleDebugger = new Debugger<Matrix<double>>(0.01, 100, 1000);
            //var hyper = from k1 in doubleDebugger.hyperParameterModel.truncatedGeometric
            //            select Tuple.Create(k1, doubleDebugger.hyperParameterModel.truncatedGeometric.Score(k1));
            //var KValue = doubleDebugger.DebugSampleSizeComplex(doubleDebugger.hyperParameterModel, F, mu, hyper);

            //Matrix<double> weight_sample = w.GetSample(); // sample from this posterior 
            //var I_noise = Matrix<double>.Build.SparseIdentity(X.RowCount, X.RowCount); 
            //var y_posterior_predictive_distribution = new MathNet.Numerics.Distributions.MatrixNormal(X * weight_sample, I_noise, I);
            //var prediction = y_posterior_predictive_distribution.Sample();            
        }

        private static Matrix<double> Train(Matrix<double> X, Matrix<double> Y)
        {
            Contract.Requires(X.RowCount == Y.RowCount);
            var alpha = 0.0001F;
            var w = Matrix<double>.Build.Dense(X.ColumnCount, Y.ColumnCount, 0F);
            var rand = new Random(0);

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
                    var error = tmp.TransposeThisAndMultiply(tmp)[0, 0] / (float)X.RowCount;
                    Console.WriteLine(error);
                }
            }
            return w;
        }
        static void Main(string[] args)
        {
            var TRAINING_FILE = "ijcnn1";
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
            BayesianTrain(X, Y);
            var w = Train(X, Y);

        }
    }
}