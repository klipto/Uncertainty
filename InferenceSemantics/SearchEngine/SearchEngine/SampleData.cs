using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchEngine
{
    public class SampleData
    {
        public SampleData()
        {

        }
        public string Id { get; set; }
        public string OriginalTitle { get; set; }
        public string NormalizedTitle { get; set; }
        public string Year { get; set; }
        public string Date { get; set; }
        public string DOI { get; set; }
        public string OriginalVenue { get; set; }
        public string NormalizedVenue { get; set; }
        public string JournalID { get; set; }
        public string ConferenceID { get; set; }
        public string PaperRank { get; set; }

    }
}
