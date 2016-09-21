using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPSWalking
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

    public class Program
    {
        private static Tuple<Point, double> WeightedMeanAndAccuracy(IList<Weighted<Point>> data)
        {
            var N = (double)data.Count();
            var WeightSum = (from k in data select k.Probability).Sum();

            // weighted mean
            //var Xbar = data.Select(x => x.Value * x.Probability).Aggregate((a, b) => a + b) / WeightSum;
            var Xbar = data.Select(x => x.Value).Aggregate((a, b) => a + b) / N;

            // Calculate horizontal accuracy by finding the 95th percentile of distances from the
            // mean. An O(n) selection algorithm may be an improvement here for larger n.
            var distances = data.Select(x => Xbar.HaversineDistance(x.Value)).ToList();
            distances.Sort();
            var accuracy = distances.ElementAt((int)(distances.Count * 0.95));
            Console.WriteLine(String.Format("{0} {1}", Xbar, accuracy));
            return Tuple.Create(Xbar, accuracy);
        }

        public static GPSLocation ApplySpeedPrior(GPSObservation newlocation, GPSLocation lastlocation)
        {
            // The speed prior; units are metres per second
            // The model is that human speeds are normal about 0, with ~80% < 3 m/s (= 6.7 mph)
            // So 1.3*stdev = 3
            double StDev = 3 / 1.3;
            Uncertain<double> speedprior = new Gaussian(0, StDev);

            // Apply a heuristic to decide whether to ignore the prior
            double dt = Math.Max(0.5, newlocation.Timestamp.Subtract(lastlocation.Timestamp).TotalSeconds);
            if (dt > 5)
                return newlocation;
            
            Point center = lastlocation.Center;
            var traveled = from speed in speedprior
                           from degrees in new Uniform<double>(0, 360)
                           let dist = speed / dt
                           select GeoMath.PointFromCentreDistanceBearing(center, dist, degrees) - center;

            var prior = from l in lastlocation
                        from t in traveled
                        select t + l;

            // Get the likelihood function from the new GPS observation
            Func <Point, double> LikelihoodFn = newlocation.ToLikelihoodFunction();

            Uncertain<Point> reweighted = from p in prior
                                         let prob = LikelihoodFn(p)
                                         select new Weighted<Point> { Value = p, Probability = prob };

            // short circuit and call inference or else perf suffers.
            var posterior = reweighted.SampledInference(1000);

            return new GPSLocation(posterior, newlocation.Timestamp);
        }

        /// <summary>
        /// Transform a list of GPS observations by applying a speed prior to each location
        /// </summary>
        /// <param name="Locations">The input locations</param>
        /// <returns>A list of pairs of locations (OldLocation, TransformedLocation)</returns>
        static List<Tuple<GPSLocation, GPSLocation>> TransformGPSData(List<GPSObservation> Locations)
        {
            List<Tuple<GPSLocation, GPSLocation>> Output = new List<Tuple<GPSLocation, GPSLocation>>();
            var r = new RandomMath();


            Console.WriteLine();
            Console.WriteLine("Transforming GPS data...");
            GPSLocation LastLocation = null;
            int n = 0;
            foreach (GPSObservation Location in Locations)
            {
                n += 1;
                if (n % 50 == 0)
                    Console.WriteLine("\t" + n + "/" + Locations.Count);

                // Skip the first one but store it as the previous location
                if (n == 1)
                {
                    LastLocation = Location;
                    continue;
                }

                // Apply the speed prior to the new location
                GPSLocation NewLocation = ApplySpeedPrior(Location, LastLocation);
                Output.Add(Tuple.Create<GPSLocation, GPSLocation>(Location, NewLocation));
                Console.WriteLine(String.Format("{0} {1}", NewLocation.Center, NewLocation.HorizontalAccuracy));
                // Reuse the new location as the last location in the next iteration
                LastLocation = NewLocation;
            }

            return Output;
        }

        static void Main(string[] args)
        {
            // Input file
            string InputPath = "..\\..\\Data\\Campus.csv";
            if (args.Length > 0)
                InputPath = args[0];

            // Read the location trace from the input file
            Console.WriteLine("Reading input trace...");
            var Reader = new StreamReader(InputPath);
            List<GPSObservation> Locations = new List<GPSObservation>();
            string Line;
            while ((Line = Reader.ReadLine()) != null)
            {
                string[] Columns = Line.Split(',');
                if (Columns.Length != 5) continue;

                // Ignore marks
                if (Columns[1] == "Mark") continue;

                double Latitude = Double.Parse(Columns[1]);
                double Longitude = Double.Parse(Columns[2]);
                double Accuracy = Double.Parse(Columns[3]);
                DateTimeOffset Timestamp = DateTimeOffset.Parse(Columns[4]);

                if (Double.IsNaN(Latitude) || Double.IsNaN(Longitude) || Double.IsNaN(Accuracy)) continue;

                Locations.Add(new GPSObservation(Latitude, Longitude, Accuracy, Timestamp));
            }
            Reader.Close();

            //// Apply the transformation to each point, creating a list of (OldLoc, NewLoc) tuples
            List<Tuple<GPSLocation, GPSLocation>> ImprovedLocations = TransformGPSData(Locations);

            // Write the output to Output.csv in the same folder as the input file
            Console.WriteLine();
            Console.WriteLine("Computing and writing output trace...");
            string OutputPath = Path.GetDirectoryName(InputPath) + "\\Output.csv";
            var Writer = new StreamWriter(OutputPath);
            Writer.WriteLine("Time,Seconds,NaiveLat,NaiveLng,NaiveAcc,NaiveSpeed,NewLat,NewLng,NewAcc,NewSpeed");

            GPSLocation LastLocationNaive = Locations[0];
            GPSLocation LastLocationTrans = Locations[0];
            int n = 0;
            foreach (var Pair in ImprovedLocations)
            {
                n += 1;
                if (n % 50 == 0)
                    Console.WriteLine("\t" + n + "/" + ImprovedLocations.Count);

                double dt = Pair.Item1.Timestamp.Subtract(LastLocationNaive.Timestamp).TotalSeconds;
                double secs = Pair.Item1.Timestamp.Subtract(Locations[0].Timestamp).TotalSeconds;

                Uncertain<double> SpeedNaive = from l in GeoMath.Distance(LastLocationNaive, Pair.Item1)
                                               select l / dt;

                double SpeedNaiveMean = Math.Max(SpeedNaive.ExpectedValueWithConfidence().Mean, 0);

                Uncertain<double> SpeedTrans = from l in GeoMath.Distance(LastLocationTrans, Pair.Item2)
                                               select l / dt;

                double SpeedTransMean = Math.Max(SpeedTrans.ExpectedValueWithConfidence().Mean, 0);

                Writer.WriteLine(Pair.Item1.Timestamp + "," + secs + "," +
                    Pair.Item1.Center.Latitude + "," + Pair.Item1.Center.Longitude + "," +
                    Pair.Item1.HorizontalAccuracy + "," + SpeedNaiveMean + "," +
                    Pair.Item2.Center.Latitude + "," + Pair.Item2.Center.Longitude + "," +
                    Pair.Item2.HorizontalAccuracy + "," + SpeedTransMean);

                LastLocationNaive = Pair.Item1;
                LastLocationTrans = Pair.Item2;
            }
            Writer.Close();

            Console.WriteLine();
            Console.WriteLine("Done! Press any key to exit...");
            Console.ReadKey();
        }
    }
}