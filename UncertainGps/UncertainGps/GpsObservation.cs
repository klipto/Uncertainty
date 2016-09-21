using System;
using System.Collections.Generic;

namespace Microsoft.Research.Uncertain.Gps
{
    public class GPSObservation : GPSLocation
    {
        public double Epsilon { get; private set; }

        public GPSObservation(double lat, double lon, double horizontalAccuracy, DateTimeOffset offset)
        {
            this.center = new Point(lat, lon);
            this.horizontalAccuracy = horizontalAccuracy;
            this.Epsilon = horizontalAccuracy / Math.Sqrt(Math.Log(400));
            this.Timestamp = offset;
        }

        /// <summary>
        /// Create a likelihood function from this GPS observation
        /// </summary>
        /// <returns>A likelihood function</returns>
        public Func<Point, double> ToLikelihoodFunction()
        {
            Func<Point, double> Fn = (pt) =>
            {
                double dist = pt.HaversineDistance(this.Center);
                return GeoMath.RayleighPDF(dist, this.Epsilon);
            };

            return Fn;
        }

        protected override Point GetSample()
        {
            double radius = randomMath.NextRayleigh(this.Epsilon);
            double angle = randomMath.NextUniform(0, 360); // degrees

            return GeoMath.PointFromCentreDistanceBearing(this.Center, radius, angle);
        }

        protected override bool StructuralEquals(RandomPrimitive other)
        {
            if ((other is GPSObservation) == false)
                return false;

            if (object.ReferenceEquals(this, other))
                return true;

            var tmp = other as GPSObservation;
            var b = Center.Equals(tmp.Center);
            return b;
        }

        protected override int GetStructuralHash()
        {
            return Center.Longitude.GetHashCode() ^ Center.Latitude.GetHashCode() ^ Epsilon.GetHashCode();
        }

        public override double Score(Point t)
        {
            return this.ToLikelihoodFunction()(t);
        }

        protected override IEnumerable<Weighted<Point>> GetSupport()
        {
            throw new Exception("Infinite Support");
        }
    }
}
