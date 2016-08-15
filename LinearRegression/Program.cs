using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinearRegression
{
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
    }
    class Program
    {
        private static double Score(double[][] x, double[] y, double[] weights, double sigma)
        {
            var likelihood = 0.0;
            for (int i = 0; i < x.Length; i++)
            {
                var yhat = 0.0;
                for (int j = 0; j < weights.Length; j++)
                {
                    yhat += weights[j] * x[i][j];
                }

                likelihood += Gaussian.Likelihood(yhat, y[i], sigma);
            }
            return likelihood / (double)x.Length;
        }

        private static void RunOne(int N, int F, StreamWriter file)
        {
            var noise = new RandomMath();
            var x = (from i in Enumerable.Range(0, N)
                     let xi = new[] { 1.0 }.Concat(Enumerable.Range(1, F).Select(j => noise.NextGaussian(j, 0.001)).Scale())
                     select xi.ToArray()).ToArray();

            var y = (from xi in x
                     select xi.Select((f, index) => index == 0 ? 1.0 : f * index).Aggregate(0.0, (a, b) => a + b)).ToArray();

            Func<double[], double, IEnumerable<double>> Output = (weights, sigma) => from i in Enumerable.Range(0, N)
                                                                                     let yhat = (from j in Enumerable.Range(0, F + 1)
                                                                                                 select weights[j] * x[i][j]).Sum()
                                                                                     select yhat;

            Func<double[], double, double> Score1 = (weights, sigma) => (from i in Enumerable.Range(0, N)
                                                                         let yhat = (from j in Enumerable.Range(0, F + 1)
                                                                                     select weights[j] * x[i][j]).Sum()
                                                                         let likelihood = Gaussian.Likelihood(yhat, y[i], sigma)
                                                                         select likelihood).Sum();

            Func<double[], double, double> Error = (weights, sigma) => (from i in Enumerable.Range(0, N)
                                                                        let yhat = (from j in Enumerable.Range(0, F + 1)
                                                                                    select weights[j] * x[i][j]).Sum()
                                                                        select Math.Pow(yhat - y[i], 2)).Sum();
            //var sigma1 = 0.001;
            Uncertain<Tuple<double[], double>> model = from sigma in new Gaussian(0, 1)
                                                       from weights in Enumerable.Range(0, F + 1).Select(_ => new Uniform<double>(1, 6)).USeq()
                                                       let likelihood = Score(x, y, weights, sigma)
                                                       select new Weighted<Tuple<double[], double>>
                                                       {
                                                           Value = Tuple.Create(weights, sigma),
                                                           Probability = likelihood
                                                       };
            //var tmp = new MarkovChainMonteCarloSampler<Tuple<double[], double>>(model).Skip(1000000).Take(1000).OrderByDescending(p => p.Probability).Take(10).ToList();
            //var yhats = Output(tmp.First().Value.Item1, tmp.First().Value.Item2).ToList();
            //for (int i = 0; i < N; i++)
            //{
            //    Console.WriteLine(String.Format("{0} {1}", yhats[i], y[i]));
            //}

            var sampler = new MarkovChainMonteCarloSampler<Tuple<double[], double>>(model);
            ulong count = 0;
            double score = double.NegativeInfinity;
            double bestError = double.NegativeInfinity;
            Tuple<double[], double> bestModel = null;

            foreach (var item in sampler)
            {
                if (score < item.Probability)
                {
                    score = item.Probability;
                    bestModel = item.Value;
                    bestError = Error(item.Value.Item1, item.Value.Item2);
                }

                var error = Error(item.Value.Item1, item.Value.Item2);
                file.WriteLine(String.Format("{0} {1} {2} {3} {4}", count++, item.Probability, error, score, bestError));

                if (count % 1024UL * 1024 == 0)
                    Console.Error.WriteLine(String.Format("{0} {1} {2}", count, item.Probability, error));

                if (count == 1024UL * 1024 * 1024 * 1024)
                    break;
            }

        }

        static void Main(string[] args)
        {
            using (var output = File.OpenWrite(@"e:\tmp\xxx.txt"))
            {
                using (var stream = new StreamWriter(output))
                {
                    RunOne(50, 4, stream);
                }
            }
        }
    }
}
