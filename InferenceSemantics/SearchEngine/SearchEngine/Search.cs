using System;
using System.Collections.Generic;
using System.Linq;

using System.Web;
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
    public class Search
    {
        private IndexSearcher searcher = null;
        private QueryParser parser = null;

        public Search()
        {
            searcher = new IndexSearcher(FSDirectory.Open(new DirectoryInfo("index-directory")), false);
            parser = new QueryParser(Version.LUCENE_30,"content", new StandardAnalyzer(Version.LUCENE_30));
        }
        public TopDocs performSearch(String query, int n)
        {
            Query q = parser.Parse(query);
            return searcher.Search(q, n);
        }
        public Document getDocument(int docID)
        {
            return searcher.Doc(docID);
        }
    }
}
