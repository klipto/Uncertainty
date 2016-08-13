using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;

using MathNet.Numerics.Statistics;
using MathNet.Numerics.Distributions;

using System.Linq.Expressions;

namespace InferenceDebugger
{
    public static class Program
    {      
        public static IEnumerable<Weighted<Tuple<R, R>>> Debug<R>(Func<R, R, Uncertain<double>> program, Uncertain<R> param1,
            Uncertain<R> param2, Func<Uncertain<double>, bool> CorrectnessCondition)
        {
            var Ks = from k1 in param1
                     from k2 in param2
                     let prog = program(k1, k2)
                     where CorrectnessCondition(prog) == true
                     select Tuple.Create(k1, k2);
            var result = Ks.Inference().Support().OrderByDescending(i => i.Probability).ToList();
            return result;
        }

        Func<int, Uncertain<double>> F = (k1) =>
                 from a in new Gaussian(0, 1).SampledInference(k1)
                 select a;

        const double BernoulliP = 0.05;
        Func<int, Uncertain<int>> F1 = (k1) =>
              from a in new Flip(BernoulliP).SampledInference(k1, null)
              select Convert.ToInt32(a);


        static Func<int, double, Uncertain<double>, Tuple<int, double, List<Weighted<double>>>> TVariateGenerator = (k, population_mean, sample) =>
        {
            var all_values = sample.Inference().Support().ToList();
            var sample_mean = all_values.Select(i => i.Value).Sum() / k;
            var sample_variance = all_values.Select(i => (i.Value - sample_mean) * (i.Value - sample_mean)).Sum() / (k - 1);
            var SEM = Math.Sqrt(sample_variance / k);
            var t_statistic = (sample_mean - population_mean) / SEM;
            return Tuple.Create(k, t_statistic, all_values);
        };

        Func<double, Uncertain<Uncertain<double>>, IEnumerable<Tuple<int, double, List<Weighted<double>>>>> SameSampleSizeBestProgramSampler = (population_mean, p) =>
        {
            var samples = p.SampledInference(10000).Support().ToList();
            var t_variates = from sample in samples
                             let t = TVariateGenerator(sample.Value.Inference().Support().ToList().Count, population_mean, sample.Value)
                             select t;
            var sorted_tvariates = t_variates.OrderByDescending(i => StudentT.PDF(0, 1, i.Item1 - 1, i.Item2)).ToList();
            Dictionary<int, Tuple<double, List<Weighted<double>>>> best_samples_of_fixed_sizes = new Dictionary<int, Tuple<double, List<Weighted<double>>>>();
            for (int x = 0; x < sorted_tvariates.Count; x++)
            {
                if (!best_samples_of_fixed_sizes.Keys.Contains(sorted_tvariates[x].Item1))
                {
                    best_samples_of_fixed_sizes.Add(sorted_tvariates[x].Item1, Tuple.Create(sorted_tvariates[x].Item2, sorted_tvariates[x].Item3));
                }
                else continue;
            }

            var max_likelihoods_for_each_sample_size = from best_sample_of_fixed_size in best_samples_of_fixed_sizes
                                                       select Tuple.Create(best_sample_of_fixed_size.Key, StudentT.PDF(0, 1, best_sample_of_fixed_size.Key - 1, best_sample_of_fixed_size.Value.Item1), best_sample_of_fixed_size.Value.Item2);

            return max_likelihoods_for_each_sample_size;
        };

        Func<IEnumerable<Tuple<int, double, List<Weighted<double>>>>, int> BestKSelector = (best_samples_of_fixed_size) =>
        {
            int k = 0;
            int largest_sample_size = best_samples_of_fixed_size.OrderByDescending(i => i.Item1).ElementAt(0).Item1;
            List<Tuple<double, int, double, List<Weighted<double>>>> normalized_sample_size = new List<Tuple<double, int, double, List<Weighted<double>>>>();

            foreach (var tuple in best_samples_of_fixed_size)
            {
                double ratio = (double)largest_sample_size / (double)tuple.Item1;
                var newTuple = Tuple.Create(ratio, tuple.Item1, tuple.Item2, tuple.Item3);
                normalized_sample_size.Add(newTuple);
            }
            var ordered_list_according_to_utility =
                normalized_sample_size.OrderByDescending(i => i.Item3 * (double)(i.Item2 - 3) * Math.Pow(i.Item1, 1 / 2) / (double)(i.Item2 - 1)); //proportional to product of likelihood and inversely proprotional to variance which is (dof/dof-2)  

            List<Tuple<double, double, double, int, double, List<Weighted<double>>>> utilities = new List<Tuple<double, double, double, int, double, List<Weighted<double>>>>();
            foreach (var tuple in ordered_list_according_to_utility)
            {
                double likelihood = tuple.Item3;
                double variance_inverse = (double)(tuple.Item2 - 3) / (double)(tuple.Item2 - 1);
                double ratio_l_var = likelihood * variance_inverse;
                double size_ratio = Math.Pow(tuple.Item1, 1 / 2);
                double final_utility = (ratio_l_var * size_ratio);
                double utility = Math.Round(final_utility, 3);
                var newTuple = Tuple.Create(final_utility, utility, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
                utilities.Add(newTuple);
            }
            var ordered_utilities = utilities.OrderByDescending(i => i.Item2);
            var max_utility = ordered_utilities.ElementAt(0).Item2;
            List<Tuple<double, double, double, int, double, List<Weighted<double>>>> best_utilities = new List<Tuple<double, double, double, int, double, List<Weighted<double>>>>();
            foreach (var utility_tuple in utilities)
            {
                if (utility_tuple.Item2 == max_utility)
                {
                    var tuple = Tuple.Create(utility_tuple.Item1, utility_tuple.Item2, utility_tuple.Item3, utility_tuple.Item4, utility_tuple.Item5, utility_tuple.Item6);
                    best_utilities.Add(tuple);
                }
            }
            k = best_utilities.OrderBy(i => i.Item4).ElementAt(0).Item4;
            return k;
        };

        public static void TestDebugger1()
        {
            var hyper1 = new FiniteEnumeration<int>(Enumerable.Range(10, 5).ToList());
            var hyper2 = new FiniteEnumeration<int>(Enumerable.Range(10, 5).ToList());
            var debug = Program.Debug(Example.P, hyper1, hyper2, Example.CorrectnessCondition);
            foreach (var d in debug)
            {
                Console.Write(d.Value + "\n");
            }
        }
      
        static void Main(string[] args)
        {
            TestDebugger1();        
            Console.ReadKey();
        }
    }
}
