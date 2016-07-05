using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Research.Uncertain;

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
        private static double desired_fraction_of_max_score = 0.7;

        private static Dictionary<int, List<SampleData>> data_partitions_for_distributed_search =
            new Dictionary<int, List<SampleData>>();

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

        public static void finalSearch(Dictionary<int, Dictionary<Field, double>> score_summaries)
        {
            foreach (var key in score_summaries.Keys)
            {
                foreach (var key1 in score_summaries[key].Keys)
                {
                    if (score_summaries[key][key1] >= desired_fraction_of_max_score) 
                   {
                       Console.Write(key1 + " : " + score_summaries[key][key1] + "\n");
                   }
                }
            }
        }

        static void Main(string[] args)
        {
            Dictionary<int, Dictionary<Field, double>> score_summaries = new Dictionary<int, Dictionary<Field, double>>();
            
            try
            {
                data_partitions_for_distributed_search = CreateDataPartitions(SampleDataRepository.GetAll(), number_of_machines);
                int machine = 1;

                // distribute search to available servers
                foreach (var data_partition in data_partitions_for_distributed_search)
                {
                    Dictionary<Field, double> score_ratios = new Dictionary<Field, double>();

                    Console.Write("Machine " + machine + " building indexes\n");
                    Index indexer = new Index();
                    indexer.rebuildIndex(data_partition.Value);
                    Console.Write("Building indexes done\n");
                    Console.Write("Machine " + machine + " performing search\n");
                    Search s = new Search();


                    TopDocs topDocs = s.performSearch("Allahabad Seattle", 5);
                    Console.Write("Results found: " + topDocs.TotalHits + "\n");

                    ScoreDoc[] hits = topDocs.ScoreDocs;
                    for (int x = 0; x < hits.Length; x++)
                    {
                        Document doc = s.getDocument(hits[x].Doc);
                        double score_ratio = hits[x].Score / topDocs.MaxScore;
                        Console.Write(doc.GetField("Id") + " " + doc.GetField("Name") + " " + doc.GetField("Description") + " " + hits[x].Score);
                        Console.Write("\n");
                        score_ratios.Add(doc.GetField("Id"), score_ratio);
                    }
                    score_summaries.Add(machine, score_ratios);
                    machine++;
                    Console.Write("Finished\n");

                }

                // final search in the "central server" using results from the other servers
                finalSearch(score_summaries);
            }
            catch (Exception e)
            {
                Console.Write("Exception" + e.GetType());
            }
            Console.ReadKey();
        }
    }
}
