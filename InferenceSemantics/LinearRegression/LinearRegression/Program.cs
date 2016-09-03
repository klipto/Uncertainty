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
        private static void BayesianTrain(Matrix<Single> X, Matrix<Single> Y)
        {
            float a = 0.5F;
            float noise_sigma = 1;
            var I = Matrix<float>.Build.DenseIdentity(X.ColumnCount, X.ColumnCount);
            var S0 = Matrix<float>.Build.Dense(X.ColumnCount, X.ColumnCount);
            I.DivideByThis(a, S0);
            var aI = Matrix<float>.Build.Dense(X.ColumnCount, X.ColumnCount);
            I.DivideByThis((1 / a), aI);
            var intermediate = aI + noise_sigma * X.TransposeThisAndMultiply(X);
            var mu = (intermediate.Inverse()) * noise_sigma * (X.TransposeThisAndMultiply(Y));

        }
        private static Matrix<Single> Train(Matrix<Single> X, Matrix<Single> Y)
        {
            Contract.Requires(X.RowCount == Y.RowCount);

            var alpha = 0.0001F;
            var w = Matrix<Single>.Build.Dense(X.ColumnCount, Y.ColumnCount, 0F);
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
            var TRAINING_FILE = "rcv1_train.binary";
            var data = ProblemHelper.ReadAndScaleProblem(TRAINING_FILE);
            var numexamples = data.l;
            var numfeatures = (from example in data.x
                               from column in example
                               select column.index).Max() + 1;

            var X = Matrix<Single>.Build.Sparse(numexamples, numfeatures);
            var Y = Matrix<Single>.Build.Dense(numexamples, 1);

            for (int i = 0; i < data.l; i++)
            {
                foreach (var column in data.x[i])
                {
                    X[i, column.index] = (float)column.value;
                }
                Y[i, 0] = (float)data.y[i];
            }

            var w = Train(X, Y);
            BayesianTrain(X, Y);
        }
    }
}


