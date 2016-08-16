using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPSWalking
{
    /// <summary>
    /// A GPS location is a distribution over possible points on a map, together with some metadata
    /// (such as timestamp and data source) and methods to compute the centre and horizontal
    /// accuracy of the distribution (for compatibility with existing GPS APIs).
    /// </summary>
    public abstract class GPSLocation : RandomPrimitive<Point>
    {
        /// <summary>
        /// The 95% confidence interval, in metres, of the GPS error
        /// </summary>
        public double HorizontalAccuracy
        {
            get
            {
                if (!HorizontalAccuracy_.HasValue) ComputeCentreAndAccuracy();
                return HorizontalAccuracy_.Value;
            }
        }

        /// <summary>
        /// The centre of the GPS estimate
        /// </summary>
        public Point Centre
        {
            get
            {
                if (Centre_ == null)
                    ComputeCentreAndAccuracy();
                return Centre_;
            }
        }

        /// <summary>
        /// The time the location was recorded
        /// </summary>
        public DateTimeOffset Timestamp { get; protected set; }

        /// <summary>
        /// The source of the data for this location
        /// </summary>
        public string PositionSource { get; protected set; }

        protected RandomMath R = new RandomMath();
        protected double? HorizontalAccuracy_;
        protected Point Centre_;

        /// <summary>
        /// The underlying distribution
        /// </summary>
        protected Uncertain<Point> Distribution;

        /// <summary>
        /// The GPSObservation constructor does not need to call the base constructors
        /// </summary>
        protected GPSLocation() { }

        /// <summary>
        /// Create a GPSLocation given a distribution over locations and a timestamp
        /// </summary>
        /// <param name="Dist">The distribution over locations</param>
        /// <param name="Timestamp">The time the location was recorded</param>
        public GPSLocation(Uncertain<Point> Dist, DateTimeOffset Timestamp)
            : this(Dist, Timestamp, "Unknown")
        { }

        /// <summary>
        /// Create a GPSLocation given a distribution over locations, a timestamp, and a source of
        /// the location data
        /// </summary>
        /// <param name="Dist">The distribution over locations</param>
        /// <param name="Timestamp">The time the location was recorded</param>
        /// <param name="PositionSource">The source of the location data</param>
        protected GPSLocation(Uncertain<Point> Dist, DateTimeOffset Timestamp, string PositionSource)
        {
            this.Distribution = Dist;
            this.Timestamp = Timestamp;
            this.PositionSource = PositionSource;
        }

        //protected override bool StructuralEquals(RandomPrimitive other)
        //{
        //    if ((other is GPSLocation) == false)
        //        return false;

        //    if (object.ReferenceEquals(this, other))
        //        return true;

        //    var tmp = other as GPSLocation;
        //    var b = Centre.Equals(tmp.Centre);
        //    return b;
        //}

        //protected override int GetStructuralHash()
        //{
        //    return Centre.Longitude.GetHashCode() ^ Centre.Latitude.GetHashCode() ^ Epsilon.GetHashCode();
        //}

        //public override void Walk(Visitor V)
        //{
        //    V.Walk(Distribution);
        //    V.Visit(this);
        //}

        /// <summary>
        /// GPSLocation is just a proxy for <c>Distribution</c>, and so can call its
        /// <c>NewSample()</c> directly.
        /// </summary>
        /// <returns>A sample from the distribution</returns>
        //protected override Point NewSampleWithContext(SampleContext Samples, out double Weight)
        //{
        //    return Distribution.SampleWithContext(Samples, out Weight);
        //}

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
            this.Centre_ = Mean;

            // Calculate horizontal accuracy by finding the 95th percentile of distances from the
            // mean. An O(n) selection algorithm may be an improvement here for larger n.
            List<double> Distances = new List<double>();
            for (int i = 0; i < SampleSize; i++)
            {
                Distances.Add(Mean.HaversineDistance(Points[i]));
            }
            Distances.Sort();
            this.HorizontalAccuracy_ = Distances[(int)(SampleSize * 0.95)];
        }


        internal Point ExpectedValue(int SampleSize = 1000)
        {
            var data = Enumerable.Range(0, SampleSize).Select(_ => this.GetSample()).ToList();

            var N = (double)data.Count();            

            var Xbar = (data.Aggregate((a,b) => a + b)) / N;

            return Xbar;            
        }
    }
}
