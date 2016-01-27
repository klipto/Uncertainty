using System;
using System.Linq;
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UncertainTests
{
    [TestClass]
    public class MCMCTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            var program = from a in new Flip(0.9)
                          from b in new Flip(0.9)
                          from c in new Flip(0.9)
                          where !a
                          select Convert.ToInt32(a) + Convert.ToInt32(b) + Convert.ToInt32(c);

            var sampler = new MarkovChainMonteCarloSampler<int>(program);

            var tmp = Microsoft.Research.Uncertain.Inference.Extensions.RunInference(sampler.Take(100000).ToList()).Support().ToList();

            var correct = program.Inference().Support().ToList();

            int x = 10;
        }

        [TestMethod]
        public void TestMethod4()
        {
            var program = from a in new Flip(0.9)
                          from b in new Flip(0.9)
                          from c in new Flip(0.9)
                          where b
                          let d = Convert.ToInt32(a) + Convert.ToInt32(b) + Convert.ToInt32(c)
                          select new Weighted<int>(d, a ? 0.1 : 1);

            var sampler = new MarkovChainMonteCarloSampler<int>(program);

            var tmp = Microsoft.Research.Uncertain.Inference.Extensions.RunInference(sampler.Take(100000).ToList()).Support().ToList();

            var correct = program.Inference().Support().ToList();

            int x = 10;
        }

        public Uncertain<int> Geometric(double p)
        {
            var program = from a in new Flip(p)
                          let count = a ? (Uncertain<int>)0 : Geometric(p)
                          from b in count
                          select 1 + b;
            return program;
        }

        [TestMethod]
        public void TestMethod2()
        {
            var program = from i in Geometric(0.9)
                          where i <= 5
                          select i;

            var sampler = new MarkovChainMonteCarloSampler<int>(program);
            var tmp = Microsoft.Research.Uncertain.Inference.Extensions.RunInference(sampler.Take(100000).ToList()).Support().ToList();

            int x = 10;
        }
    }
}
