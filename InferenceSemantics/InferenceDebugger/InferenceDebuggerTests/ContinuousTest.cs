using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;

using Microsoft.Research.Uncertain.InferenceDebugger;

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
            string file = "continuous_test.txt";
            Debugger<double> doubleDebugger = new Debugger<double>(0.01, 10, 600);
            var hyper = from k1 in Debugger<double>.truncatedGeometric
                        select Tuple.Create(k1, Debugger<double>.truncatedGeometric.Score(k1));
            var k = doubleDebugger.Debug(F, getMean(), hyper);
            Console.WriteLine(k);
            Assert.AreNotEqual(600, k);
        }
    }
}
