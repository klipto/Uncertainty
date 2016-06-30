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
        static void Main(string[] args)
        {
            try
            {
                Console.Write("Building indexes\n");
                Index indexer = new Index();
                indexer.rebuildIndex();
                Console.Write("Rebuilding done\n");

                Console.Write("Perform search\n");
                Search s = new Search();
                TopDocs topDocs = s.performSearch("Florida", 100);
                Console.Write("Results found: " + topDocs.TotalHits+ "\n");
                ScoreDoc[] hits = topDocs.ScoreDocs;
                for(int x=0;x<hits.Length;x++) {
                    Document doc = s.getDocument(hits[x].Doc);
                    Console.Write(doc.GetField("Id")+ " "+ doc.GetField("Name")+ " "+doc.GetField("Description") + " " + hits[x].Score);
                    Console.Write("\n");
                }
                Console.Write("Finished\n");
            }
            catch (Exception e)
            {
                Console.Write("Exception");
            }
            Console.ReadKey();
        }
    }

}
