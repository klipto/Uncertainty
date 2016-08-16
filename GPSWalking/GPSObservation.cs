using Microsoft.Research.Uncertain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPSWalking
{

    /// <summary>
    /// A GPSObservation is a distribution over possible points on a map that has been sourced
    /// directly from a GPS sensor. This means we know (or at least assume a model for) its
    /// distribution and so can use it as a likelihood function without sampling.
    /// </summary>
    public class GPSObservation : GPSLocation
    {
        /// <summary>
        /// The parameter of the Rayleigh distribution for this observation
        /// </summary>
        public double Epsilon { get; private set; }

        /// <summary>
        /// Create a GPSObservation given data from a GPS sensor (<paramref name="Centre"/> and
        /// <paramref name="HorizontalAccuracy"/>), and a timestamp and position source for the
        /// observation.
        /// </summary>
        /// <param name="Centre">The centre reported by the GPS</param>
        /// <param name="HorizontalAccuracy">the 95% error confidence interval reported by the
        /// GPS</param>
        /// <param name="Timestamp">The time of the observation</param>
        /// <param name="PositionSource">The source of the observation</param>
        public GPSObservation(Point Centre, double HorizontalAccuracy, DateTimeOffset Timestamp, string PositionSource)
        {
            this.Centre_ = Centre;
            this.HorizontalAccuracy_ = HorizontalAccuracy;
            this.Timestamp = Timestamp;
            this.PositionSource = PositionSource;
            this.Epsilon = HorizontalAccuracy / Math.Sqrt(Math.Log(400));
        }

        /// <summary>
        /// Create a GPSObservation given data from a GPS sensor (<paramref name="Centre"/> and
        /// <paramref name="HorizontalAccuracy"/>).
        /// </summary>
        /// <param name="Centre">The centre reported by the GPS</param>
        /// <param name="HorizontalAccuracy">the 95% error confidence interval reported by the
        /// GPS</param>
        public GPSObservation(Point centre, double horizontalAccuracy)
            : this(centre, horizontalAccuracy, DateTimeOffset.MinValue, "")
        { }

        internal GPSObservation(Uncertain<Point> c, DateTimeOffset Timestamp) : base(c, Timestamp) { }

        protected override Point GetSample()
        {
            double Radius = R.NextRayleigh(this.Epsilon);
            double Angle = R.NextUniform(0, 360); // degrees

            return GeoMath.PointFromCentreDistanceBearing(Centre, Radius, Angle);
        }

        /// <summary>
        /// Create a likelihood function from this GPS observation
        /// </summary>
        /// <returns>A likelihood function</returns>
        public Func<Point, double> ToLikelihoodFunction()
        {
            Func<Point, double> Fn = (pt) =>
            {
                double Dist = pt.HaversineDistance(this.Centre);
                return GeoMath.RayleighPDF(Dist, this.Epsilon);
            };

            return Fn;
        }

        /// <summary>
        /// The maximum value of the likelihood function returned by <c>ToLikelihoodFunction</c>
        /// </summary>
        /// <returns>The maximum value</returns>
        public double MaxLikelihood()
        {
            return GeoMath.RayleighPDF(this.Epsilon, this.Epsilon);
        }
        public override double Score(Point t)
        {
            return this.ToLikelihoodFunction()(t);
        }

        protected override IEnumerable<Weighted<Point>> GetSupport()
        {
            throw new Exception("Infinite Support");
        }

        protected override bool StructuralEquals(RandomPrimitive other)
        {
            if ((other is GPSObservation) == false)
                return false;

            if (object.ReferenceEquals(this, other))
                return true;

            var tmp = other as GPSObservation;
            var b = Centre.Equals(tmp.Centre);
            return b;
        }

        protected override int GetStructuralHash()
        {
            return Centre.Longitude.GetHashCode() ^ Centre.Latitude.GetHashCode() ^ Epsilon.GetHashCode();
        }
    }
}

    ///// <summary>
    ///// A GPSObservation is a distribution over possible points on a map that has been sourced
    ///// directly from a GPS sensor. This means we know (or at least assume a model for) its
    ///// distribution and so can use it as a likelihood function without sampling.
    ///// </summary>
    //public class GPSObservation : RandomPrimitive<Point>
    //{
    //    /// <summary>
    //    /// The parameter of the Rayleigh distribution for this observation
    //    /// </summary>
    //    public double Epsilon { get; private set; }
    //    /// <summary>
    //    /// The center of the GPS estimate
    //    /// </summary>
    //    public Point Center { get; private set; }

    //    private RandomMath random;

    //    /// <summary>
    //    /// Create a GPSObservation given data from a GPS sensor (<paramref name="Centre"/> and
    //    /// <paramref name="HorizontalAccuracy"/>), and a timestamp and position source for the
    //    /// observation.
    //    /// </summary>
    //    /// <param name="Centre">The centre reported by the GPS</param>
    //    /// <param name="HorizontalAccuracy">the 95% error confidence interval reported by the
    //    /// GPS</param>
    //    public GPSObservation(Point center, double horizontalAccuracy)
    //    {
    //        this.Center = center;
    //        this.Epsilon = horizontalAccuracy / Math.Sqrt(Math.Log(400));
    //        this.random = new RandomMath();
    //    }

    //    public double Likelihood(Point pt)
    //    {
    //        double Dist = pt.HaversineDistance(this.Center);
    //        return GeoMath.RayleighPDF(Dist, this.Epsilon);
    //    }

    //    protected override Point GetSample()
    //    {
    //        double Radius = this.random.NextRayleigh(this.Epsilon);
    //        double Angle = this.random.NextUniform(0, 360); // degrees

    //        return GeoMath.PointFromCentreDistanceBearing(this.Center, Radius, Angle);
    //    }

    //    protected override bool StructuralEquals(RandomPrimitive other)
    //    {
    //        if ((other is GPSObservation) == false)
    //            return false;

    //        if (object.ReferenceEquals(this, other))
    //            return true;

    //        var tmp = other as GPSObservation;

    //        return this.Center.Equals(tmp.Center);
    //    }

    //    protected override int GetStructuralHash()
    //    {
    //        return this.Center.Longitude.GetHashCode() ^ this.Center.Latitude.GetHashCode() ^ this.Epsilon.GetHashCode();
    //    }

    //    public override double Score(Point t)
    //    {
    //        return this.Likelihood(t);
    //    }

    //    protected override IEnumerable<Weighted<Point>> GetSupport()
    //    {
    //        throw new Exception("Infinite Support");
    //    }
    //}
