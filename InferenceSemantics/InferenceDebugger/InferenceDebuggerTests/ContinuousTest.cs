using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;

using InferenceDebugger;

namespace InferenceDebuggerTests
{
    [TestClass]
    public class ContinuousTest
    {
        public static Func<int, Uncertain<double>> F = (k1) =>
               from a in new Gaussian(0, 1).SampledInference(k1)
               select a;
        public static double getMean()
        {
            return 0;
        }

        [TestMethod]
        public void TestContinuous()
        {
            Debugger<double> doubleDebugger = new Debugger<double>();
            var hyper = from k1 in new FiniteEnumeration<int>(new[] { 150, 175, 200, 250, 275, 300, 350, 400, 500, 600 })
                        select k1;
            var topk = doubleDebugger.Debug(F, getMean(), hyper);
            Assert.AreNotEqual(600, topk);
        }
    }
}
