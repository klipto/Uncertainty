using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchEngine
{
    public static class SampleDataRepository
    {
        public static SampleData Get(int id)
        {
            return GetAll().SingleOrDefault(x=>x.Id.Equals(id));
        }
        public static List<SampleData> GetAll()
        {
            return new List<SampleData> {
                new SampleData {Id=1, Name="Seattle", Description="City in Washington"}, 
                new SampleData {Id=2, Name="Buffalo", Description="City in New York"}, 
                new SampleData {Id=3, Name="San francisco", Description="City in California"}, 
                new SampleData {Id=4, Name="San Hose", Description="City in California"}, 
                new SampleData {Id=5, Name="San Diego", Description="City in California"}, 
                new SampleData {Id=6, Name="St Petergburg", Description="City in Florida"}, 
                new SampleData {Id=7, Name="Pittsburgh", Description="City in Pennsylvania"}, 
                new SampleData {Id=8, Name="Tampa", Description="City in Florida"}, 
                new SampleData {Id=9, Name="Jupiter", Description="City in Florida"}, 
                new SampleData {Id=10, Name="Austin", Description="City in Texas"},
                new SampleData {Id=11, Name="Dallas", Description="City in Texas"},
                new SampleData {Id=12, Name="Houston", Description="City in Texas"},
                new SampleData {Id=13, Name="Phoenix", Description="City in Arizona"},
                new SampleData {Id=14, Name="Calcutta", Description="City in West Bengal"},
                new SampleData {Id=15, Name="Allahabad", Description="City in Uttar Pradesh"},
                new SampleData {Id=16, Name="Varanasi", Description="City in Uttar Pradesh"},
                new SampleData {Id=17, Name="Lucknow", Description="City in Uttar Pradesh"},
                new SampleData {Id=18, Name="Paris", Description="City in France"},
                new SampleData {Id=19, Name="Montreal", Description="City in Quebec"},                
                new SampleData {Id=20, Name="Vancouver", Description="City in British Columbia"},
                new SampleData {Id=21, Name="aaa", Description="City in bde"},
                new SampleData {Id=22, Name="def", Description="City in xyz"},
                new SampleData {Id=23, Name="pqr", Description="City in tuv"},
                new SampleData {Id=24, Name="bbb", Description="City in fff"},
                new SampleData {Id=25, Name="ghg", Description="City in tuv"},
                new SampleData {Id=26, Name="ttu", Description="City in ghkkk"},
                new SampleData {Id=27, Name="iii", Description="City in kku"},
                new SampleData {Id=28, Name="Alt", Description="Hotel in Montreal"},
                new SampleData {Id=29, Name="Hilton", Description="Hotel in Seattle"},
                new SampleData {Id=30, Name="Dallas", Description="City in Texas"},
                new SampleData {Id=31, Name="Allahabad", Description="is in the northern part of India"},
                new SampleData {Id=32, Name="Fact", Description="Allahabad is a place in India"},
                new SampleData {Id=33, Name="Allahabad", Description="Known for Sangam"},
                new SampleData {Id=34, Name="Fact", Description="Allahabad is great"},
                new SampleData {Id=35, Name="Allahabad", Description="City in Uttar Pradesh"},
            };
        }
    }
}
