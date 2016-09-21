using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.Uncertain.Gps
{
    /// <summary>
    /// A point on the Earth's surface (latitude and longitude)
    /// </summary>
    public class Point : IEquatable<Point>
    {
        /// <summary>
        /// Latitude in degrees
        /// </summary>
        public double Latitude;
        /// <summary>
        /// Longitude in degrees
        /// </summary>
        public double Longitude;

        public Point(double latitude, double longitude)
        {
            this.Latitude = latitude;
            this.Longitude = longitude;
        }
        public Point(Point other)
        {
            this.Latitude = other.Latitude;
            this.Longitude = other.Longitude;
        }

        public static Point operator +(Point lhs, Point rhs)
        {
            return new Point(lhs.Latitude + rhs.Latitude, lhs.Longitude + rhs.Longitude);
        }
        public static Point operator -(Point lhs, Point rhs)
        {
            return new Point(lhs.Latitude - rhs.Latitude, lhs.Longitude - rhs.Longitude);
        }
        public static Point operator *(double c, Point vec)
        {
            return new Point(c * vec.Latitude, c * vec.Longitude);
        }
        public static Point operator *(Point vec, double c)
        {
            return new Point(c * vec.Latitude, c * vec.Longitude);
        }
        public static Point operator /(Point num, double denom)
        {
            return new Point(num.Latitude / denom, num.Longitude / denom);
        }

        public override string ToString()
        {
            return String.Format("({0},{1})", this.Latitude, this.Longitude);
        }

        /// <summary>
        /// Compute the dot product of this point and another point
        /// </summary>
        /// <param name="other">The other point</param>
        /// <returns>The dot product</returns>
        public double Dot(Point other)
        {
            return this.Latitude * other.Latitude + this.Longitude * other.Longitude;
        }

        /// <summary>
        /// The great circle distance between this point and another point (i.e., shortest distance
        /// along the Earth's surface)
        /// </summary>
        /// <param name="other">The other point</param>
        /// <returns>The distance in metres</returns>
        public double HaversineDistance(Point other)
        {
            double Lat1 = this.Latitude * Math.PI / 180.0;
            double Lon1 = this.Longitude * Math.PI / 180.0;
            double Lat2 = other.Latitude * Math.PI / 180.0;
            double Lon2 = other.Longitude * Math.PI / 180.0;

            double dLat = Lat2 - Lat1;
            double dLon = Lon2 - Lon1;

            double SinDLatDiv2 = Math.Sin(dLat / 2);
            double SinDLonDiv2 = Math.Sin(dLon / 2);

            double a = SinDLatDiv2 * SinDLatDiv2 + SinDLonDiv2 * SinDLonDiv2 * Math.Cos(Lat1) * Math.Cos(Lat2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return c * GeoMath.EARTH_RADIUS;
        }

        public bool Equals(Point other)
        {
            return this.Latitude == other.Latitude && this.Longitude == other.Longitude;
        }
    }
}
