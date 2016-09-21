using Microsoft.Research.Uncertain;
using System;
using System.Linq;

namespace Microsoft.Research.Uncertain.Gps
{
    /// <summary>
    /// Various utility functions for math involving location data
    /// </summary>
    public class GeoMath
    {
        /// <summary>
        /// The mean radius of the Earth in metres
        /// </summary>
        public static double EARTH_RADIUS = 6371009;

        /// <summary>
        /// Calculate the great-circle distance between two uncertain locations.
        /// </summary>
        /// <remarks>The distance is signed to avoid being an overestimate, so the mean of the
        /// resulting distribution may be (slightly) negative due to random error.
        /// </remarks>
        /// <param name="Source">The source location</param>
        /// <param name="Destination">The destination location</param>
        /// <returns>A distribution of the distance from <paramref name="Source"/> to
        /// <paramref name="Destination"/></returns>
        public static Uncertain<double> Distance(Uncertain<Point> Source, Uncertain<Point> Destination)
        {
            return from s in Source
                   from d in Destination
                   let delta = d - s
                   let distance = d.HaversineDistance(s)
                   let direction = d - s
                   let correctedDistance = delta.Dot(direction) > 0 ? distance : -distance
                   select correctedDistance;
        }

        /// <summary>
        /// Calculate the coordinates of a point <paramref name="Distance"/> metres from the point
        /// <paramref name="Centre"/> along a bearing <paramref name="Bearing"/> degrees East of
        /// North.
        /// </summary>
        /// <remarks>
        /// Formula from http://www.movable-type.co.uk/scripts/latlong.html
        /// </remarks>
        /// <param name="Centre">The starting location</param>
        /// <param name="Distance">The distance to travel from <paramref name="Centre"/></param>
        /// <param name="Bearing">The direction to travel in degrees East of North</param>
        /// <returns>The required point</returns>
        public static Point PointFromCentreDistanceBearing(Point Centre, double Distance, double Bearing)
        {
            // Convert to radians
            double LatCentreRads = Centre.Latitude * Math.PI / 180.0;
            double LngCentreRads = Centre.Longitude * Math.PI / 180.0;
            double AngleRads = Bearing * Math.PI / 180.0;

            // Repeated subexpressions
            double R = Distance / EARTH_RADIUS;
            double SinR = Math.Sin(R);
            double CosR = Math.Cos(R);
            double SinLat1 = Math.Sin(LatCentreRads);
            double CosLat1 = Math.Cos(LatCentreRads);

            // New latitude, in radians
            double LatNewRads = Math.Asin(SinLat1 * CosR + CosLat1 * SinR * Math.Cos(AngleRads));
            // New longitude, in radians
            double LngNewRads = LngCentreRads + Math.Atan2(Math.Sin(AngleRads) * SinR * CosLat1, CosR - SinLat1 * Math.Sin(LatNewRads));

            return new Point(LatNewRads * 180.0 / Math.PI, LngNewRads * 180.0 / Math.PI);
        }

        ///// <summary>
        ///// Apply a prior defined by a physics model to GPS evidence.
        ///// </summary>
        ///// <param name="NewLocation">The newly observed GPS evidence</param>
        ///// <param name="SpeedDist">A distribution over possible speeds</param>
        ///// <param name="LastLocation">A hypothesized prior location</param>
        ///// <returns>A new location estimate that combines the GPS evidnece with the physics model prior</returns>
        //public static Uncertain<Point> ApplySpeedPrior(GPSObservation NewLocation, Uncertain<double> SpeedDist, Uncertain<Point> LastLocation)
        //{
        //    // Apply a heuristic to decide whether to ignore the prior
        //    //double dt = Math.Max(0.5, NewLocation.Timestamp.Subtract(LastLocation.Timestamp).TotalSeconds);
        //    //if (dt > 5)
        //    //  return NewLocation;

        //    // Calculate the prior over location by adding the speed to the last location
        //    var R = new RandomMath();
        //    //Point Centre = LastLocation.Centre;
        //    var speedprior = from speed in SpeedDist
        //                     let degrees = R.NextUniform(0, 360)
        //                     let dist = speed / dt
        //                     from center in LastLocation
        //                     select GeoMath.PointFromCentreDistanceBearing(center, dist, degrees) - center;

        //    //select GeoMath.PointFromCentreDistanceBearing(Centre, dist, degrees) - Centre;

        //    // Get the likelihood function from the new GPS observation
        //    Func<Point, double> LikelihoodFn = NewLocation.ToLikelihoodFunction();

        //    Uncertain<Point> Posterior = from l in LastLocation
        //                                 from r in speedprior
        //                                 let loc = l + r
        //                                 let prob = LikelihoodFn(loc)
        //                                 select new Weighted<Point> { Value = loc, Probability = prob };

        //    //var point = Posterior.SampledInference(1000);

        //    return Posterior;//.SampledInference(1000);
        //    //return new GPSObservation(Posterior, NewLocation.Timestamp);

        //    // Apply the Bayes operator to the prior and likelihood function
        //    //Uncertain<Point> Posterior = Bayes.BayesByTable<Point>(LikelihoodFn, Prior);
        //    // Wrap the resulting posterior in the metadata from the new GPS observation
        //    //return new GPSLocation(Posterior, NewLocation.Timestamp);
        //}

        /// <summary>
        /// Calculate the density of the Rayleigh distribution with parameter <paramref name="r"/>
        /// at <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The input value</param>
        /// <param name="r">The distribution parameter</param>
        /// <returns>The density of the distribution</returns>
        public static double RayleighPDF(double x, double r)
        {
            return (x / (r * r)) * Math.Exp(-(x * x) / (2 * r * r));
        }
    }
}
