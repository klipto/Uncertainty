using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;

using MathNet.Numerics.Statistics;
using MathNet.Numerics.Distributions;

using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Version = Lucene.Net.Util.Version;

namespace SearchEngine
{
    public static class SearchEngineImpl
    {
        private static int number_of_machines = 3;
        // This is used by the  "central server" to prune the results of the other servers and return the final top-k.
        private static double threshold = 0.3;
        private static Dictionary<int, List<SampleData>> data_partitions_for_distributed_search = new Dictionary<int, List<SampleData>>();

        public struct ChosenDocument
        {
            public Field field;
            public double picking_probability;
            public double exponential_bound;

            public override bool Equals(object obj)
            {
                if (obj == null)
                {
                    return false;
                }

                if (obj.GetType().ToString().Contains("ChosenDocument"))
                {
                    if (((ChosenDocument)obj).field.ToString().Equals(this.field.ToString()))
                    {
                        return true;
                    }
                }
                return false;
            }
            public override int GetHashCode()
            {
                return this.field.ToString().GetHashCode();
            }
        }

        static Dictionary<int, List<SampleData>> CreateDataPartitions(List<SampleData> dataset, int number_of_machines)
        {
            Dictionary<int, List<SampleData>> partitions = new Dictionary<int, List<SampleData>>();
            Dictionary<int, int> partition_sizes = new Dictionary<int, int>();
            int partition_size = 0;

            // assuming all servers are equally powerful, we try to allocate the same number of searches to each of them. 
            partition_size = dataset.Count / number_of_machines;
            for (int x = 1; x <= number_of_machines; x++)
            {
                partition_sizes.Add(x, partition_size);
            }

            // if the dataset cannot be split equally, distribute the extras among the machines as evenly as possible.
            if (dataset.Count % number_of_machines != 0)
            {
                int extras = dataset.Count % number_of_machines;
                for (int x = 1; x <= extras; x++)
                {
                    partition_sizes[x] = partition_sizes[x] + 1;
                }
            }

            int index = 0;
            foreach (var machine_partition_size in partition_sizes)
            {
                List<SampleData> partition = new List<SampleData>();
                for (int x = index; x < index + machine_partition_size.Value; x++)
                {
                    partition.Add(dataset[x]);
                }
                index = index + machine_partition_size.Value;
                partitions.Add(machine_partition_size.Key, partition);
            }
            return partitions;
        }

        public static HashSet<ChosenDocument> finalSearch(int topk, Dictionary<int, Dictionary<Field, double>> score_probabilities, Dictionary<int, Dictionary<Field, double>> score_summaries,
            HashSet<Uncertain<ChosenDocument[]>> uncertain_documents)
        {
            var s1 = uncertain_documents.ElementAt(0);
            var s2 = uncertain_documents.ElementAt(1);
            var s3 = uncertain_documents.ElementAt(2);

            var final_sampled_output = from o1 in s1
                                       from o2 in s2
                                       from o3 in s3
                                       let combined_output = o1.Concat(o2).Concat(o3)
                                       let sorted = combined_output.OrderByDescending(i => i.picking_probability).ToArray()
                                       select sorted;
            Console.Write("central_K: " + topk + "\n");
            Console.Write("Central server's output with uncertainty: \n");
            var result = final_sampled_output.SampledInference(topk).Support().ToArray();
            HashSet<ChosenDocument> result_set = new HashSet<ChosenDocument>();
            foreach (var r in result)
            {
                foreach (var v in r.Value)
                {
                    result_set.Add(v);
                }
            }

            foreach (var v in result_set)
            {
                Console.Write(v.field + " : " + v.picking_probability + "\n");
            }
            Console.Write("total results with uncertainty: " + result_set.Count + "\n");
            Console.Write("\n\nCentral server's output: \n");
            int c = 0;
            foreach (var val in score_probabilities.Values)
            {
                foreach (var k in val)
                {
                    c++;
                    Console.Write(k.Key + " : " + k.Value + "\n");
                }
            }
            Console.Write("total results without uncertainty: " + c + "\n");
            return result_set;
        }

        public static HashSet<Uncertain<ChosenDocument[]>> distributedSearch(string query, int topk, Dictionary<int, Dictionary<Field, double>> score_summaries, Dictionary<int, Dictionary<Field, double>> score_probabilities)
        {
            HashSet<Uncertain<ChosenDocument[]>> uncertain_documents = new HashSet<Uncertain<ChosenDocument[]>>();
            int machine = 1;
            // distribute search to available servers --- indexing and searching are both distributed. 
            foreach (var data_partition in data_partitions_for_distributed_search)
            {
                // f(x) = lambda*e^(-lambda*x) is the pdf of exponential distribution. We model the probability of picking a document
                // with a score x as an exponential distribution.
                // MLE of lambda for exponential distribution is the reciprocal of sample mean, where the sample is the reciprocals of the normalized scores generated by the servers.
                // smaller the value of the reciprocal, the larger the probability of picking it since the score is larger.
                double lambda_mle = 0.0;
                HashSet<double> unique_normalized_score_reciprocals = new HashSet<double>();
                string score_file = "scores" + machine.ToString() + ".txt";

                using (StreamWriter sw = new StreamWriter(score_file))
                {
                    Dictionary<Field, double> normalized_scores = new Dictionary<Field, double>();
                    Dictionary<Field, double> document_probabilities = new Dictionary<Field, double>();
                    Console.Write("\nMachine " + machine + " building indexes\n");
                    Index indexer = new Index();
                    indexer.rebuildIndex(data_partition.Value);
                    Console.Write("Building indexes done\n");
                    Console.Write("Machine " + machine + " performing search\n");
                    Search s = new Search();
                    TopDocs topDocs = s.performSearch(query, data_partition.Value.Count);
                    Console.Write("Results found: " + topDocs.TotalHits + "\n");
                    ScoreDoc[] hits = topDocs.ScoreDocs;
                    double sum_of_score_reciprocals = 0.0;
                    for (int x = 0; x < hits.Length; x++)
                    {
                        Document doc = s.getDocument(hits[x].Doc);
                        double normalized_score = hits[x].Score / topDocs.MaxScore;
                        // the minimum value of the reciprocal of a score is 1. To make the probabilities more meaningful, the origin is shifted to the right by 1. 
                        //double normalized_score_reciprocal = (topDocs.MaxScore / hits[x].Score)-1;                            
                        double normalized_score_reciprocal = (topDocs.MaxScore / hits[x].Score);
                        unique_normalized_score_reciprocals.Add(normalized_score_reciprocal);
                        sum_of_score_reciprocals = sum_of_score_reciprocals + normalized_score_reciprocal;
                        Console.Write(doc.GetField("Id") + " " + doc.GetField("Original title") + " " + doc.GetField("Normalized title") + " " + hits[x].Score);
                        Console.Write("\n");
                        normalized_scores.Add(doc.GetField("Id"), normalized_score);
                        sw.Write(normalized_score);
                        sw.Write(Environment.NewLine);
                    }
                    lambda_mle = unique_normalized_score_reciprocals.Count / sum_of_score_reciprocals;
                    var exp = new Microsoft.Research.Uncertain.Exponential(lambda_mle);

                    // probability associated with picking a document with a reciprocal score S is then lambda.e^(-lambda.S)                        
                    // the minimum value of the reciprocal of a score is 1. To make the probabilities more meaningful, the origin is shifted to the right by 1. 
                    foreach (var key in normalized_scores.Keys)
                    {
                        document_probabilities.Add(key, exp.Score(((1 / normalized_scores[key]) - 1)));
                    }
                    document_probabilities.OrderByDescending(entry => entry.Value);

                    Uncertain<ChosenDocument[]> selected_documents = from exponential in exp
                                                                     let docs = from entry in document_probabilities
                                                                                let chosen_doc = new ChosenDocument { field = entry.Key, picking_probability = entry.Value, exponential_bound = exponential }
                                                                                where entry.Value < exponential
                                                                                orderby chosen_doc.picking_probability descending
                                                                                select chosen_doc
                                                                     select docs.ToArray();

                    score_summaries.Add(machine, normalized_scores);
                    score_probabilities.Add(machine, document_probabilities);

                    uncertain_documents.Add(selected_documents.SampledInference(topk));
                    machine++;
                    Console.Write("Finished\n");
                    Console.Write("distributed_K: " + topk + "\n");
                }
            }
            return uncertain_documents;
        }

        private static double Score(double t, double mu, double stdev)
        {
            var a = 1.0 / (stdev * Math.Sqrt(2 * Math.PI));
            var b = Math.Exp(-Math.Pow(t - mu, 2) / (2 * stdev * stdev));
            return a * b;
        }

        private static long Factorial(long n)
        {
            if (n <= 1)
                return 1;
            else
            {
                return n * Factorial(n - 1);
            }
        }
        private static double BinomialScore(int n, int r, double p)
        {
            var combination = Factorial(n) / (Factorial(r) * Factorial(n - r));
            return combination * Math.Pow(p, r) * Math.Pow((1 - p), (n - r));
        }

        private static double T_Score(double t, long dof)
        {
            double numerator = 1.0, denominator = 1.0;
            double ret = 0.0;
            double factor = Math.Pow((1 + (Math.Pow(t, 2) / dof)), (-(dof + 1) / 2));
            if (dof <= 3)
            {
                if (dof == 1)
                    ret = 1 / (Math.PI * (1 + Math.Pow(t, 2)));
                if (dof == 2)
                    ret = 1 / (Math.Pow((2 + Math.Pow(t, 2)), 3 / 2));
                if (dof == 3)
                    ret = 6 * Math.Sqrt(3) / (Math.PI * Math.Pow((3 + Math.Pow(t, 2)), 2));
                return ret;
            }
            else
            {
                if (dof % 2 == 0)
                {
                    for (int x = 3; x <= dof - 1; x += 2)
                    {
                        numerator = numerator * x;
                    }
                    for (int y = 2; y <= dof - 2; y += 2)
                    {
                        denominator = denominator * y;
                    }
                    ret = factor * numerator / (denominator * 2 * Math.Sqrt(dof));
                }
                else
                {
                    for (int x = 3; x <= dof - 2; x += 2)
                    {
                        denominator = denominator * x;
                    }
                    for (int y = 2; y <= dof - 1; y += 2)
                    {
                        numerator = numerator * y;
                    }
                    ret = factor * numerator / (denominator * Math.PI * Math.Sqrt(dof));
                }
                return ret;
            }              
        }

        public struct TmpStruct : IEqualityComparer<TmpStruct>
        {
            public int k1, k2;
            public double yhatSqrd;

            public bool Equals(TmpStruct x, TmpStruct y)
            {
                return x.k1 == y.k1 && x.k2 == y.k2;
            }

            public int GetHashCode(TmpStruct obj)
            {
                return obj.k1.GetHashCode() ^ obj.k2.GetHashCode();
            }
        }

        public class MyTupleComparer : IComparer<Tuple<int, int>>
        {
            public int Compare(Tuple<int, int> x, Tuple<int, int> y)
            {
                return ((IComparable)x).CompareTo(y);
            }
        }

        public static Weighted<T> Create<T>(T sample, double prob) 
        {
            return new Weighted<T>() { Value = sample, Probability = prob };
        }
        public static void Main(string[] args)
        {  
            Func<int, Uncertain<double>> F = (k1) =>               
                  from a in new Gaussian(0,1).SampledInference(k1)
                  select a;               

            const double BernoulliP = 0.05;
            Func<int, Uncertain<int>> F1 = (k1) =>
                  from a in new Flip(BernoulliP).SampledInference(k1, null)
                  select Convert.ToInt32(a);

                    
            Func<int, double, Uncertain<double>, Tuple<int, double, List<Weighted<double>>>> TVariateGenerator = (k, population_mean, sample) =>
            {
                var all_values = sample.Inference().Support().ToList();              
                var sample_mean = all_values.Select(i => i.Value).Sum()/k;
                var sample_variance = all_values.Select(i => (i.Value - sample_mean) * (i.Value - sample_mean)).Sum() / (k - 1);
                var SEM = Math.Sqrt(sample_variance/k);
                var t_statistic = (sample_mean - population_mean) / SEM;
                return Tuple.Create(k, t_statistic, all_values);
            };
                                             
            Func<Uncertain<Uncertain<double>>, IEnumerable<Tuple<int, double, List<Weighted<double>>>>> SameSampleSizeBestProgramSampler = (p) =>
            {
                var samples = p.SampledInference(100000).Support().ToList();
                var t_variates = from sample in samples
                                 let t = TVariateGenerator(sample.Value.Inference().Support().ToList().Count, BernoulliP, sample.Value)
                                 select t;
                var sorted_tvariates = t_variates.OrderByDescending(i => StudentT.PDF(0, 1, i.Item1 - 1, i.Item2)).ToList();
                Dictionary<int, Tuple<double, List<Weighted<double>>>> best_samples_of_fixed_sizes = new Dictionary<int, Tuple<double, List<Weighted<double>>>>();
                for (int x = 0; x < sorted_tvariates.Count;x++ )
                {
                    if (!best_samples_of_fixed_sizes.Keys.Contains(sorted_tvariates[x].Item1))
                    {
                        best_samples_of_fixed_sizes.Add(sorted_tvariates[x].Item1, Tuple.Create(sorted_tvariates[x].Item2, sorted_tvariates[x].Item3));
                    }
                    else continue;
                }

                var max_likelihoods_for_each_sample_size = from best_sample_of_fixed_size in best_samples_of_fixed_sizes
                                                           select Tuple.Create(best_sample_of_fixed_size.Key, StudentT.PDF(0,1,best_sample_of_fixed_size.Key-1,best_sample_of_fixed_size.Value.Item1), best_sample_of_fixed_size.Value.Item2);                   
               
                return max_likelihoods_for_each_sample_size;
            };          

            Func<IEnumerable<Tuple<int, double, List<Weighted<double>>>>, int> BestKSelector = (best_samples_of_fixed_size) => 
            {
                int k = 0;
                int largest_sample_size = best_samples_of_fixed_size.OrderByDescending(i => i.Item1).ElementAt(0).Item1;

                List<Tuple<double, int, double, List<Weighted<double>>>> normalized_weights = new List<Tuple<double, int, double, List<Weighted<double>>>>();

                foreach (var tuple in best_samples_of_fixed_size)
                {
                    double ratio = (double)tuple.Item1 / (double)largest_sample_size;
                    var newTuple = Tuple.Create(ratio, tuple.Item1, tuple.Item2, tuple.Item3);
                    normalized_weights.Add(newTuple);
                }
                var ordered_list_according_to_likelihood = normalized_weights.OrderByDescending(i => Math.Pow(i.Item3,1) * Math.Sqrt(i.Item2 - 3) / (Math.Sqrt(i.Item2 - 1))); //proportional to product of likelihood and inversely proprotional to variance which is (dof/dof-2)  

                List<Tuple<double, double, int, double, List<Weighted<double>>>> utilities = new List<Tuple<double, double, int, double, List<Weighted<double>>>>();

                foreach (var tuple in ordered_list_according_to_likelihood)
                {
                    double utility = tuple.Item3 * Math.Sqrt(tuple.Item2 - 3) / (Math.Sqrt(tuple.Item2 - 1));
                    var newTuple = Tuple.Create(utility, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
                    utilities.Add(newTuple);
                }

                var ordered_utilities = utilities.OrderByDescending(i=>i.Item1);
                k = ordered_utilities.ElementAt(0).Item3;  
                return k;
            };

            var binomial_program = from k1 in new FiniteEnumeration<int>(new[] {10, 50, 75, 100,110})
                                   let a = F(k1)
                                   select a;

            var all_good_programs = SameSampleSizeBestProgramSampler(binomial_program);                     
            var bestK = BestKSelector(all_good_programs);    

            StreamReader datafile = new StreamReader(@"C:\Users\t-chnand\Desktop\Uncertainty\InferenceSemantics\SearchEngine\SearchEngine\dataset\Data1.txt");
            DataParser.ParseDataSet(datafile);
            data_partitions_for_distributed_search = CreateDataPartitions(SampleDataRepository.GetAll(), number_of_machines);
            string query = "learning";
            var distributed_k = new FiniteEnumeration<int>(Enumerable.Range(20, 5).ToList());
            var central_k = new FiniteEnumeration<int>(Enumerable.Range(10, 3).ToList());
            try
            {
                for (int times = 0; times < 1000; times++)
                {
                    var ks = from d_k in distributed_k
                             from c_k in central_k
                             let score_summaries = new Dictionary<int, Dictionary<Field, double>>()
                             let score_probabilities = new Dictionary<int, Dictionary<Field, double>>()
                             let uncertain_documents = new List<Uncertain<ChosenDocument[]>>()
                             let distributed_search = distributedSearch(query, d_k, score_summaries, score_probabilities)
                             let central_search = finalSearch(c_k, score_summaries, score_probabilities, distributed_search)
                             where CorrectnessCondition(score_probabilities, central_search) == true
                             select Tuple.Create(d_k, c_k);
                    var res = ks.Inference().Support().OrderByDescending(i => i.Probability);
                    foreach (var r in res)
                    {
                        Console.WriteLine(String.Format("{0} {1} {2}", r.Value.Item1, r.Value.Item2, r.Probability));
                        //Console.Write("top k values: " + r.Value.Item1 + " : " + r.Value.Item2 + "\n");
                    }
                }
            }
            catch (Exception e)
            {
                Console.Write("Search failed: " + e.GetType());
            }
            Console.ReadKey();
        }

        internal static bool CorrectnessCondition(Dictionary<int, Dictionary<Field, double>> score_probabilities, HashSet<ChosenDocument> result_set)
        {
            HashSet<ChosenDocument> score_probability_list = new HashSet<ChosenDocument>();
            foreach (var key in score_probabilities.Keys)
            {
                foreach (var key1 in score_probabilities[key].Keys)
                {
                    ChosenDocument document = new ChosenDocument { field = key1, exponential_bound = 0, picking_probability = score_probabilities[key][key1] };
                    score_probability_list.Add(document);
                }
            }

            bool same = true;
            foreach (var v in score_probability_list)
            {
                if (result_set.Contains(v))
                {
                    continue;
                }
                else
                {
                    same = false;
                    break;
                }
            }
            return same;
        }
    }
}