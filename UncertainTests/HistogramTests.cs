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
            var someDist = new Multinomial<int>(1, 2, 2, 3);
            var someHist = Histogram.flatten(someDist);

            // Check the resulting support.
            Assert.AreEqual(0.0, someHist.Score(partial<int>.Other));
            Assert.AreEqual(0.25, someHist.Score(partial<int>.NewTop(1)));
            Assert.AreEqual(0.5, someHist.Score(partial<int>.NewTop(2)));
            Assert.AreEqual(0.25, someHist.Score(partial<int>.NewTop(3)));
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
            var hist = Histogram.flatten(new Multinomial<double>(2.0, 2.0, 4.0));
            var doubledDist = liftedLambda(hist);
            var doubledHist = Histogram.reflatten(doubledDist);

            // The resulting values should now be doubled.
            Assert.AreEqual(0.0, doubledHist.Score(partial<double>.Other), 0.01);
            Assert.AreEqual(0.6667, doubledHist.Score(partial<double>.NewTop(4.0)), 0.01);
            Assert.AreEqual(0.3333, doubledHist.Score(partial<double>.NewTop(8.0)), 0.01);
        }
    }
}
