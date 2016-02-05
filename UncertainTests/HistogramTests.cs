using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Histogram;
using System.Collections.Generic;

namespace UncertainTests
{
    [TestClass]
    public class HistogramTests
    {
        [TestMethod]
        public void Test_Flattening()
        {
            int[] values = { 1, 2, 3 };
            double[] probs = { 0.25, 0.5, 0.25 };
            var someDist = new Multinomial<int>(values, probs);
            var someHist = Histogram.flatten(someDist);

            // Check the resulting support.
            Assert.AreEqual(0.0, someHist.Score(partial<int>.Other), 0.01);
            Assert.AreEqual(0.25, someHist.Score(partial<int>.NewTop(1)), 0.01);
            Assert.AreEqual(0.5, someHist.Score(partial<int>.NewTop(2)), 0.01);
            Assert.AreEqual(0.25, someHist.Score(partial<int>.NewTop(3)), 0.01);
        }

        public static double doubler(double x) {
            return 2.0 * x;
        }

        [TestMethod]
        public void Test_Lifting()
        {
            // You can lift C# lambdas.
            var liftedLambda = CSLifting.lift<double, double>(x => 2.0 * x);

            // You can also, apparently, pass static methods in the same way.
            // C#'s type inference is evidently not strong enough to figure
            // out the Func<> type parameters, alas, so we need type
            // annotations in both cases.
            var liftedStaticMethod = CSLifting.lift<double, double>(doubler);

            // Apply a lifted function to a distribution and flatten it back to
            // a histogram.
            double[] values = { 2.0, 3.0 };
            double[] probs = { 0.6, 0.4 };
            var hist = Histogram.flatten(new Multinomial<double>(values, probs));
            var doubledDist = liftedLambda(hist);
            var doubledHist = Histogram.reflatten(doubledDist);

            // The resulting values should now be doubled.
            Assert.AreEqual(0.0, doubledHist.Score(partial<double>.Other), 0.01);
            Assert.AreEqual(0.6, doubledHist.Score(partial<double>.NewTop(4.0)), 0.01);
            Assert.AreEqual(0.4, doubledHist.Score(partial<double>.NewTop(6.0)), 0.01);
        }

        [TestMethod]
        public void Test_Sampled_Flattening()
        {
            int[] values = { 10, 11, 15 };
            double[] probs = { 0.7, 0.2, 0.1 };
            var mult = new Multinomial<int>(values, probs);
            var hist = Histogram.flattenSample(mult, 1000);

            // The resulting values should now be doubled.
            Assert.AreEqual(0.0, hist.Score(partial<int>.Other), 0.1);
            Assert.AreEqual(0.7, hist.Score(partial<int>.NewTop(10)), 0.1);
            Assert.AreEqual(0.2, hist.Score(partial<int>.NewTop(11)), 0.1);
            Assert.AreEqual(0.1, hist.Score(partial<int>.NewTop(15)), 0.1);
        }
    }
}
