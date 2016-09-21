using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchEngine
{
    public static class SampleDataRepository
    {
        private static List<SampleData> data_list=new List<SampleData>();
        public static SampleData Get(string id)
        {
            return GetAll().SingleOrDefault(x=>x.Id.Equals(id));
        }
        public static List<SampleData> GetAll()
        {
            return data_list;
        }
        public static void addData(SampleData data)
        {
            data_list.Add(data);          
        }
    }
}
