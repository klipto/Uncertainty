using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.Uncertain.Gps
{
    public class GPSLocation : RandomPrimitive<Point>
    {
        /// <summary>
        /// The 95% confidence interval, in metres, of the GPS error
        /// </summary>
        public double HorizontalAccuracy
        {
            get
            {
                if (!horizontalAccuracy.HasValue)
                    ComputeCentreAndAccuracy();
                return horizontalAccuracy.Value;
            }
        }

        /// <summary>
        /// The centre of the GPS estimate
        /// </summary>
        public Point Center
        {
            get
            {
                if (center == null)
                    ComputeCentreAndAccuracy();
                return center;
            }
        }

        /// <summary>
        /// The time the location was recorded
        /// </summary>
        public DateTimeOffset Timestamp { get; protected set; }


        protected RandomMath randomMath = new RandomMath();
        protected double? horizontalAccuracy;
        protected Point center;

        /// <summary>
        /// The underlying distribution
        /// </summary>
        protected Uncertain<Point> Distribution;
        protected IEnumerator<Weighted<Point>> sampler;

        protected GPSLocation() { }

        public GPSLocation(Uncertain<Point> Dist, DateTimeOffset Timestamp)
        {
            this.Distribution = Dist;
            this.Timestamp = Timestamp;
            // really don't like this: forward sampler should not be exposed to clients
            // but I am having trouble getting this all to work with James' design without
            // allowing this class to sample from a sub-distribution.
            this.sampler = new ForwardSampler<Point>(this.Distribution).GetEnumerator();
        }


        public override double Score(Point t)
        {
            return 1.0;
        }

        protected override Point GetSample()
        {
            if (sampler.MoveNext() == false)
                throw new Exception("Should not happen");
            return sampler.Current.Value;
        }

        protected override int GetStructuralHash()
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<Weighted<Point>> GetSupport()
        {
            throw new NotImplementedException();
        }

        protected override bool StructuralEquals(RandomPrimitive other)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Compute the centre and horizontal accuracy for this distribution.
        /// </summary>
        private void ComputeCentreAndAccuracy(int SampleSize = 5000)
        {
            // Calculate the mean, and record each sample to be used to calculate horizontal accuracy
            Point[] Points = new Point[SampleSize];
            for (int i = 0; i < SampleSize; i++)
            {
                Points[i] = this.GetSample();
            }

            var Mean = this.ExpectedValue();
            this.center = Mean;

            // Calculate horizontal accuracy by finding the 95th percentile of distances from the
            // mean. An O(n) selection algorithm may be an improvement here for larger n.
            List<double> Distances = new List<double>();
            for (int i = 0; i < SampleSize; i++)
            {
                Distances.Add(Mean.HaversineDistance(Points[i]));
            }
            Distances.Sort();
            this.horizontalAccuracy = Distances[(int)(SampleSize * 0.95)];
        }

    }
}
