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
        public static int number_of_machines= 3;   
        public static Dictionary<int, List<SampleData>> data_partitions_for_distributed_search = 
            new Dictionary<int, List<SampleData>>();

        static Dictionary<int, List<SampleData>> CreateDataPartitions (List<SampleData> dataset, int number_of_machines)
        {
            Dictionary<int, List<SampleData>> partitions = new Dictionary<int, List<SampleData>>();
            Dictionary<int, int> partition_sizes = new Dictionary<int, int>();
            int partition_size=0;

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

        static void Main(string[] args)
        {
           try
            {
                data_partitions_for_distributed_search = CreateDataPartitions(SampleDataRepository.GetAll(), number_of_machines);
                int machine=1;
                foreach (var data_partition in data_partitions_for_distributed_search)
                {
                    Console.Write("Machine " + machine + " building indexes\n");
                    Index indexer = new Index();
                    indexer.rebuildIndex(data_partition.Value);
                    Console.Write("Building indexes done\n");
                    Console.Write("Perform search\n");
                    Search s = new Search();
                    TopDocs topDocs = s.performSearch("Allahabad Seattle", 100);
                    Console.Write("Results found: " + topDocs.TotalHits + "\n");
                    ScoreDoc[] hits = topDocs.ScoreDocs;
                    for (int x = 0; x < hits.Length; x++)
                    {
                        Document doc = s.getDocument(hits[x].Doc);
                        Console.Write(doc.GetField("Id") + " " + doc.GetField("Name") + " " + doc.GetField("Description") + " " + hits[x].Score);
                        Console.Write("\n");
                    }
                    machine++;
                    Console.Write("Finished\n");
                }
               
            }
            catch (Exception e)
            {
                Console.Write("Exception" + e.GetType());
            }
            Console.ReadKey();
        }
    }
}
