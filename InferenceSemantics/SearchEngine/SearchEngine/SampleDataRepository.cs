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
            };
        }
    }
}
