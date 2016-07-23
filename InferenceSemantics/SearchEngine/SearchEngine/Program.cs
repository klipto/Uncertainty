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
    class Program
    {
        private static int number_of_machines = 3;
        // This is used by the  "central server" to prune the results of the other servers and return the final top-k.      
        private static Dictionary<int, List<SampleData>> data_partitions_for_distributed_search = new Dictionary<int, List<SampleData>>();

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

        struct DocumentStruct : IComparable<DocumentStruct>
        {
            public string id;
            public double score;

            public int CompareTo(DocumentStruct other)
            {
                var a = Tuple.Create(this.id, this.score);
                var b = Tuple.Create(other.id, other.score);
                return Comparer<Tuple<string, double>>.Default.Compare(a, b);
            }
        }

        public static void finalSearch(Dictionary<int, Dictionary<Field, double>> score_summaries, Weighted<int> top_k, StreamWriter sw)
        {
            Dictionary<string, double> all_scores = new Dictionary<string, double>();
            foreach (var key in score_summaries.Keys)
            {
                foreach (var key1 in score_summaries[key].Keys)
                {
                    all_scores.Add((key.ToString() + ":" + key1.ToString()), score_summaries[key][key1]);
                }
            }
            var sorted_scores = all_scores.OrderByDescending(val => val.Value);
            Dictionary<string, double> final_scores = sorted_scores.ToDictionary(p => p.Key, p => p.Value);
            Console.Write("Central server's output: \n");
            int count = 0;
            int first_server = 0;
            int second_server = 0;
            int third_server = 0;

            foreach (var key in final_scores.Keys)
            {
                if (count < top_k.Value)
                {
                    count++;
                    Console.Write(key + " : " + final_scores[key] + "\n");
                    //sw.Write(key + " : " + final_scores[key]);
                    //sw.Write(Environment.NewLine);
                    int colon_index = key.IndexOf(':');
                    if (key.Substring(0, 1).Equals("1"))
                    {
                        first_server++;
                    }
                    else if (key.Substring(0, 1).Equals("2"))
                    {
                        second_server++;
                    }
                    else if (key.Substring(0, 1).Equals("3"))
                    {
                        third_server++;
                    }
                }
            }

            Console.Write("no of top documents: " + count + "\n");
            sw.Write("no of top documents: " + top_k.Value + Environment.NewLine);
            sw.Write("no of top documents from server1: " + first_server + " no of top documents froms server2: " + second_server + " no of top documents from server3: " + third_server + Environment.NewLine);
        }

        static void Main(string[] args)
        {
            uncertain_search();
        }

        public static void uncertain_search()
        {
            StreamReader datafile = new StreamReader(@"C:\Users\t-chnand\Desktop\Uncertainty\InferenceSemantics\SearchEngine\SearchEngine\dataset\Data1.txt");
            DataParser.ParseDataSet(datafile);
            Dictionary<int, Dictionary<Field, double>> score_summaries = new Dictionary<int, Dictionary<Field, double>>();
            Dictionary<int, Dictionary<Document, double>> score_probabilities = new Dictionary<int, Dictionary<Document, double>>();
            Dictionary<int, Uncertain<DocumentStruct[]>> machine_document_map = new Dictionary<int, Uncertain<DocumentStruct[]>>();
            try
            {
                data_partitions_for_distributed_search = CreateDataPartitions(SampleDataRepository.GetAll(), number_of_machines);
                int machine = 1;
                string query = "learning";
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
                        Dictionary<Document, double> normalized_scores_documents = new Dictionary<Document, double>();
                        Dictionary<Field, double> normalized_scores = new Dictionary<Field, double>();
                        Dictionary<Document, double> document_probabilities = new Dictionary<Document, double>();
                        List<DocumentStruct> docs = new List<DocumentStruct>();
                        Console.Write("\nMachine " + machine + " building indexes\n");
                        Index indexer = new Index();
                        indexer.rebuildIndex(data_partition.Value);
                        Console.Write("Building indexes done\n");
                        Console.Write("Machine " + machine + " performing search\n");
                        Search s = new Search();
                        TopDocs topDocs = s.performSearch(query, data_partition.Value.Count);
                        Console.Write("Results found: " + topDocs.TotalHits + "\n");
                        ScoreDoc[] hits = topDocs.ScoreDocs.OrderByDescending(p => p.Score).ToArray();
                        double sum_of_score_reciprocals = 0.0;

                        //servers return the top 10 of the hits
                        for (int x = 0; x < hits.Length; x++)
                        {
                            Document doc = s.getDocument(hits[x].Doc);
                            double normalized_score = hits[x].Score / topDocs.MaxScore;
                            // the minimum value of the reciprocal of a score is 1. To make the probabilities more meaningful, the origin is shifted to the right by 1. 
                            double normalized_score_reciprocal = (topDocs.MaxScore / hits[x].Score) - 1;
                            unique_normalized_score_reciprocals.Add(normalized_score_reciprocal);
                            sum_of_score_reciprocals = sum_of_score_reciprocals + normalized_score_reciprocal;
                            Console.Write(doc.GetField("Id") + " " + hits[x].Score);
                            Console.Write("\n");

                            normalized_scores.Add(doc.GetField("Id"), normalized_score);
                            normalized_scores_documents.Add(doc, normalized_score);

                            var doc_struct = new DocumentStruct { id = doc.GetField("Id").ToString(), score = normalized_score };

                            docs.Add(doc_struct);
                            sw.Write(normalized_score);
                            sw.Write(Environment.NewLine);
                        }
                        docs.OrderByDescending(i => i.score);

                        var u_docs = from noise in new Gaussian(0, 1)
                                     let document_structs = from d in docs
                                                            let u_doc = new DocumentStruct { id = d.id, score = d.score + Math.Abs(noise) }
                                                            orderby u_doc.score descending
                                                            select u_doc
                                     select document_structs.ToArray();

                        var list=u_docs.SampledInference(5).Support().OrderByDescending(p => p.Probability).ToList();

                        foreach (var l in list)
                        {
                            foreach (var val in l.Value)
                            {
                                Console.Write("value::: "+val.id + " : "+val.score + "\n");
                            }
                        }
                        
                        machine_document_map.Add(machine, u_docs);

                        //var all_douments= from m in machine_document_map 

                        //lambda_mle = unique_normalized_score_reciprocals.Count / sum_of_score_reciprocals;
                        Exponential exponential = new Exponential(lambda_mle);

                        // probability associated with picking a document with a reciprocal score S is then lambda.e^(-lambda.S)                        
                        // the minimum value of the reciprocal of a score is 1. To make the probabilities more meaningful, the origin is shifted to the right by 1. 
                        foreach (var key in normalized_scores_documents.Keys)
                        {
                            document_probabilities.Add(key, exponential.Score(((1 / normalized_scores_documents[key]) - 1)));
                        }

                        // order the documents by decreaing probability values.
                        document_probabilities.OrderByDescending(i => i.Value);
                        Uncertain<Dictionary<Document, double>> u_document_probabilities = (Uncertain<Dictionary<Document, double>>)document_probabilities;

                        //var v=u_document_probabilities.Inference().Support().ToList();
                        //foreach (var val in v)
                        //{
                        //  Console.Write("VALUE: "+ val.Value+"\n");
                        //}

                        score_summaries.Add(machine, normalized_scores);
                        score_probabilities.Add(machine, document_probabilities);
                        machine++;
                        Console.Write("Finished\n");
                    }
                }

                // final search in the "central server" using results from the other servers
                int total_documents_from_all_servers = 0;
                foreach (var m in score_summaries.Keys)
                {
                    total_documents_from_all_servers = total_documents_from_all_servers + score_summaries[m].Count;
                }                                                                       
                    

                Uncertain<int> top_k = new Uniform<int>(1, total_documents_from_all_servers + 1);
                var lst = top_k.Inference().Support().ToList();
                string file = "result.txt";
                using (StreamWriter sw = new StreamWriter(file))
                {
                    // foreach (var topk in list)
                    //{
                    //  finalSearch(score_summaries, topk, sw);
                    //}
                }
            }
            catch (Exception e)
            {
                Console.Write("Search failed: " + e.GetType());
            }
            Console.ReadKey();
        }
    }
}
