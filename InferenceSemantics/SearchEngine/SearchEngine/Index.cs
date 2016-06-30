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
        private IndexWriter indexWriter;

        public IndexWriter getIndexWriter(Boolean create)
        {
            if (indexWriter == null)
            {
                FSDirectory indexDir = FSDirectory.Open(new DirectoryInfo("index-directory"));
                indexWriter = new IndexWriter(indexDir, new StandardAnalyzer(Version.LUCENE_30), IndexWriter.MaxFieldLength.UNLIMITED);                
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
            Console.Write("Indexing sample data:"+ sampleData+"\n");
            IndexWriter writer = getIndexWriter(false);
            Document doc = new Document();
            doc.Add(new Field("Id", sampleData.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("Name", sampleData.Name.ToString(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Description", sampleData.Description.ToString(), Field.Store.YES, Field.Index.ANALYZED));
            string searchableText=sampleData.Id + " " + sampleData.Name + " " + sampleData.Description;
            doc.Add(new Field("content",searchableText, Field.Store.NO, Field.Index.ANALYZED));
            writer.AddDocument(doc);
        }

        public void rebuildIndex()
        {
            getIndexWriter(true);
            List<SampleData> datas = SampleDataRepository.GetAll();
            foreach (SampleData data in datas)
            {
                indexSampleData(data);
            }
            closeIndexWriter();
        }

    }
}
