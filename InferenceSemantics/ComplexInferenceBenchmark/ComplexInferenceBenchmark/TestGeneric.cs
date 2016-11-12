using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.InferenceDebugger;
using Microsoft.Research.Uncertain.Inference;

namespace ComplexInferenceBenchmark
{
	public class TestGeneric<T>
	{
		T attr;

		public TestGeneric(T attr){	
			this.attr = attr;	
		}
	}
}

