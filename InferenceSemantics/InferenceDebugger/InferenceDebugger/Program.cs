using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;

namespace InferenceDebugger
{
    class Program
    {
        static Debugger<int> intDebugger = new Debugger<int>();
        static Debugger<double> doubleDebugger = new Debugger<double>();
        public static void Main(string[] args)
        {
            for (int x = 0; x < 10; x++)
            {
                TestDebuggerContinuous();
                TestDebuggerDiscrete();
            }
            Console.Write("done");
            Console.ReadKey();
        }

        public static void TestDebuggerContinuous()
        {
            var hyper = from k1 in new FiniteEnumeration<int>(new[] { 150, 175, 200, 250, 275, 300, 350, 400, 500, 600 })
                        select k1;
            var topk = doubleDebugger.Debug(Example.F, Example.getMean(), hyper);
        }
        public static void TestDebuggerDiscrete()
        {
            var hyper = from k1 in new FiniteEnumeration<int>(new[] { 50, 75, 100, 200, 250 })
                        select k1;
            var topk = intDebugger.Debug(Example2.F1, Example2.getMean(), hyper);
        }
    }  
}
