using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Research.Uncertain.Inference;
using Microsoft.Research.Uncertain;
using InferenceDebugger;

namespace InferenceDebuggerTests
{
    
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            Uncertain<double> gaussian1 = new Gaussian(0, 1);
            Uncertain<double> gaussian2 = new Gaussian(0, 1);
            var u_list1 = gaussian1.SampledInference(5);
            var u_list2 = gaussian2.SampledInference(5);
            var u_doubles = from bias in new FiniteEnumeration<double>(new[] { -0.01, 0.02, -0.1, 0.05, 0.03 })
                            from a in u_list1
                            from b in u_list2
                            select a + b + bias;

            // p(a,b,a+b)
            // p(a + b | a + b < 10, u_list1, u_list2)
            var p = from a in u_list1
                    from b in u_list2
                    where a + b < 10
                    select a + b;

            Visitor visitor = new Visitor();
            u_doubles.Accept(visitor);
            
        }

        public void TestMethod2()
        {
            var x = new Flip(0.1);
            var p = from a in x
                    from b in new Flip(0.9)
                    select a | b;

            Uncertain<bool> q = from i in p.Inference()
                                where i
                                from j in x
                                select j | i;
        }
    }
}
