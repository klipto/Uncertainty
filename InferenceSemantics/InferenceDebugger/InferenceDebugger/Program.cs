using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using System.Linq.Expressions;

namespace InferenceDebugger
{
    class Program
    {
        // works for only two unknows top-k values right now.
        public static IEnumerable<Weighted<Tuple<R, R>>> Debug<R>(Func<R, R, Uncertain<double>> program, Uncertain<R> param1,
            Uncertain<R> param2, Func<Uncertain<double>, bool> CorrectnessCondition)
        {
            var Ks = from k1 in param1
                     from k2 in param2
                     let prog = program(k1, k2)
                     where CorrectnessCondition(prog) == true
                     select Tuple.Create(k1, k2);
            var result = Ks.Inference().Support().OrderByDescending(i => i.Probability).ToList();
            return result;
        }

        public static void TestDebugger1()
        {
            var hyper1 = new FiniteEnumeration<int>(Enumerable.Range(10, 5).ToList());
            var hyper2 = new FiniteEnumeration<int>(Enumerable.Range(10, 5).ToList());
            var debug = Program.Debug(Example.P, hyper1, hyper2, Example.CorrectnessCondition);
            foreach (var d in debug)
            {
                Console.Write(d.Value + "\n");
            }
        }
      
        static void Main(string[] args)
        {
            TestDebugger1();        
            Console.ReadKey();
        }
    }
}
