using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Research.Uncertain.Inference;
using Microsoft.Research.Uncertain;

using InferenceDebugger;

namespace InferenceDebuggerTests
{
    [TestClass]
    public class DiscreteTest
    {
        const double BernoulliP = 0.05;

        public static Func<int, Uncertain<int>> F1 = (k1) =>
              from a in new Flip(BernoulliP).SampledInference(k1)
              select Convert.ToInt32(a);

        public static double getMean()
        {
            return BernoulliP;
        }

        [TestMethod]
        public void TestDiscrete()
        {
            Debugger<int> intDebugger = new Debugger<int>();
            var hyper = from k1 in new FiniteEnumeration<int>(new[] { 50, 75, 100, 200, 250 })
                        select k1;
            var topk = intDebugger.Debug(F1, getMean(), hyper);
            Assert.AreNotEqual(50, topk);
        }
    }
}
