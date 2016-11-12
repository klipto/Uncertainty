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

namespace Regression
{
    public class FunctionalList<T> : IEnumerable<T>
    {
        // Creates a new list that is empty
        public FunctionalList()
        {
            IsEmpty = true;
        }
        // Creates a new list containe value and a reference to tail
        public FunctionalList(T head, FunctionalList<T> tail)
        {
            IsEmpty = false;
            Head = head;
            Tail = tail;
        }
        // Is the list empty?
        public bool IsEmpty { get; private set; }
        // Properties valid for a non-empty list
        public T Head { get; private set; }
        public FunctionalList<T> Tail { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            return FunctionalList.Helper<T>(this).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return FunctionalList.Helper<T>(this).GetEnumerator();
        }
    }

    // Static class that provides nicer syntax for creating lists
    public static class FunctionalList
    {
        public static FunctionalList<T> Empty<T>()
        {
            return new FunctionalList<T>();
        }
        public static FunctionalList<T> Cons<T>
                (T head, FunctionalList<T> tail)
        {
            return new FunctionalList<T>(head, tail);
        }

        internal static IEnumerable<T> Helper<T>(FunctionalList<T> lst)
        {
            if (lst.IsEmpty) yield break;
            yield return lst.Head;
            foreach (var item in Helper(lst.Tail))
                yield return item;
        }

        public static T[] ToArray<T>(FunctionalList<T> lst)
        {
            var array = Helper<T>(lst).ToArray();
            return array;
        }
    }
    public static class Extensions
    {
        public static IEnumerable<double> Scale(this IEnumerable<double> x)
        {
            var mean = x.Average();
            var sd = (from i in x
                      let tmp = i - mean
                      select tmp * tmp).Sum();
            return from i in x
                   select (i - mean) / sd;
        }

        public static Uncertain<T[]> USeq<T>(this IEnumerable<Uncertain<T>> source, int num)
        {
            Uncertain<T[]> output = source.Take(num).Aggregate<Uncertain<T>, Uncertain<FunctionalList<T>>, Uncertain<T[]>>(
                FunctionalList.Empty<T>(),
                (i, j) =>
                {
                    return from lst in i
                           from sample in j
                           select FunctionalList.Cons(sample, lst);
                },
                uncertainlst =>
                {
                    return from sample in uncertainlst
                           select sample.Reverse().ToArray();
                });
            return output;
        }

        public static Uncertain<T[]> USeq<T>(this IEnumerable<Uncertain<T>> source)
        {
            var data = source.ToList();
            return data.USeq<T>(data.Count);
        }
    }

    class Program
    {
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


        static void TestDifferentInferenceAlgos()
        {
            const int N = 10;
            const int F = 1;

            var r = new RandomMath();

            // NxF
            var X = Matrix<Double>.Build.Dense(N, F + 1, (i, j) => j == 0 ? 1.0 : 1 + r.NextGaussian(0, 0.01));
            // Nx1 = NxF . Fx1 -> Y = 2 * x0 + 3 * x1            
            var Y = X * Vector<Double>.Build.DenseOfArray(new[] { 2.0, 3.0 });
            var sigma = 0.1;

            // Y = 2*x0 + 3*x1 + N(0,\sigma)
            // Y = X.w + N(0,\sigma)
            // Y ~ N(X.w, \sigma)
            // find w
            var model = from w0 in new Gaussian(0, 1)
                        from w1 in new Gaussian(0, 1)

                        let w = Vector<Double>.Build.DenseOfArray(new[] { w0, w1 })

                        let yhat = X * w

                        let likelihood = Likelihood(Y, yhat, sigma)

                        select new Weighted<Vector<Double>> { Value = w, Probability = likelihood };

            var novelExample = Vector<Double>.Build.Dense(F + 1, (j) => j == 0 ? 1.0 : 1 + r.NextGaussian(0, 0.01));

            // compute maximumn likelihood estimate: least squares for fixed variance term
            // mle = (X^t.X)^-1 . X^t . y
            // 0.00001, 0.0001, 0.001, 0.01, 0.1
            var mle = MaximumLikelihoodLearner(X, Y, 0.001, 1e-07);

            // compute maximumn a posteri estimate: regularized least squares for fixed variance term
            // map = (X^t.X - \lambda X^t)^-1 . X^t . y
            var map = MaximumAPosteriorLearner(X, Y, 0.001, 0.1, 1e-07);

            // compute full posterior: Pr[Y ~ N(X*W, \sigma) 
            var bayesEstimate = model.SampledInference(100000);

            // Suppose novelExample leads to assertion failure for mle or map!
            //   In other words, IS MAP a good estimate of the model (true posterior)

            // \hat{map} = MaximumAPosteriorLearner(X, Y, 0.001, 0.1, 10); // increase learning to 1000 -> fix novelExample?
            // \hat{map} ~= ? true map ?
            // x = [1,0.5] y = 2 * x0 + 3 * x1  = 2 * 1 + 0.5 * 3 = 3.5
            // y < 5
            // suppose x . \hat{map} -> 6
            //  1. is \hat{map} a good estimate of map (is sample mode a good estimate of true mode)
            //     a. if yes then goto 2
            //     b. if no then debug / build better estimate of map! (success -- we found a better \hat{map} -- or failure -- we cannot improve model)
            //  2. is \hat{map} a good estimate of posterior (is good sample mode a good estimate of the true posterior!)

            // Edge detection example

            // MNIST 
            // 1.  Learn Features : 
            //     a) use prebuilt Sobel edge detector : 100 images x 256 features.   Learn X.w -> model of edges in an image.
            //     b) challenge problem: MLE version of kmeans here! 
            //     Uncertain<Feature> where feature is either from a or b above
            // 2.  Use above to learn model from features above to predict whether the image is a 0.  

            // Tests here
            var A = from w in model
                    let yhat = novelExample * w
                    select yhat;
            var Atest = (A < 5.0);

            var B = from w in bayesEstimate
                    let yhat = novelExample * w
                    select yhat;
            var Btest = (A < 5.0);

            var C = from w in map
                    let yhat = novelExample * w
                    select yhat;
            var Ctest = (C < 5.0);

            var D = from w in mle
                    let yhat = novelExample * w
                    select yhat;
            var Dtest = (D < 5.0);

            Contract.Assert(Atest.Pr());
            Contract.Assert(Btest.Pr());
            Contract.Assert(Ctest.Pr());
            Contract.Assert(Dtest.Pr());

        }

        static void Main(string[] args)
        {
            TestDifferentInferenceAlgos();

            const int N = 1000;
            const int F = 1;

            var r = new RandomMath();
            Func<double, double, double, bool> ApproxEqual = (a, b, tol) => Math.Abs(a - b) <= tol;
            //var p = from w_map in map
            //        let yhat_map = novelExample * w_map
            //        from w in posterior
            //        let yhat = novelExample * w
            //        let correct = novelExample[0] * 2.0 + novelExample[1] * 3.0
            //       s elect new { map = ApproxEqual(correct, yhat_map, 0.01), posterior = ApproxEqual(correct, yhat, 0.01) };

            //var c = p.Select(i => i.map).ExpectedValueWithConfidence(100000);
            //var d = p.Select(i => i.posterior).ExpectedValueWithConfidence(100000);

            /*   1.0, 1.0, 2.1, 3.1
             *   1.0, 1.1, 2.2, 2.9
             */

            // NxF



            //var Train = from g in Enumerable.Range(0, N).Select(_ => new Gaussian(0, 0.01)).USeq()
            //            let xtmp = Matrix<Double>.Build.Dense(N, F + 1, (i, j) => j == 0 ? 1.0 : 1 + g[j])
            //            let ytmp = xtmp * Vector<Double>.Build.DenseOfArray(new[] { 2.0, 3.0 })
            //            select new { X = xtmp, Y = ytmp };

            //var model = from s in new Gaussian(0, 1)
            //            let sigma = Math.Abs(s)
            //            from x0 in new Gaussian(0, 1)
            //            from x1 in new Gaussian(0, 1)

            //            let w = Vector<Double>.Build.DenseOfArray(new[] { x0, x1 })
            //            let likelihood = from pair in Train
            //                             let yhat = pair.X * w
            //                             let likelihood = Likelihood(pair.Y, yhat, sigma)
            //                             select new Weighted<Vector<Double>> {  Value = w, Probability = likelihood }

            //            select new Weighted<Vector<Double>> { Value = w, Probability = likelihood };

            int xx = 10;
        }
    }
}
