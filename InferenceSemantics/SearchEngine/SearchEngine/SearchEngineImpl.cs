using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;

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
                    var exp = new Exponential(lambda_mle);

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



        public static void Main(string[] args)
        {
           
            Func<int,int,int,Uncertain<double>> F = (k1, k2, k3) =>
                from a in new Gaussian(0, 1).SampledInference(k1, null)
                from b in new Gaussian(0, 1).SampledInference(k2, null)
                from c in new Gaussian(0, 1).SampledInference(k3, null)
                select a + b + c;


            var tmpp = Enumerable.Range(20, 200).Select(i => new Weighted<int> { Value = i, Probability = 200 - i });
            var sum = tmpp.Select(i => i.Probability).Sum();
            tmpp = tmpp.Select(i => new Weighted<int> { Value = i.Value, Probability = i.Probability / sum });

            var program =
                from k1 in new FiniteEnumeration<int>(tmpp.ToList())
                from k2 in new FiniteEnumeration<int>(tmpp.ToList())
                from k3 in new FiniteEnumeration<int>(tmpp.ToList())
                from yhat in F(k1, k2, k3)
                let prob = Score(yhat, 0.0, 3.0)
                select new Weighted<Tuple<int, int, int, double>> { Value = Tuple.Create(k1, k2, k3, yhat * yhat), Probability = prob };

            var sampler = new MarkovChainMonteCarloSampler<Tuple<int, int, int, double>>(program);

            var best = double.NegativeInfinity;
            Tuple<int, int, int, double> bestItem = null;
            var count = 0;
            foreach(var item in sampler.Skip(1000))
            {
                if (item.Probability > best)
                {
                    best = item.Probability;
                    bestItem = item.Value;
                }

                if (count++ % 10000 == 0)
                {
                    Console.WriteLine(String.Format("{0} {1}", best, bestItem.Item4));
                }
            }

            //var tmp = program.SampledInference(10000).Support().OrderByDescending(i => i.Probability).Take(10).ToList();

            StreamReader datafile = new StreamReader(@"C:\Users\t-chnand\Desktop\Uncertainty\InferenceSemantics\SearchEngine\SearchEngine\dataset\Data1.txt");
            DataParser.ParseDataSet(datafile);
            data_partitions_for_distributed_search = CreateDataPartitions(SampleDataRepository.GetAll(), number_of_machines);
            string query = "learning";
            int new_d_k = 1;
            int new_c_k = 1;
            int delta = 3;
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