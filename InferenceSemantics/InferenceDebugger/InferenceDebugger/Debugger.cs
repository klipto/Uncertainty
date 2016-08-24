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

namespace Microsoft.Research.Uncertain.InferenceDebugger
{
    public class Debugger<R>
    {
        public static TruncatedGeometric truncatedGeometric;

        public Debugger(double p, int a, int b)
        {
            truncatedGeometric = new TruncatedGeometric(p, a, b);
        }
        static Func<int, double, Uncertain<R>, Tuple<int, double, List<Weighted<R>>>> TVariateGenerator = (k, population_mean, sample) =>
        {
            var all_values = sample.Inference().Support().ToList();
            var sample_mean = all_values.Select(i => (dynamic)i.Value).Aggregate((a, b) => a + b) / k;
            var sample_variance = all_values.Select(i => Math.Pow((dynamic)i.Value - sample_mean, 2)).Aggregate((a, b) => a + b) / (k - 1);
            var SEM = Math.Sqrt((dynamic)sample_variance / k);
            var t_statistic = (sample_mean - population_mean) / SEM;
            return Tuple.Create(k, t_statistic, all_values);
        };

        Func<double, Uncertain<Uncertain<R>>, IEnumerable<Tuple<int, double, List<Weighted<R>>>>> SameSampleSizeBestProgramSampler = (population_mean, p) =>
        {
            var samples = p.SampledInference(100000).Support().ToList();
            var t_variates = from sample in samples
                             where sample.Value.Inference().Support().ToList().Count > 0
                             let t = TVariateGenerator(sample.Value.Inference().Support().ToList().Count, population_mean, sample.Value)
                             select t;
            List<Tuple<int, double, List<Weighted<R>>>> t_scores = new List<Tuple<int, double, List<Weighted<R>>>>();
            foreach (var t_variate in t_variates)
            {
                if (t_variate.Item1 - 1 <= 0)
                {
                    continue;
                }
                else
                {
                    var t_score = Tuple.Create(t_variate.Item1, StudentT.PDF(0, 1, t_variate.Item1 - 1, t_variate.Item2), t_variate.Item3);
                    t_scores.Add(t_score);
                }
            }
            var sorted_tscores = t_scores.OrderByDescending(i => i.Item2).ToList();
            Dictionary<int, Tuple<double, List<Weighted<R>>>> best_samples_of_fixed_sizes = new Dictionary<int, Tuple<double, List<Weighted<R>>>>();
            for (int x = 0; x < sorted_tscores.Count; x++)
            {
                if (!best_samples_of_fixed_sizes.Keys.Contains(sorted_tscores[x].Item1))
                {
                    best_samples_of_fixed_sizes.Add(sorted_tscores[x].Item1, Tuple.Create(sorted_tscores[x].Item2, sorted_tscores[x].Item3));
                }
                else continue;
            }

            var max_likelihoods_for_each_sample_size = from best_sample_of_fixed_size in best_samples_of_fixed_sizes
                                                       select Tuple.Create(best_sample_of_fixed_size.Key, StudentT.PDF(0.0, 1.0, (double)(best_sample_of_fixed_size.Key - 1), best_sample_of_fixed_size.Value.Item1), best_sample_of_fixed_size.Value.Item2);
            return max_likelihoods_for_each_sample_size;
        };

        Func<IEnumerable<Tuple<int, double, List<Weighted<R>>>>, int> BestKSelector = (best_samples_of_fixed_size) =>
        {
            int k = 0;
            List<Tuple<double, int, double, List<Weighted<R>>>> utilities = new List<Tuple<double, int, double, List<Weighted<R>>>>();
            foreach (var tuple in best_samples_of_fixed_size)
            {
                double likelihood = tuple.Item2;
                double variance_inverse = (double)(tuple.Item1 - 3) / (double)(tuple.Item1 - 1);
                double ratio_l_var = likelihood * variance_inverse;
                double k_likelihood_sqrt = (Math.Sqrt(truncatedGeometric.Score(tuple.Item1)));
                double utility = Math.Pow(ratio_l_var, 4) * Math.Sqrt(k_likelihood_sqrt);
                var newTuple = Tuple.Create(utility, tuple.Item1, tuple.Item2, tuple.Item3);
                utilities.Add(newTuple);
            }
            var ordered_utilities = utilities.OrderByDescending(i => i.Item1);
            var max_utility = ordered_utilities.ElementAt(0).Item1;
            List<Tuple<double, int, double, List<Weighted<R>>>> best_utilities = new List<Tuple<double, int, double, List<Weighted<R>>>>();
            foreach (var utility_tuple in utilities)
            {
                if (utility_tuple.Item1 == max_utility)
                {
                    var tuple = Tuple.Create(utility_tuple.Item1, utility_tuple.Item2, utility_tuple.Item3, utility_tuple.Item4);
                    best_utilities.Add(tuple);
                }
            }
            k = best_utilities.OrderBy(i => i.Item2).ElementAt(0).Item2;
            return k;
        };

        public int Debug<R>(Func<int, Uncertain<R>> program, double population_mean, Uncertain<Tuple<int, double>> hyper_params)
        {
            var uncertain_program = from k1 in hyper_params
                                    let prog = program(k1.Item1)
                                    select prog;
            var all_good_programs = SameSampleSizeBestProgramSampler(population_mean, (dynamic)uncertain_program);
            var best_hyper_parameter = BestKSelector(all_good_programs);
            return best_hyper_parameter;
        }
    }
}

