﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;

using MathNet.Numerics.Statistics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

using System.Linq.Expressions;

namespace Microsoft.Research.Uncertain.InferenceDebugger
{
    public struct HyperParameterModel
    {
        public TruncatedGeometric truncatedGeometric;
        public HyperParameterModel(double p, int a, int b)
        {
            truncatedGeometric = new TruncatedGeometric(p, a, b);
        }
    }
    public class Debugger<R>
    {
        public HyperParameterModel hyperParameterModel;
        public Debugger(double p, int a, int b)
        {
            hyperParameterModel = new HyperParameterModel(p, a, b);
        }

        static Func<int, double, Uncertain<R>, Tuple<int, double, List<Weighted<R>>, double>> TVariateGenerator = (k, population_mean, sample) =>
        {
            var all_values = sample.Inference().Support().ToList();
            var sample_mean = all_values.Select(i => (dynamic)i.Value).Aggregate((a, b) => a + b) / k;
            var sample_variance = all_values.Select(i => Math.Pow((dynamic)i.Value - sample_mean, 2)).Aggregate((a, b) => a + b) / (k - 1);
            var SEM = Math.Sqrt((dynamic)sample_variance / k);
            var t_statistic = (sample_mean - population_mean) / SEM;
            return Tuple.Create(k, t_statistic, all_values, sample_variance);
        };

        Func<double, Uncertain<Uncertain<R>>, IEnumerable<Tuple<int, double, List<Weighted<R>>, double>>> SameSampleSizeBestProgramSamplerForSize = (population_mean, p) =>
        {
            var samples = p.SampledInference(10000).Support().ToList();
            var t_variates = from sample in samples
                             where sample.Value.Inference().Support().ToList().Count > 0
                             let t = TVariateGenerator(sample.Value.Inference().Support().ToList().Count, population_mean, sample.Value)
                             select t;
            List<Tuple<int, double, List<Weighted<R>>, double>> t_scores = new List<Tuple<int, double, List<Weighted<R>>, double>>();
            foreach (var t_variate in t_variates)
            {
                if (t_variate.Item1 - 1 <= 0)
                {
                    continue;
                }
                else
                {
                    var t_score = Tuple.Create(t_variate.Item1, StudentT.PDF(0, 1, t_variate.Item1 - 1, t_variate.Item2), t_variate.Item3, t_variate.Item4);
                    t_scores.Add(t_score);
                }
            }
            var sorted_tscores = t_scores.OrderByDescending(i => i.Item2 / ((i.Item4))).ToList();
            Dictionary<int, Tuple<double, List<Weighted<R>>, double>> best_samples_of_fixed_sizes = new Dictionary<int, Tuple<double, List<Weighted<R>>, double>>();
            for (int x = 0; x < sorted_tscores.Count; x++)
            {
                if (!best_samples_of_fixed_sizes.Keys.Contains(sorted_tscores[x].Item1))
                {
                    best_samples_of_fixed_sizes.Add(sorted_tscores[x].Item1, Tuple.Create(sorted_tscores[x].Item2, sorted_tscores[x].Item3, sorted_tscores[x].Item4));
                }
                else continue;
            }

            var max_likelihoods_for_each_sample_size = from best_sample_of_fixed_size in best_samples_of_fixed_sizes
                                                       select Tuple.Create(best_sample_of_fixed_size.Key,
                                                       StudentT.PDF(0.0, 1.0, (double)(best_sample_of_fixed_size.Key - 1), best_sample_of_fixed_size.Value.Item1),
                                                       best_sample_of_fixed_size.Value.Item2, best_sample_of_fixed_size.Value.Item3);
            return max_likelihoods_for_each_sample_size;
        };

        Func<IEnumerable<Tuple<int, double, List<Weighted<R>>, double>>, HyperParameterModel, Tuple<int, List<Weighted<R>>>> BestKSelectorForSampleSize = (best_samples_of_fixed_size, model) =>
        {
            int k = 0;
            
            string file = "utility_example.txt";
            using (StreamWriter sw = new StreamWriter(file))
            {
                List<Tuple<double, int, double, List<Weighted<R>>, double, double>> utilities = new List<Tuple<double, int, double, List<Weighted<R>>, double, double>>();
                foreach (var tuple in best_samples_of_fixed_size)
                {
                    double likelihood = tuple.Item2;
                    double var_underlying_program = (tuple.Item4);
                    double variance_inverse = (double)(tuple.Item1 - 3) / (double)(tuple.Item1 - 1);
                    double ratio_l_var = likelihood * variance_inverse / (var_underlying_program);
                    double k_likelihood = ((model.truncatedGeometric.Score(tuple.Item1)));
                    double utility = ratio_l_var * (k_likelihood);
                    var newTuple = Tuple.Create(utility, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, var_underlying_program);
                    utilities.Add(newTuple);
                    sw.WriteLine(tuple.Item1 + " " + utility);
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
                var best_program = best_utilities.OrderBy(i => i.Item2).ElementAt(0).Item4;
                return Tuple.Create(k, best_program);
            }
        };

        static Func<int, double, double, Uncertain<R>, RandomPrimitive<R>, Tuple<int, double, List<Weighted<R>>, double, double>> ModeGenerator = (k, population_mode, population_mean, sample, function) =>
        {
            var all_values = sample.Inference().Support().ToList();
            var sample_mode = (dynamic)all_values.OrderByDescending(i => function.Score(i.Value)).ElementAt(0).Value;
            var sample_mean = all_values.Select(i => (dynamic)i.Value).Aggregate((a, b) => a + b) / k;
            var sample_variance = all_values.Select(i => Math.Pow((dynamic)i.Value - sample_mean, 2)).Aggregate((a, b) => a + b) / (k - 1);
            var SEM = Math.Sqrt((dynamic)sample_variance / k);
            var t_statistic = (sample_mean - population_mean) / SEM;
            return Tuple.Create(k, t_statistic, all_values, sample_variance, sample_mode);
        };

        Func<double, double, Uncertain<Uncertain<R>>, RandomPrimitive<R>, IEnumerable<Tuple<int, double, List<Weighted<R>>, double, double>>> SameSampleSizeBestProgramSamplerForTopk = (population_mode, population_mean, p, function) =>
        {
            var samples = p.SampledInference(10000).Support().ToList();
            var t_variates_and_modes = from sample in samples
                                       where sample.Value.Inference().Support().ToList().Count > 0
                                       let t = ModeGenerator(sample.Value.Inference().Support().ToList().Count, population_mode, population_mean, sample.Value, function)
                                       select t;
            List<Tuple<int, double, List<Weighted<R>>, double, double>> t_scores = new List<Tuple<int, double, List<Weighted<R>>, double, double>>();
            foreach (var t_variate in t_variates_and_modes)
            {
                if (t_variate.Item1 - 1 <= 0)
                {
                    continue;
                }
                else
                {
                    var t_score = Tuple.Create(t_variate.Item1, StudentT.PDF(0, 1, t_variate.Item1 - 1, t_variate.Item2), t_variate.Item3, t_variate.Item4, t_variate.Item5);
                    t_scores.Add(t_score);
                }
            }
            var sorted_tscores = t_scores.OrderByDescending(i => i.Item2 * i.Item5 / ((i.Item4))).ToList();
            Dictionary<int, Tuple<double, List<Weighted<R>>, double, double>> best_samples_of_fixed_sizes = new Dictionary<int, Tuple<double, List<Weighted<R>>, double, double>>();
            for (int x = 0; x < sorted_tscores.Count; x++)
            {
                if (!best_samples_of_fixed_sizes.Keys.Contains(sorted_tscores[x].Item1))
                {
                    best_samples_of_fixed_sizes.Add(sorted_tscores[x].Item1, Tuple.Create(sorted_tscores[x].Item2, sorted_tscores[x].Item3, sorted_tscores[x].Item4, sorted_tscores[x].Item5));
                }
                else continue;
            }

            var max_likelihoods_for_each_sample_size = from best_sample_of_fixed_size in best_samples_of_fixed_sizes
                                                       select Tuple.Create(best_sample_of_fixed_size.Key, StudentT.PDF(0.0, 1.0, (double)(best_sample_of_fixed_size.Key - 1), best_sample_of_fixed_size.Value.Item1), best_sample_of_fixed_size.Value.Item2,
                                                       best_sample_of_fixed_size.Value.Item3, best_sample_of_fixed_size.Value.Item4);
            return max_likelihoods_for_each_sample_size;
        };


        Func<IEnumerable<Tuple<int, double, List<Weighted<R>>, double, double>>, HyperParameterModel, Tuple<int, List<Weighted<R>>>> BestKSelectorForTopk = (best_samples_of_fixed_size, model) =>
        {
            int k = 0;
            string file = "utility_example.txt";
            using (StreamWriter sw = new StreamWriter(file))
            {
                List<Tuple<double, int, double, List<Weighted<R>>, double, double>> utilities = new List<Tuple<double, int, double, List<Weighted<R>>, double, double>>();
                foreach (var tuple in best_samples_of_fixed_size)
                {
                    double likelihood = tuple.Item2;
                    double var_underlying_program = (tuple.Item4);
                    double variance_inverse = (double)(tuple.Item1 - 3) / (double)(tuple.Item1 - 1);
                    double ratio_l_var = likelihood * variance_inverse / (var_underlying_program);
                    double k_likelihood = ((model.truncatedGeometric.Score(tuple.Item1)));
                    double utility = ratio_l_var * (k_likelihood);
                    var newTuple = Tuple.Create(utility, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, var_underlying_program);
                    utilities.Add(newTuple);
                    sw.WriteLine(tuple.Item1 + " " + utility);
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
                var best_program = best_utilities.OrderBy(i => i.Item2).ElementAt(0).Item4;
                return Tuple.Create(k, best_program);
            }
        };

        public Tuple<int, List<Weighted<R>>> DebugSampleSize<R>(HyperParameterModel model, Func<int, Uncertain<R>> program, double population_mean, Uncertain<Tuple<int, double>> hyper_params)
        {
            var uncertain_program = from k1 in hyper_params
                                    let prog = program(k1.Item1)
                                    select prog;
            var all_good_programs = SameSampleSizeBestProgramSamplerForSize(population_mean, (dynamic)uncertain_program);
            Tuple<int, List<Weighted<R>>> best_hyper_parameter = BestKSelectorForSampleSize(all_good_programs, model);
            return best_hyper_parameter;
        }        
 
        public Tuple<int, List<Weighted<R>>> DebugTopk<R>(HyperParameterModel model, Func<int, Uncertain<R>> program, double population_mode, double population_mean, Uncertain<Tuple<int, double>> hyper_params,
            RandomPrimitive<R> function)
        {
            var uncertain_program = from k1 in hyper_params
                                    let prog = program(k1.Item1)
                                    select prog;
            var all_good_programs = SameSampleSizeBestProgramSamplerForTopk(population_mode, population_mean, (dynamic)uncertain_program, (dynamic)function);
            Tuple<int, List<Weighted<R>>> best_hyper_parameter = BestKSelectorForTopk(all_good_programs, model);
            return best_hyper_parameter;
        }

        public Tuple<double, double, double, List<Weighted<Matrix<double>>>, int> ComplexDebugSampleSize(HyperParameterModel model, 
            Func<int, Uncertain<Matrix<double>>> program, Matrix<double> population_mean, Matrix<double> population_var,Uncertain<Tuple<int, double>> hyper_params)
        {
            var uncertain_program = from k1 in hyper_params
                                    let prog = program(k1.Item1)
                                    select prog;

            List<Tuple<double, double, double, List<Weighted<Matrix<double>>>, int>> sample_parameters = new List<Tuple<double, double, double, List<Weighted<Matrix<double>>>, int>>();
            var samples = uncertain_program.SampledInference(10000).Support().ToList();
            // pick that sample for which the likelihood of N(sample_mean - population_mean, sigma_sq/n) is maximized.
            foreach (var sample in samples) 
            {                
                var sample_size = sample.Value.Inference().Support().ToList().Count;
                var actual_sample = sample.Value.Inference().Support().ToList();
                var sample_sum = Matrix<double>.Build.Sparse(population_mean.RowCount,1,0);
                for(int x=0;x< population_mean.RowCount;x++)
                {
                    for (int y = 0; y < sample_size; y++)
                    {
                        sample_sum[x, 0] = sample_sum[x,0] + (actual_sample.ElementAt(y).Value)[x, 0];
                    }
                }
                var sample_mean = sample_sum / sample_size;
                Matrix<double> sample_var = population_var/sample_size; // sigma_sq/n                
                Matrix<double> mean_of_gaussian = Matrix<double>.Build.Sparse(population_mean.RowCount, 1); // 0 matrix
                var I = Matrix<double>.Build.Sparse(1, 1, 1);
                var n_score = new MultivariateNormal(mean_of_gaussian, sample_var, I).Density(sample_mean - population_mean);
                var k_likelhood = model.truncatedGeometric.Score(sample_size);
                var utility = k_likelhood * n_score;
                var parameter = Tuple.Create(n_score, k_likelhood, utility, actual_sample, sample_size);
                sample_parameters.Add(parameter);
            }
            var best_sample = sample_parameters.OrderByDescending(i=> i.Item3).ElementAt(0);
            return best_sample;           
        }
    }
}