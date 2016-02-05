using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Histogram;

namespace UncertainTests
{
    [TestClass]
    public class HistogramTests
    {
        [TestMethod]
        public void Flattening()
        {
            var someDist = new Multinomial<int>(1, 2, 2, 3, 3, 3, 4);

            var someHist = Histogram.flatten(someDist);
        }
    }
}
