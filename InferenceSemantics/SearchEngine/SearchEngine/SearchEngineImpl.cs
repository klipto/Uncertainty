using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;

using Microsoft.Research.Uncertain.InferenceDebugger;

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

        public static Tuple<HashSet<ChosenDocument>, HashSet<ChosenDocument>, HashSet<ChosenDocument>,List<ChosenDocument>, Dictionary<int, Dictionary<Field, double>>> finalSearch(Dictionary<int, Dictionary<Field, double>> score_summaries, Dictionary<int, Dictionary<Field, double>> score_probabilities,
            Dictionary<int, HashSet<ChosenDocument>> machine_specific_results)
        {            
            var s1 = machine_specific_results[1];
            var s2 = machine_specific_results[2];
            var s3 = machine_specific_results[3];

            var final_output = s1.Concat(s2).Concat(s3).ToList();
            List<ChosenDocument> all_results = new List<ChosenDocument>();
            foreach (var s in score_probabilities)
            {
                foreach (var val in s.Value)
                {
                    all_results.Add(new ChosenDocument { field = val.Key, picking_probability = val.Value});
                }
            }

            //Func<int, Uncertain<ChosenDocument[]>> F = (best_k) =>
            //    from a in final_sampled_output.SampledInference(best_k)
            //    select a;
            // Here is an example of why the debugger should be run on ERPs. It is hard to know the distribution of every function of ERPs and hence it is hard to know the mean.
            //var best_k = new UncertainTDebugger.Debugger<double>().Debug(, ___ , c_k);       
     
            return Tuple.Create(s1, s2, s3, final_output,score_probabilities);
        }

        public static Dictionary<int, HashSet<ChosenDocument>> distributedSearch(string query, Dictionary<int, Dictionary<Field, double>> score_summaries, Dictionary<int, Dictionary<Field, double>> score_probabilities)
        {            
            Dictionary<int, HashSet<ChosenDocument>> machine_result_map = new Dictionary<int, HashSet<ChosenDocument>>();            
            int machine = 1;
            // distribute search to available servers --- indexing and searching are both distributed. 
            foreach (var data_partition in data_partitions_for_distributed_search)
            {
                HashSet<ChosenDocument> topk_uncertain_documents = new HashSet<ChosenDocument>();
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
                    Dictionary<Field, double> probabilities_documents = new Dictionary<Field, Double>();
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

                     string file = "searchengine_probabilities.txt";
                     using (StreamWriter sw1 = new StreamWriter(file))
                     {
                       int counter = 1;
                        foreach (var key in normalized_scores.Keys)
                        {

                            document_probabilities.Add(key, exp.Score(((double)(1 / normalized_scores[key]) - 1)));
                            if (!probabilities_documents.ContainsValue(exp.Score(((double)(1 / normalized_scores[key]) - 1))))
                            {
                                probabilities_documents.Add(key, exp.Score(((double)(1 / normalized_scores[key]) - 1)));
                            }
                        }
                        foreach (var k in probabilities_documents.Keys)
                        {
                            if (machine == 1)
                            {
                                sw1.WriteLine(counter + " " + probabilities_documents[k]);
                                counter++;
                            }
                        }
                    }
                    document_probabilities.OrderByDescending(entry => entry.Value); // finding the scores with maximum likelihood.  
                    probabilities_documents.OrderByDescending(entry => entry.Value);
                    score_summaries.Add(machine, normalized_scores);
                    //score_probabilities.Add(machine, document_probabilities);
                    score_probabilities.Add(machine, probabilities_documents);
                    if (document_probabilities.Count > 2)
                    {
                        //topk_uncertain_documents = TopkDocumentSelector(exp, document_probabilities);
                        
                        topk_uncertain_documents = TopkDocumentSelector(exp, probabilities_documents);
                    }
                    //else if (document_probabilities.Count <= 2) // if there are very few matches, then return them all (this is a hack for now).
                    //{
                    //    foreach (var doc in document_probabilities)
                    //    {
                    //        topk_uncertain_documents.Add(new ChosenDocument {field = doc.Key, picking_probability = doc.Value});
                    //    }
                    //}                   
                    machine_result_map.Add(machine, topk_uncertain_documents);
                    machine++;
                    Console.Write("Finished\n");
                }
            }
            return machine_result_map;
        }
        public static HashSet<ChosenDocument> TopkDocumentSelector(Microsoft.Research.Uncertain.Exponential exp, Dictionary<Field, double> document_probabilities, int atleast_top = 10)
        {
            HashSet<double> probabilities = new HashSet<double>();
            HashSet<ChosenDocument> uncertain_documents = new HashSet<ChosenDocument>();
            
            Debugger<double> doubleDebugger = new Debugger<double>(0.001, 1, document_probabilities.Count);
            Func<int, Uncertain<double>> F = (k1) =>
               from a in exp.SampledInference(k1)
               select a;
            var hyper = from k in doubleDebugger.hyperParameterModel.truncatedGeometric
                        select Tuple.Create(k, doubleDebugger.hyperParameterModel.truncatedGeometric.Score(k));
            //var topk = doubleDebugger.DebugSampleSize(doubleDebugger.hyperParameterModel, F, 1 / (double)exp.Score(0), hyper);            
            //var topk = doubleDebugger.DebugTopkWithRange(doubleDebugger.hyperParameterModel, F, 0 ,1 / (double)exp.Score(0), hyper, exp, 1);
            var topk = doubleDebugger.DebugTopk(doubleDebugger.hyperParameterModel, F, 0, 1 / (double)exp.Score(0), hyper, exp);            
            if (topk.Item1 > 0) 
            {
                for (int x = 0; x < topk.Item1; x++)
                {
                    uncertain_documents.Add(new ChosenDocument { field = document_probabilities.ElementAt(x).Key, picking_probability = document_probabilities.ElementAt(x).Value });
                }
            }
            return uncertain_documents;
        }
        public static void Main(string[] args)
        {
            StreamReader datafile = new StreamReader(@"C:\Users\t-chnand\Desktop\Uncertainty\InferenceSemantics\SearchEngine\SearchEngine\dataset\Data.txt");
            DataParser.ParseDataSet(datafile);
            List<Tuple<string, int, HashSet<ChosenDocument>, HashSet<ChosenDocument>, HashSet<ChosenDocument>, Dictionary<int, Dictionary<Field, double>>>> result_count = 
                new List<Tuple<string, int, HashSet<ChosenDocument>, HashSet<ChosenDocument>, HashSet<ChosenDocument>, Dictionary<int, Dictionary<Field, double>>>>();
            data_partitions_for_distributed_search = CreateDataPartitions(SampleDataRepository.GetAll(), number_of_machines);
            string[] queries = { "algorithm", "artificial" , "machine"  ,"inference", "statistical"};
            foreach (var query in queries)
            {
                try
                {
                    var score_summaries = new Dictionary<int, Dictionary<Field, double>>();
                    var score_probabilities = new Dictionary<int, Dictionary<Field, double>>();                    
                    var distributed_search = distributedSearch(query, score_summaries, score_probabilities);
                    var central_search = finalSearch(score_summaries, score_probabilities, distributed_search);
                   // result_count.Add(Tuple.Create(query, central_search.Item1.Count, central_search.Item2, central_search.Item3, central_search.Item4, central_search.Item5));
                }
                catch (Exception e)
                {
                    Console.Write("Search failed: " + e.GetType());
                }
            }
            Console.ReadKey();
        }        
    }
}