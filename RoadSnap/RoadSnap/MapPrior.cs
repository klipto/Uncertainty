using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Gps;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoadSnap
{
    /// <summary>
    /// A prior distribution reflecting road network data
    /// </summary>
    class MapPrior : RandomPrimitive<Point>
    {
        /// <summary>
        /// The sampler that draws a segment of road
        /// </summary>
        private DiscreteSampler<Tuple<Point, Point>> SegmentSampler;

        /// <summary>
        /// The centre of the area that can be sampled for non-road samples
        /// </summary>
        private Point Centre;
        /// <summary>
        /// The radius of the area that can be sampled for non-road samples
        /// </summary>
        private double Radius;
        /// <summary>
        /// The desired probability of a sample being a road samples (versus a non-road sample)
        /// </summary>
        private double RoadProbability;

        private RandomMath R = new RandomMath();

        /// <summary>
        /// Create a map-based prior from a given list of road segments. The on-road points will be
        /// sampled <paramref name="Weight"/> times more often than other points in the circle
        /// centred at <paramref name="Centre"/> with radius <paramref name="Radius"/>.
        /// </summary>
        /// <param name="Segments">The list of road segments in the map</param>
        /// <param name="Centre">The centre of the non-road sample space</param>
        /// <param name="Radius">The radius of the non-road sample space</param>
        /// <param name="Weight">How strongly to prefer roads over non-roads</param>
        public MapPrior(List<Tuple<Point, Point>> Segments, Point Centre, double Radius, double Weight = 5.0)
        {
            this.Centre = Centre;
            this.Radius = Radius;
            this.RoadProbability = 1 - 1 / (1 + Weight);

            if (Segments.Count > 0 && Weight > 0)
                this.InitializeSegmentSampler(Segments);
        }

        /// <summary>
        /// Initialize the segment sampler, which returns a segment of road to be sampled from
        /// uniformly. Each segment is weighted by its length, so a sample from the resulting
        /// sampler is uniformly drawn from all on-road points in the given map.
        /// </summary>
        /// <param name="Segments">The list of road segments in the map</param>
        private void InitializeSegmentSampler(List<Tuple<Point, Point>> Segments)
        {
            List<double> Lengths = new List<double>();

            foreach (Tuple<Point, Point> Segment in Segments)
            {
                double Length = Segment.Item2.HaversineDistance(Segment.Item1);
                Lengths.Add(Length);
            }

            this.SegmentSampler = new DiscreteSampler<Tuple<Point, Point>>(Segments.ToArray(), Lengths.ToArray());
        }

        /// <summary>
        /// Draw a new sample from the map prior. The sample is on-road with probability
        /// <c>RoadProbability</c>.
        /// </summary>
        /// <returns>A new sample from the distribution</returns>
        protected override Point GetSample()
        {
            if (SegmentSampler == null)
                return SampleNonRoad();
            double U = R.NextDouble();
            if (U < RoadProbability)
                return SampleRoad();
            else
                return SampleNonRoad();
        }

        protected override bool StructuralEquals(RandomPrimitive other)
        {
            throw new NotImplementedException();
        }

        protected override int GetStructuralHash()
        {
            throw new NotImplementedException();
        }

        public override double Score(Point t)
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<Weighted<Point>> GetSupport()
        {
            throw new Exception("Infinite Support");
        }

        /// <summary>
        /// Draw a on-road sample from the map. The sample is uniformly drawn from all on-road
        /// points in the map.
        /// </summary>
        /// <returns>A new on-road sample from the distribution</returns>
        private Point SampleRoad()
        {
            // First, select a road segment
            Tuple<Point, Point> Segment = SegmentSampler.Sample();
            double Length = Segment.Item2.HaversineDistance(Segment.Item1);
            // Now select a distance along it
            double Dist = R.NextUniform(0, Length);
            // Interpolate along the road to find the sample point
            double pct = Dist / Length;
            double Lat = pct * (Segment.Item2.Latitude - Segment.Item1.Latitude) + Segment.Item1.Latitude;
            double Lng = pct * (Segment.Item2.Longitude - Segment.Item1.Longitude) + Segment.Item1.Longitude;
            return new Point(Lat, Lng);
        }

        /// <summary>
        /// Draw an off-road sample from the map uniformly.
        /// </summary>
        /// <returns>A new off-road sample from the distribution</returns>
        private Point SampleNonRoad()
        {
            // Select a distance and angle uniformly
            double Dist = R.NextUniform(0, Radius);
            double Angle = R.NextUniform(0, 360); // degrees
            return GeoMath.PointFromCentreDistanceBearing(Centre, Dist, Angle);
        }
    }
}
