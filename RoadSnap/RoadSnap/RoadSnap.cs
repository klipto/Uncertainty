using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using Microsoft.Research.Uncertain.Gps;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.Uncertain.Inference
{
    public static class MyExtensions
    {
        public static Uncertain<T> RejectionInference<T>(this Uncertain<T> source, double maxlikelihood, int maxsamples, IEqualityComparer<T> comparer = null)
        {
            if (comparer == null) comparer = EqualityComparer<T>.Default;

            var r = new RandomMath();
            var sampler = new ForwardSampler<T>(source);
            var data = new List<Weighted<T>>();

            foreach(var sample in sampler.Take(maxsamples))
            {
                if (r.NextDouble() < sample.Probability / maxlikelihood)
                {
                    data.Add(new Weighted<T> { Value = sample.Value, Probability = 1.0 } );
                }
            }

            if (data.Count == 0)
                throw new TimeoutException("Rejection sampling exceeded maximum iterations.");

            return Extensions.RunInference(data, comparer);
        }

        public static Uncertain<T> LikelihoodInference<T>(this Uncertain<T> source, int maxsamples, IEqualityComparer<T> comparer = null)
        {
            var sampler = new ForwardSampler<T>(source);
            return Extensions.RunInference(sampler.Take(maxsamples).ToList(), comparer);
        }

        public static Uncertain<T> MCMCInference<T>(this Uncertain<T> source, int maxsamples, IEqualityComparer<T> comparer = null)
        {
            var sampler = new MarkovChainMonteCarloSampler<T>(source);
            return Extensions.RunInference(sampler.Take(maxsamples).ToList(), comparer);
        }
    }
}


namespace RoadSnap
{
    public class Aggregate
    {
        private double Mu = 0.0;
        private double M2 = 0.0;
        private int N = 0;

        public Aggregate() { }

        public void Add(double X)
        {
            N += 1;
            var Delta = X - Mu;
            Mu += Delta / N;
            M2 += Delta * (X - Mu);
        }

        public double Mean()
        {
            return Mu;
        }
        public double Variance()
        {
            if (N < 2) throw new ArgumentException();
            return M2 / (N - 1);
        }
        public string Str()
        {
            return String.Format("{0:F6},{1:F6}", Mean(), Variance());
        }
    }

    class RoadSnap
    {

        static Tuple<Aggregate, Aggregate, Aggregate> RunExperiment(
            double RoadWeight,
            int N,
            double SearchRadius,
            IEnumerable<GPSObservation> Locations,
            RoadDB DB,
            ISet<int> Blacklist,
            Func<GPSObservation, Point, Point, int, double, double, int> NewResultCallback,
            Func<int, Point[], int> NewRoadCallback)
        {

            int n = 0;
            Point GroundTruth = new Point(0, 0);
            HashSet<int> Idxs = new HashSet<int>();
            var ErrorNaive = new Aggregate();
            var ErrorPrior = new Aggregate();
            var SampleTime = new Aggregate();

            foreach (GPSObservation Loc in Locations)
            {
                if (n % 50 == 0) Console.WriteLine("\t" + n + "/" + Locations.Count());
                n++;

                // Query the roads database to create a prior over nearby roads
                Uncertain<Point> Prior = DB.MapQuery(Loc.Center, Loc.HorizontalAccuracy * SearchRadius, RoadWeight);


                // Apply the prior to the newly observed location
                //Uncertain<Point> Posterior = Bayes.BayesByLHRW(Prior, Loc.Center, (r, t) => GeoMath.RayleighPDF(t.HaversineDistance(r), Loc.Epsilon));
                Uncertain<Point> tmp = from location in Prior
                                             let reweighted = GeoMath.RayleighPDF(Loc.Center.HaversineDistance(location), Loc.Epsilon)
                                             select new Weighted<Point>
                                             {
                                                 Value = location,
                                                 Probability = reweighted
                                             };
                Uncertain<Point> Posterior = tmp.LikelihoodInference(10000);
                // Calculate the mean of the posterior and time it
                DateTime T1 = DateTime.Now;
                Point PostLoc = Posterior.ExpectedValue(N);
                SampleTime.Add((DateTime.Now - T1).TotalSeconds);

                // Find the ground truth for this sample by finding the closest point that is on a
                // road which is not on the blacklist
                var Closest = DB.ClosestRoadToPoint(Loc.Center, Loc.HorizontalAccuracy, Blacklist);
                Point MinPoint = Closest.Item1;
                int MinIdx = Closest.Item2;
                if (MinIdx != -1)
                {
                    if (!Idxs.Contains(MinIdx))
                    {
                        NewRoadCallback(MinIdx, DB.GetRoadByIdx(MinIdx));
                        Idxs.Add(MinIdx);
                    }
                    GroundTruth = MinPoint;
                }

                var OrigDist = GroundTruth.HaversineDistance(Loc.Center);
                ErrorNaive.Add(OrigDist);
                var NewDist = GroundTruth.HaversineDistance(PostLoc);
                ErrorPrior.Add(NewDist);

                NewResultCallback(Loc, PostLoc, GroundTruth, MinIdx, OrigDist, NewDist);
            }
            return Tuple.Create(ErrorNaive, ErrorPrior, SampleTime);
        }

        static Tuple<Aggregate, Aggregate, Aggregate, Aggregate, Aggregate, Aggregate> RunExperimentQuery(
            double RoadWeight,
            double SearchRadius,
            IEnumerable<GPSObservation> Locations,
            RoadDB DB,
            ISet<int> Blacklist)
        {

            int n = 0;
            Point GroundTruth = new Point(0, 0);
            HashSet<int> Idxs = new HashSet<int>();
            var SamplesLHRW = new Aggregate();
            var SamplesRej = new Aggregate();
            var DecisionsLHRW = new Aggregate();
            var DecisionsRej = new Aggregate();
            var TimeLHRW = new Aggregate();
            var TimeRej = new Aggregate();

            var LastLocation = Locations.First();

            foreach (GPSObservation Loc in Locations.Skip(1))
            {
                if (n % 50 == 0) Console.WriteLine("\t" + n + "/" + Locations.Count());
                n++;

                //var NumSamplesLHRW = UncertainBase.NumOuterSamples;
                var NumSamplesLHRW = 0;
                // Query the roads database to create a prior over nearby roads
                Uncertain<Point> Prior = DB.MapQuery(Loc.Center, Loc.HorizontalAccuracy * SearchRadius, RoadWeight);
                // Apply the prior to the newly observed location
                //Uncertain<Point> Posterior = Bayes.BayesByLHRW(Prior, Loc.Center, (r, t) => GeoMath.RayleighPDF(t.HaversineDistance(r), Loc.Epsilon));
                Uncertain<Point> tmp = from location in Prior
                                             let reweighted = GeoMath.RayleighPDF(Loc.Center.HaversineDistance(location), Loc.Epsilon)
                                             select new Weighted<Point>
                                             {
                                                 Value = location,
                                                 Probability = reweighted
                                             };
                Uncertain<Point> Posterior = tmp.LikelihoodInference(10000);
                // Do the query
                var Query = GeoMath.Distance(LastLocation, Posterior) > 1;
                DateTime T1 = DateTime.Now;
                double QueryResultLHRW = Query.Pr(out NumSamplesLHRW, 0.5, MaxSampleSize: 10000) ? 1 : 0;
                TimeLHRW.Add((DateTime.Now - T1).TotalMilliseconds);

                SamplesLHRW.Add(NumSamplesLHRW);
                DecisionsLHRW.Add(QueryResultLHRW);


                var NumSamplesRej = 0;
                // Query the roads database to create a prior over nearby roads
                Prior = DB.MapQuery(Loc.Center, Loc.HorizontalAccuracy * SearchRadius, RoadWeight);
                // Apply the prior to the newly observed location
                //Posterior = Bayes.BayesByRejection((r) => GeoMath.RayleighPDF(Loc.Centre.HaversineDistance(r), Loc.Epsilon), 1.0, Prior, 10000);
                tmp = from location in Prior
                      let reweighted = GeoMath.RayleighPDF(Loc.Center.HaversineDistance(location), Loc.Epsilon)
                      select new Weighted<Point>
                      {
                        Value = location,
                        Probability = reweighted
                      };

                Posterior = tmp.RejectionInference(1.0, 10000);
                // Do the query
                Query = GeoMath.Distance(LastLocation, Posterior) > 1;
                DateTime T2 = DateTime.Now;
                double QueryResultRej = Query.Pr(out NumSamplesRej, 0.5, MaxSampleSize: 10000) ? 1 : 0;
                TimeRej.Add((DateTime.Now - T2).TotalMilliseconds);

                SamplesRej.Add(NumSamplesRej);
                DecisionsRej.Add(QueryResultRej);

                LastLocation = Loc;

                //// Find the ground truth for this sample by finding the closest point that is on a
                //// road which is not on the blacklist
                //var Closest = DB.ClosestRoadToPoint(Loc.Centre, Loc.HorizontalAccuracy, Blacklist);
                //Point MinPoint = Closest.Item1;
                //int MinIdx = Closest.Item2;
                //if (MinIdx != -1) {
                //    if (!Idxs.Contains(MinIdx)) {
                //        Idxs.Add(MinIdx);
                //    }
                //    GroundTruth = MinPoint;
                //}

                //var OrigDist = GroundTruth.HaversineDistance(Loc.Centre);
                //ErrorNaive.Add(OrigDist);
                //var NewDist = GroundTruth.HaversineDistance(PostLoc);
                //ErrorPrior.Add(NewDist);
            }
            return Tuple.Create(SamplesLHRW, SamplesRej, DecisionsLHRW, DecisionsRej, TimeLHRW, TimeRej);
        }

        static Tuple<Aggregate, Aggregate, Aggregate> RunExperimentAccuracy(
            double RoadWeight,
            double SearchRadius,
            double Alpha,
            IEnumerable<GPSObservation> Locations,
            RoadDB DB,
            ISet<int> Blacklist)
        {

            int n = 0;
            Point GroundTruth = new Point(0, 0);
            HashSet<int> Idxs = new HashSet<int>();
            var Accuracy = new Aggregate();
            var Samples = new Aggregate();
            var Time = new Aggregate();

            var LastLocation = Locations.First();

            foreach (GPSObservation Loc in Locations.Skip(1))
            {
                if (n % 50 == 0) Console.WriteLine("\t" + n + "/" + Locations.Count());
                n++;

                var NumSamples = 0;
                // Query the roads database to create a prior over nearby roads
                Uncertain<Point> Prior = DB.MapQuery(Loc.Center, Loc.HorizontalAccuracy * SearchRadius, RoadWeight);
                // Apply the prior to the newly observed location
                //Uncertain<Point> Posterior = Bayes.BayesByLHRW(Prior, Loc.Center, (r, t) => GeoMath.RayleighPDF(t.HaversineDistance(r), Loc.Epsilon));
                Uncertain<Point> tmp = from location in Prior
                                       let reweighted = GeoMath.RayleighPDF(Loc.Center.HaversineDistance(location), Loc.Epsilon)
                                       select new Weighted<Point>
                                       {
                                           Value = location,
                                           Probability = reweighted
                                       };
                Uncertain<Point> Posterior = tmp.LikelihoodInference(10000);

                // Do the query
                var Query = GeoMath.Distance(LastLocation, Posterior) > 4;
                DateTime T1 = DateTime.Now;
                var QueryResult = Query.Pr(out NumSamples, Alpha, MaxSampleSize: 100000); //, Alpha: Alpha);
                Time.Add((DateTime.Now - T1).TotalMilliseconds);
                Samples.Add(NumSamples);

                var Closest = DB.ClosestRoadToPoint(Loc.Center, Loc.HorizontalAccuracy, Blacklist);
                Point MinPoint = Closest.Item1;
                int MinIdx = Closest.Item2;
                if (MinIdx != -1)
                {
                    if (!Idxs.Contains(MinIdx))
                    {
                        Idxs.Add(MinIdx);
                    }
                    GroundTruth = MinPoint;
                }

                var QueryTruth = GroundTruth.HaversineDistance(LastLocation.Center) > 4;
                Accuracy.Add(QueryTruth == QueryResult ? 1.0 : 0.0);

                //var OrigDist = GroundTruth.HaversineDistance(Loc.Centre);
                //ErrorNaive.Add(OrigDist);
                //var NewDist = GroundTruth.HaversineDistance(PostLoc);
                //ErrorPrior.Add(NewDist);

                LastLocation = Loc;

            }
            return Tuple.Create(Accuracy, Samples, Time);
        }

        static void Main(string[] args)
        {
            string InputPath = "..\\..\\..\\Data\\BusTrip_Induced.csv";
            string RoadPath = "..\\..\\..\\Data\\RoadNetwork.txt";
            string BlacklistPath = "..\\..\\..\\Data\\GroundTruthBlacklist.txt";

            // Read the input data
            Console.WriteLine("Parsing input file");
            List<GPSObservation> Locations = new List<GPSObservation>();
            IFormatProvider Culture = new CultureInfo("en-US");
            var Reader = new StreamReader(InputPath);
            string Line;
            while ((Line = Reader.ReadLine()) != null)
            {
                string[] Columns = Line.Split(',');

                if (Columns.Length != 5) continue;
                if (Columns[1] == "Mark") continue;

                double Latitude = Double.Parse(Columns[1]);
                double Longitude = Double.Parse(Columns[2]);
                double Accuracy = Double.Parse(Columns[3]);
                DateTimeOffset Timestamp = DateTimeOffset.Parse(Columns[4], Culture);

                if (Double.IsNaN(Latitude) || Double.IsNaN(Longitude) || Double.IsNaN(Accuracy)) continue;

                Locations.Add(new GPSObservation(Latitude, Longitude, Accuracy, Timestamp));
            }
            Reader.Close();

            Console.WriteLine("Parsing road network data");
            RoadDB DB = new RoadDB(RoadPath);
            Console.WriteLine("Reading ground truth blacklist");
            var Blacklist = new HashSet<int>(File.ReadAllLines(BlacklistPath).Select(s => Int32.Parse(s)));

            ////////////////////////////////////////////////////////////////////////////////////////
            // Run an experiment to generate output on the map (Output.html)

            if (true)
            {
                Console.WriteLine("Computing output");
                string OutputPath = Path.GetDirectoryName(InputPath) + "\\Output.js";
                var OutputWriter = new StreamWriter(OutputPath);
                OutputWriter.WriteLine("var OutputData = [");
                var Roads = new Dictionary<int, Point[]>();

                Func<GPSObservation, Point, Point, int, double, double, int> OutputCallback = (PN, PP, PG, Idx, ErrorNaive, ErrorPrior) => {
                    var S = "[" + PN.Center.Latitude + ", " + PN.Center.Longitude + ", " + PN.HorizontalAccuracy + ", " +
                        PP.Latitude + ", " + PP.Longitude + ", " + PN.HorizontalAccuracy + ", " +
                        PG.Latitude + ", " + PG.Longitude + ", " + Idx + ", " + ErrorNaive + ", " + ErrorPrior + "],";
                    OutputWriter.WriteLine(S);
                    return 0;
                };
                Func<int, Point[], int> RoadCallback = (Idx, Pts) => {
                    Roads[Idx] = Pts;
                    return 0;
                };

                var Result = RunExperiment(50.0, 10000, 1.0, Locations, DB, Blacklist, OutputCallback, RoadCallback);
                var AvgErrorNaive = Result.Item1;
                var AvgErrorPrior = Result.Item2;

                OutputWriter.WriteLine("];");
                OutputWriter.WriteLine();
                OutputWriter.WriteLine("var Roads = {");
                foreach (var Pair in Roads)
                {
                    int Idx = Pair.Key;
                    Point[] Road = Pair.Value;
                    var S = String.Join(", ", Road.Select(pt => "[" + pt.Latitude + ", " + pt.Longitude + "]"));
                    OutputWriter.WriteLine(Idx + ": [" + S + "],");
                }
                OutputWriter.WriteLine("};");

                OutputWriter.Close();

                Console.WriteLine("Average error without prior: {0:F3}m", AvgErrorNaive.Mean());
                Console.WriteLine("Average error with prior: {0:F3}m", AvgErrorPrior.Mean());
            }

            ////////////////////////////////////////////////////////////////////////////////////////
            // Run experiment to explore the effect of prior weight

            if (false)
            {
                var ExperimentWriter = new StreamWriter(Path.GetDirectoryName(InputPath) + "\\Experiment-Weight.csv");
                ExperimentWriter.WriteLine("RoadWeight,SampleSize,ErrorNaive_Avg,ErrorNaive_Var,ErrorPrior_Avg,ErrorPrior_Var,SampleTime_Avg,SampleTime_Var");

                var Weights = new double[] { 1.0, 2.0, 3.0, 5.0, 10.0, 25.0, 50.0 };
                var N = 10000;
                foreach (var W in Weights)
                {
                    var R = RunExperiment(W, N, 1.0, Locations, DB, Blacklist, (a, b, c, d, e, f) => 0, (a, b) => 0);
                    ExperimentWriter.WriteLine("{0:F0},{1:D},{2:F6},{3:F6},{4:F6}", W, N, R.Item1.Str(), R.Item2.Str(), R.Item3.Str());
                    Console.WriteLine("{0:F0},{1:D},{2:F6},{3:F6},{4:F6}", W, N, R.Item1.Str(), R.Item2.Str(), R.Item3.Str());
                }
                ExperimentWriter.Close();
            }

            ////////////////////////////////////////////////////////////////////////////////////////
            // Run experiment to explore the effect of sample size

            if (false)
            {
                var ExperimentWriter = new StreamWriter(Path.GetDirectoryName(InputPath) + "\\Experiment-Samples.csv");
                ExperimentWriter.WriteLine("RoadWeight,SampleSize,ErrorNaive_Avg,ErrorNaive_Var,ErrorPrior_Avg,ErrorPrior_Var,SampleTime_Avg,SampleTime_Var");

                var Weights = new double[] { 1.0, 50.0 }; //{ 1.0, 2.0, 3.0, 5.0, 10.0, 25.0, 50.0 };
                var Ns = new int[] { 100, 1000, 2000, 5000, 10000, 25000 };
                foreach (var W in Weights)
                {
                    foreach (var N in Ns)
                    {
                        var R = RunExperiment(W, N, 1.0, Locations, DB, Blacklist, (a, b, c, d, e, f) => 0, (a, b) => 0);
                        ExperimentWriter.WriteLine("{0:F0},{1:D},{2:F6},{3:F6},{4:F6}", W, N, R.Item1.Str(), R.Item2.Str(), R.Item3.Str());
                        Console.WriteLine("{0:F0},{1:D},{2:F6},{3:F6},{4:F6}", W, N, R.Item1.Str(), R.Item2.Str(), R.Item3.Str());
                    }
                }
                ExperimentWriter.Close();
            }

            ////////////////////////////////////////////////////////////////////////////////////////
            // Run experiment to explore the effect of search radius

            if (false)
            {
                var ExperimentWriter = new StreamWriter(Path.GetDirectoryName(InputPath) + "\\Experiment-Radius.csv");
                ExperimentWriter.WriteLine("RoadWeight,SampleSize,ErrorNaive_Avg,ErrorNaive_Var,ErrorPrior_Avg,ErrorPrior_Var,SampleTime_Avg,SampleTime_Var");

                var Weights = new double[] { 1.0, 50.0 }; //{ 1.0, 2.0, 3.0, 5.0, 10.0, 25.0, 50.0 };
                var N = 1000;
                var Radii = new double[] { 1 / 6.0, 1 / 4.0, 1 / 3.0, 5 / 12.0, 1 / 2.0, 2 / 3.0, 1, 5 / 3.0, 10 / 3.0 }; // 0.5, 0.75, 1, 1.25, 1.5, 2, 3, 5, 10x
                foreach (var W in Weights)
                {
                    foreach (var Rd in Radii)
                    {
                        var R = RunExperiment(W, N, Rd, Locations, DB, Blacklist, (a, b, c, d, e, f) => 0, (a, b) => 0);
                        ExperimentWriter.WriteLine("{0:F0},{1:F2},{2:F6},{3:F6},{4:F6}", W, Rd * 3, R.Item1.Str(), R.Item2.Str(), R.Item3.Str());
                        Console.WriteLine("{0:F0},{1:F2},{2:F6},{3:F6},{4:F6}", W, Rd * 3, R.Item1.Str(), R.Item2.Str(), R.Item3.Str());
                    }
                }
                ExperimentWriter.Close();
            }

            ////////////////////////////////////////////////////////////////////////////////////////
            // Run experiment to explore sample sizes from sqeuential sampling

            if (false)
            {
                var ExperimentWriter = new StreamWriter(Path.GetDirectoryName(InputPath) + "\\Experiment-Query.csv");
                ExperimentWriter.WriteLine("RoadWeight,SamplesLHRW_Avg,SamplesLHRW_Var,SamplesRej_Avg,SamplesRej_Var,DecisionsLHRW_Avg,DecisionsLHRW_Var,DecisionsRej_Avg,DecisionsRej_Var,TimeLHRW_Avg,TimeLHRW_Var,TimeRej_Avg,TimeRej_Var");

                var Weights = new double[] { 1.0, 2.0, 3.0, 5.0, 10.0, 25.0, 50.0 };
                foreach (var W in Weights)
                {
                    var R = RunExperimentQuery(W, 1.0, Locations, DB, Blacklist);
                    ExperimentWriter.WriteLine("{0:F0},{1:S},{2:S},{3:S},{4:S},{5:S},{6:S}", W, R.Item1.Str(), R.Item2.Str(), R.Item3.Str(), R.Item4.Str(), R.Item5.Str(), R.Item6.Str());
                    Console.WriteLine("{0:F0},{1:S},{2:S},{3:S},{4:S},{5:S},{6:S}", W, R.Item1.Str(), R.Item2.Str(), R.Item3.Str(), R.Item4.Str(), R.Item5.Str(), R.Item6.Str());
                }
                ExperimentWriter.Close();
            }

            ////////////////////////////////////////////////////////////////////////////////////////
            // Run experiment to explore perf-accuracy tradeoff

            if (false)
            {
                var ExperimentWriter = new StreamWriter(Path.GetDirectoryName(InputPath) + "\\Experiment-Accuracy.csv");
                ExperimentWriter.WriteLine("RoadWeight,Confidence,Accuracy_Avg,Accuracy_Var,Samples_Avg,Samples_Var,Time_Avg,Time_Var");

                var Weights = new double[] { 1.0, 2.0, 3.0, 5.0, 10.0, 25.0, 50.0 };
                var Ps = new double[] { 0.5, 0.6, 0.7, 0.8, 0.9, 0.95 };
                foreach (var W in Weights)
                {
                    foreach (var A in Ps)
                    {
                        var R = RunExperimentAccuracy(W, 1.0, A, Locations, DB, Blacklist);
                        ExperimentWriter.WriteLine("{0:F0},{1:F2},{2:S},{3:S},{4:S}", W, A, R.Item1.Str(), R.Item2.Str(), R.Item3.Str());
                        Console.WriteLine("{0:F0},{1:F2},{2:S},{3:S},{4:S}", W, A, R.Item1.Str(), R.Item2.Str(), R.Item3.Str());
                        ExperimentWriter.Flush();
                    }
                }
                ExperimentWriter.Close();
            }

            Console.WriteLine("Done.");
            Console.ReadKey();
        }
    }
}
