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

        public static HashSet<ChosenDocument> finalSearch(Dictionary<int, Dictionary<Field, double>> score_probabilities, Dictionary<int, Dictionary<Field, double>> score_summaries,
            HashSet<Uncertain<ChosenDocument[]>> uncertain_documents)
        {
            var c_k = from k in new FiniteEnumeration<int>(new[] { 10, 11, 12 })
                      select k;
            var s1 = uncertain_documents.ElementAt(0);
            var s2 = uncertain_documents.ElementAt(1);
            var s3 = uncertain_documents.ElementAt(2);

            var final_sampled_output = from o1 in s1
                                       from o2 in s2
                                       from o3 in s3
                                       let combined_output = o1.Concat(o2).Concat(o3)
                                       let sorted = combined_output.OrderByDescending(i => i.picking_probability).ToArray()
                                       select sorted;
            
            //Func<int, Uncertain<ChosenDocument[]>> F = (best_k) =>
            //    from a in final_sampled_output.SampledInference(best_k)
            //    select a;
            // Here is an example of why the debugger should be run on ERPs. It is hard to know the distribution of every function of ERPs and hence it is hard to know the mean.
            //var best_k = new UncertainTDebugger.Debugger<double>().Debug(, ___ , c_k);

            Console.Write("Central server's output with uncertainty: \n");
            var result = final_sampled_output.Inference().Support().ToArray();
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

        public static HashSet<Uncertain<ChosenDocument[]>> distributedSearch(string query, Dictionary<int, Dictionary<Field, double>> score_summaries, Dictionary<int, Dictionary<Field, double>> score_probabilities)
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
                        //the minimum value of the reciprocal of a score is 1. To make the probabilities more meaningful, the origin is shifted to the right by 1. 
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
                    document_probabilities.OrderByDescending(entry => entry.Value); // finding the maximum likelihood.                     
                    uncertain_documents = UncertainDocumentSelector(exp, document_probabilities);
                    score_summaries.Add(machine, normalized_scores);
                    score_probabilities.Add(machine, document_probabilities);
                    machine++;
                    Console.Write("Finished\n");                    
                }
            }
            return uncertain_documents;
        }
        public static HashSet<Uncertain<ChosenDocument[]>> UncertainDocumentSelector(Microsoft.Research.Uncertain.Exponential exp, Dictionary<Field, double> document_probabilities)
        {
            HashSet<Uncertain<ChosenDocument[]>> uncertain_documents = new HashSet<Uncertain<ChosenDocument[]>>();
            var d_k = from k in new FiniteEnumeration<int>(new[] { 20, 50, 75, 100, 200, 250 })
                      select k;   
            Uncertain<ChosenDocument[]> selected_documents = from exponential in exp
                                                             let docs = from entry in document_probabilities
                                                                        let chosen_doc = new ChosenDocument { field = entry.Key, picking_probability = entry.Value, exponential_bound = exponential }
                                                                        where entry.Value < exponential
                                                                        orderby chosen_doc.picking_probability descending
                                                                        select chosen_doc
                                                             select docs.ToArray();
            Func<int, Uncertain<ChosenDocument[]>> F = (best_k) => 
                from a in selected_documents.SampledInference(best_k)
                select a;
            var best_d_k = new InferenceDebugger.Debugger<double>().Debug(F,1/exp.Score(0),d_k); // using sample mean to estimate the population mean.
            uncertain_documents.Add(selected_documents.SampledInference(best_d_k));
            return uncertain_documents;
        }
        public static void Main(string[] args)
        {
            StreamReader datafile = new StreamReader(@"C:\Users\t-chnand\Desktop\Uncertainty\InferenceSemantics\SearchEngine\SearchEngine\dataset\Data1.txt");
            DataParser.ParseDataSet(datafile);
            data_partitions_for_distributed_search = CreateDataPartitions(SampleDataRepository.GetAll(), number_of_machines);
            string query = "learning";
            try
            {           
                var score_summaries = new Dictionary<int, Dictionary<Field, double>>();
                var score_probabilities = new Dictionary<int, Dictionary<Field, double>>();
                var uncertain_documents = new List<Uncertain<ChosenDocument[]>>();
                var distributed_search = distributedSearch(query, score_summaries, score_probabilities);
                var central_search = finalSearch(score_summaries, score_probabilities, distributed_search);                
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