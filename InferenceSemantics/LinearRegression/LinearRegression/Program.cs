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
            Matrix<double> s_sq = noise_sigma_square * (X.TransposeThisAndMultiply(X) +
                (lambda * Matrix<double>.Build.SparseIdentity(X.ColumnCount, X.ColumnCount))).Inverse();
            var I = Matrix<double>.Build.Sparse(1, 1, 1);
            
            var w = new Microsoft.Research.Uncertain.MultivariateNormal(mu, s_sq, I);
            Matrix<double> weight_sample = w.GetSample(); // sample from this posterior 
            var I_noise = Matrix<double>.Build.SparseIdentity(X.RowCount, X.RowCount);
            var y_posterior_predictive_distribution = new Microsoft.Research.Uncertain.MultivariateNormal(X * weight_sample, I_noise, I);
            var y_likelihoods = y_posterior_predictive_distribution.GetSample();

            var non_uncertain_success = TotalSuccessCounter(y_likelihoods, Y);           

            var weight_samples = new Microsoft.Research.Uncertain.MultivariateNormal(mu, s_sq, I).SampledInference(1000);
            List<Tuple<int, Matrix<double>>> success_list = new List<Tuple<int,Matrix<double>>>();
            foreach (var sample in weight_samples.Inference().Support())
            {
                var predictive_dist = new Microsoft.Research.Uncertain.MultivariateNormal(X * sample.Value, I_noise, I);
                var y_likelihood = predictive_dist.GetSample();
                var successes = TotalSuccessCounter(y_likelihood, Y);
                success_list.Add(Tuple.Create(successes, sample.Value));
            }
            var uncertain_success = success_list.OrderByDescending(i=> i.Item1);


            Func<int, Uncertain<Matrix<Double>>> F = (k) =>
              from a in new Microsoft.Research.Uncertain.MultivariateNormal(mu, s_sq, I).SampledInference(k) // p(w|y)~N(mu, s)
              select a;
            Debugger<Matrix<double>> doubleDebugger = new Debugger<Matrix<double>>(0.01, 100, 1000);
            var hyper = from k1 in doubleDebugger.hyperParameterModel.truncatedGeometric
                        select Tuple.Create(k1, doubleDebugger.hyperParameterModel.truncatedGeometric.Score(k1));
            var best_hyper_parameter = doubleDebugger.ComplexDebugSampleSize(doubleDebugger.hyperParameterModel, F, mu, s_sq , hyper);

            List<Tuple<int, Matrix<double>>> meta_inferred_success_list = new List<Tuple<int, Matrix<double>>>();
            foreach (var sample in best_hyper_parameter.Item4)
            {
                var predictive_dist = new Microsoft.Research.Uncertain.MultivariateNormal(X * sample.Value, I_noise, I);
                var y_likelihood = predictive_dist.GetSample();
                var successes = TotalSuccessCounter(y_likelihood, Y);
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
            var TRAINING_FILE = "ijcnn12";                        
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