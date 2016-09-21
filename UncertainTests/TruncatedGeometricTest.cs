using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace UncertainTests
{
    [TestClass]
    public class TruncatedGeometricTest
    {
        [TestMethod]
        public void Truncated_Sample2()
        {
            var s = new TruncatedGeometric(0.01, 10, 100);
            Assert.IsFalse(s.GetSample() <= 10);
        }
        [TestMethod]
        public void testTruncated_Sample() 
        {
            var s = new Geometric(0.01).SampledInference(100).Support().ToList();
           
            var sampler = new TruncatedGeometric(0.01, 10, 100).SampledInference(100).Support().ToList();            
            //foreach (var sample in sampler.Take(1000).OrderByDescending(i => i.Value))
            //{                
            //    Console.WriteLine(sample.Value);
            //}
            Func<int, Uncertain<double>> F = (k1) =>
               from a in new Gaussian(0, 1).SampledInference(k1)
               select a;

            var tmp = from k in sampler                      
                      select k.Value;

            var tmpp = from t in tmp.OrderByDescending(i=>i)
                       select F(t);

            var d = from t in tmpp
                    select t.Inference().Support().ToList();
        }
    }
}
