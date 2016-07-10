using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SearchEngine
{
    class DataParser
    {
        public static void ParseDataSet(StreamReader file)
        {
            string line;            
            while ((line = file.ReadLine()) != null)
            {
                List<string> fields = new List<string>();
                string field = "";
                int x = 0;
                while (x < line.Length)
                {
                    if (!(line.ElementAt(x).Equals('\t')))
                    {
                        field = field + line.ElementAt(x);
                    }
                    else if (line.ElementAt(x).Equals('\t'))
                    {
                        try
                        {
                            fields.Add(field);                          
                        }
                        catch (Exception e)
                        {
                            Console.Write("Cannot add data to fields: " + e+"\n");
                        }
                        field = null;
                    }
                    x++;
                }
                fields.Add(field);
                SampleData data = new SampleData();
                data.Id = fields[0];
                try
                {
                    data.OriginalTitle = fields[1];
                    data.NormalizedTitle = fields[2];
                    data.Year = fields[3];                   
                }
                catch (Exception e) 
                {
                    Console.Write(e.GetType()+"\n"); 
                }
                SampleDataRepository.addData(data);
            }
          
        }
    }
}
