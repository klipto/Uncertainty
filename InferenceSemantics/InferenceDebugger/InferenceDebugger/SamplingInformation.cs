using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
namespace InferenceDebugger
{
    public class SamplingInformation
    {
        public SamplingInformation(Object obj)
        {
            this.Object = obj;          
        }
        public Object Object;  
    }
}
