using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Version = Lucene.Net.Util.Version;
namespace SearchEngine
{
    public class Index
    {
        public Index()
        {

        }

        private IndexWriter indexWriter=null;

        public IndexWriter getIndexWriter(bool create)
        {
            if (indexWriter == null)
            {
                FSDirectory indexDir = FSDirectory.Open(new DirectoryInfo("index-directory"));
                indexWriter = new IndexWriter(indexDir, new StandardAnalyzer(Version.LUCENE_30), create, IndexWriter.MaxFieldLength.UNLIMITED); 
            }
            return indexWriter;
        }

        public void closeIndexWriter()
        {
            if (indexWriter != null)
            {
                indexWriter.Dispose();
            }
        }

        public void indexSampleData(SampleData sampleData)
        {
            Console.Write("Indexing sample data\n");            
            IndexWriter writer = getIndexWriter(true);
            Document doc = new Document();        
            doc.Add(new Field("Id", sampleData.Id.ToString(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Original title", sampleData.OriginalTitle.ToString(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Normalized title", sampleData.NormalizedTitle.ToString(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Year", sampleData.Year.ToString(), Field.Store.YES, Field.Index.ANALYZED));            
            string searchableText=sampleData.Id + " " + sampleData.OriginalTitle + " " + sampleData.NormalizedTitle 
                + " "+ sampleData.Year +" ";
            doc.Add(new Field("content",searchableText, Field.Store.NO, Field.Index.ANALYZED));
            writer.AddDocument(doc);
        }

        public void rebuildIndex(List<SampleData> dataset)
        {            
            foreach (SampleData data in dataset)
            {               
                indexSampleData(data);
            }
            closeIndexWriter();
        }
    }
}
