using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Research.Uncertain.Inference;
using Microsoft.Research.Uncertain;

using Microsoft.Research.Uncertain.InferenceDebugger;

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
        public static double getStddev()
        {
            return Math.Sqrt(BernoulliP*(1-BernoulliP));
        }

        [TestMethod]
        public void TestDiscrete()
        {
            Debugger<int> intDebugger = new Debugger<int>(0.001, 10, 1000);
            var hyper = from k1 in intDebugger.hyperParameterModel.truncatedGeometric
                        select Tuple.Create(k1, intDebugger.hyperParameterModel.truncatedGeometric.Score(k1));          
            var k = intDebugger.Debug(intDebugger.hyperParameterModel, F1, getMean(), getStddev(), hyper);
            Console.WriteLine(k);
            Assert.AreNotEqual(50, k);
        }
    }
}
