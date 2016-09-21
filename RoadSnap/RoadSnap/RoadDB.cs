using Microsoft.Research.Uncertain.Gps;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoadSnap
{
    /// <summary>
    /// Represents a road network that can be queried to find roads near a given point.
    /// </summary>
    /// <remarks>
    /// To support fast querying, the road network is divided into a grid. Each grid cell records
    /// which roads pass through that cell.
    /// </remarks>
    class RoadDB
    {
        /// <summary>
        /// A road is a list of vertices
        /// </summary>
        private List<Point[]> Roads = new List<Point[]>();

        /// <summary>
        /// The number of cells along each dimension of the grid used to query the road DB
        /// </summary>
        private int GridSize;
        /// <summary>
        /// Each cell stores a set of indices into <c>Roads</c>
        /// </summary>
        private HashSet<int>[,] RoadGrid;
        /// <summary>
        /// The south-west and north-east corners of the grid
        /// </summary>
        private Point MinPoint, MaxPoint;
        /// <summary>
        /// The number of degrees of latitude and longitude spanned by each grid cell
        /// </summary>
        private double DLat, DLng;

        /// <summary>
        /// The maximum query radius allowed, to ensure queries only need look at neighbouring
        /// cells in the grid
        /// </summary>
        private double MaxQueryRadius = 100;
        /// <summary>
        /// The factor to dilate the given radius to find nearby roads
        /// </summary>
        private double SearchRadius = 3.0;

        /// <summary>
        /// Create a road database from the given data file
        /// </summary>
        /// <param name="Path">The path to the road network file</param>
        /// <param name="GridSize">The number of cells in each dimension of the grid</param>
        public RoadDB(string Path, int GridSize = 100)
        {
            this.LoadRoads(Path);
            this.InitializeGrid(GridSize);
        }

        /// <summary>
        /// Initialise the grid by assigning each road in the database to the grid cells it passes
        /// </summary>
        /// <param name="GridSize">The number of cells in each dimension of the grid</param>
        private void InitializeGrid(int GridSize)
        {
            this.GridSize = GridSize;
            Debug.Assert(Roads.Count > 0);

            // Initialise the grid containers
            RoadGrid = new HashSet<int>[GridSize, GridSize];
            for (int i = 0; i < GridSize; i++)
            {
                for (int j = 0; j < GridSize; j++)
                {
                    RoadGrid[i, j] = new HashSet<int>();
                }
            }

            // First pass: find the min and max point so we can determine the grid
            MinPoint = new Point(Roads[0][0]);
            MaxPoint = new Point(Roads[0][0]);
            Tuple<Point, Point>[] BoundingBoxes = new Tuple<Point, Point>[Roads.Count];
            for (int i = 0; i < Roads.Count; i++)
            {
                Tuple<Point, Point> BBox = BoundingBox(Roads[i]);
                BoundingBoxes[i] = BBox;

                if (BBox.Item1.Latitude < MinPoint.Latitude) MinPoint.Latitude = BBox.Item1.Latitude;
                if (BBox.Item1.Longitude < MinPoint.Longitude) MinPoint.Longitude = BBox.Item1.Longitude;

                if (BBox.Item2.Latitude > MaxPoint.Latitude) MaxPoint.Latitude = BBox.Item2.Latitude;
                if (BBox.Item2.Longitude > MaxPoint.Longitude) MaxPoint.Longitude = BBox.Item2.Longitude;
            }

            // Second pass: a road is in grid(x,y) if any of its vertices are in that grid's area
            DLat = Math.Abs(MaxPoint.Latitude - MinPoint.Latitude) / GridSize;
            DLng = Math.Abs(MaxPoint.Longitude - MinPoint.Longitude) / GridSize;
            for (int i = 0; i < Roads.Count; i++)
            {
                Point MinPt = BoundingBoxes[i].Item1;
                Point MaxPt = BoundingBoxes[i].Item2;

                // Figure out the range of x (lat) and y (lng) cells that this road passes through
                int FirstY = GetGridIndex(MinPt.Latitude, MinPoint.Latitude, DLat);
                int LastY = GetGridIndex(MaxPt.Latitude, MinPoint.Latitude, DLat);
                int FirstX = GetGridIndex(MinPt.Longitude, MinPoint.Longitude, DLng);
                int LastX = GetGridIndex(MaxPt.Longitude, MinPoint.Longitude, DLng);

                for (int x = FirstX; x <= LastX; x++)
                {
                    for (int y = FirstY; y <= LastY; y++)
                    {
                        RoadGrid[x, y].Add(i);
                    }
                }
            }

            // The max query radius ensures we only need to look at a cell's immediate neighbours.
            // Northern hemisphere, where MaxPoint is the most extreme latitude
            MaxQueryRadius = Math.Min(MaxQueryRadius, MaxPoint.HaversineDistance(new Point(MaxPoint.Latitude, MaxPoint.Longitude + DLng)));
            MaxQueryRadius = Math.Min(MaxQueryRadius, MaxPoint.HaversineDistance(new Point(MaxPoint.Latitude + DLat, MaxPoint.Longitude)));
            // Southern hemisphere, where MinPoint is the most extreme latitude
            MaxQueryRadius = Math.Min(MaxQueryRadius, MinPoint.HaversineDistance(new Point(MinPoint.Latitude, MinPoint.Longitude + DLng)));
            MaxQueryRadius = Math.Min(MaxQueryRadius, MinPoint.HaversineDistance(new Point(MinPoint.Latitude + DLat, MinPoint.Longitude)));
        }

        /// <summary>
        /// Calculate the index in the grid of a given coordinate
        /// </summary>
        /// <param name="Coord">The coordinate to locate</param>
        /// <param name="MinCoord">The minimum coordinate in the relevant direction</param>
        /// <param name="GridStep">The number of degrees per grid cell</param>
        /// <returns>The index of the given point along the relevant direction in the grid</returns>
        private int GetGridIndex(double Coord, double MinCoord, double GridStep)
        {
            return Math.Max(0, Math.Min((int)Math.Floor((Coord - MinCoord) / GridStep), this.GridSize - 1));
        }

        /// <summary>
        /// Calculate the bounding box of a set of points
        /// </summary>
        /// <param name="Points">The points to bound</param>
        /// <returns>A tuple containing the south-west and north-east corners of the box</returns>
        private Tuple<Point, Point> BoundingBox(Point[] Points)
        {
            Debug.Assert(Points.Length > 0);
            Point MinPt = new Point(Points[0]);
            Point MaxPt = new Point(Points[0]);
            foreach (Point P in Points)
            {
                if (P.Latitude < MinPt.Latitude) MinPt.Latitude = P.Latitude;
                else if (P.Latitude > MaxPt.Latitude) MaxPt.Latitude = P.Latitude;

                if (P.Longitude < MinPt.Longitude) MinPt.Longitude = P.Longitude;
                else if (P.Longitude > MaxPt.Longitude) MaxPt.Longitude = P.Longitude;
            }
            return Tuple.Create(MinPt, MaxPt);
        }

        /// <summary>
        /// Parse a road network file and store the roads
        /// </summary>
        /// <param name="Path">Path to the road network file</param>
        private void LoadRoads(string Path)
        {
            var Reader = new StreamReader(Path);
            string Line;
            while ((Line = Reader.ReadLine()) != null)
            {
                string[] Columns = Line.Split(',');
                if (Columns.Length == 0) continue;

                Debug.Assert(Columns.Length % 2 == 0);

                Point[] Points = new Point[Columns.Length / 2];
                for (int i = 0; i < Columns.Length / 2; i++)
                {
                    double Lat = Double.Parse(Columns[2 * i]);
                    double Lng = Double.Parse(Columns[2 * i + 1]);
                    Points[i] = new Point(Lat, Lng);
                }
                Roads.Add(Points);
            }
            Reader.Close();
        }

        /// <summary>
        /// Query the road network to find all points passing within a square of side length
        /// 2*<paramref name="Radius"/> centered at <paramref name="Centre"/>.
        /// </summary>
        /// <param name="Centre">The centre of the query box</param>
        /// <param name="Radius">The "radius" of the query box (actually half the side length)</param>
        /// <returns>A list of roads that pass through the given query box</returns>
        public IEnumerable<Point[]> Query(Point Centre, double Radius)
        {
            Radius = Math.Min(Radius, MaxQueryRadius);

            // Find the south-west and north-east corners of the query box
            double MaxLat = GeoMath.PointFromCentreDistanceBearing(Centre, Radius, 0).Latitude;
            double MaxLng = GeoMath.PointFromCentreDistanceBearing(Centre, Radius, 90).Longitude;
            double MinLat = GeoMath.PointFromCentreDistanceBearing(Centre, Radius, 180).Latitude;
            double MinLng = GeoMath.PointFromCentreDistanceBearing(Centre, Radius, 270).Longitude;

            // Which grid indices does this box cover?
            int FirstY = GetGridIndex(MinLat, MinPoint.Latitude, DLat);
            int LastY = GetGridIndex(MaxLat, MinPoint.Latitude, DLat);
            int FirstX = GetGridIndex(MinLng, MinPoint.Longitude, DLng);
            int LastX = GetGridIndex(MaxLng, MinPoint.Longitude, DLng);

            // Find all roads in those query boxes
            var Result = Enumerable.Empty<Point[]>();
            for (int x = FirstX; x <= LastX; x++)
            {
                for (int y = FirstY; y <= LastY; y++)
                {
                    Result = Result.Concat(RoadGrid[x, y].Select(i => Roads[i]));
                }
            }

            return Result;
        }

        public IEnumerable<int> QueryIdx(Point Centre, double Radius)
        {
            Radius = Math.Min(Radius, MaxQueryRadius);

            // Find the south-west and north-east corners of the query box
            double MaxLat = GeoMath.PointFromCentreDistanceBearing(Centre, Radius, 0).Latitude;
            double MaxLng = GeoMath.PointFromCentreDistanceBearing(Centre, Radius, 90).Longitude;
            double MinLat = GeoMath.PointFromCentreDistanceBearing(Centre, Radius, 180).Latitude;
            double MinLng = GeoMath.PointFromCentreDistanceBearing(Centre, Radius, 270).Longitude;

            // Which grid indices does this box cover?
            int FirstY = GetGridIndex(MinLat, MinPoint.Latitude, DLat);
            int LastY = GetGridIndex(MaxLat, MinPoint.Latitude, DLat);
            int FirstX = GetGridIndex(MinLng, MinPoint.Longitude, DLng);
            int LastX = GetGridIndex(MaxLng, MinPoint.Longitude, DLng);

            // Find all roads in those query boxes
            var Result = Enumerable.Empty<int>();
            for (int x = FirstX; x <= LastX; x++)
            {
                for (int y = FirstY; y <= LastY; y++)
                {
                    Result = Result.Concat(RoadGrid[x, y]);
                }
            }

            return Result;
        }

        /// <summary>
        /// Take a list of roads and return the segments of those roads that are contained in a
        /// circle centred at <paramref name="Centre"/> with radius <paramref name="Radius"/>. Each
        /// returned segment is trimmed so that its endpoints lie (approximately) on the circle.
        /// </summary>
        /// <param name="Roads">The roads to consider</param>
        /// <param name="Centre">The centre of the query circle</param>
        /// <param name="Radius">The radius of the query circle</param>
        /// <returns>A list of road segments; each segment is a tuple of its start and end points
        /// </returns>
        private List<Tuple<Point, Point>> RoadsToNearbySegments(IEnumerable<Point[]> Roads, Point Centre, double Radius)
        {
            // Need the radius in degrees; this is a conservative approximation (it's too big in the
            // north-south direction)
            var RadiusDegrees = GeoMath.PointFromCentreDistanceBearing(Centre, Radius, 90).Longitude - Centre.Longitude;

            var Result = new List<Tuple<Point, Point>>();
            foreach (Point[] Road in Roads)
            {
                Debug.Assert(Road.Length >= 2);
                for (int i = 0; i < Road.Length - 1; i++)
                {
                    Point A = Road[i];
                    Point B = Road[i + 1];

                    // Does the road intersect the query circle? To find out, we first compute
                    // whether the infinite ray defined by AB intersects the circle, and if so, test
                    // whether those intersections actually lie on the segment AB.
                    // From http://stackoverflow.com/questions/1073336/circle-line-collision-detection
                    Point d = B - A;
                    Point f = A - Centre;

                    // Coefficients of the quadratic equation for the intersection times
                    double a = d.Dot(d);
                    double b = 2 * f.Dot(d);
                    double c = f.Dot(f) - RadiusDegrees * RadiusDegrees;

                    // If the discriminant is negative there are no solutions. If it is zero, the
                    // line is tangent to the circle, which for our purposes is "not intersecting".
                    double Disc = b * b - 4 * a * c;
                    if (Disc <= 0) continue;
                    Disc = Math.Sqrt(Disc);

                    // The two times at which the line intersects the circle. Each time is a
                    // parameter of the equation A + t*d which specifies the coordinates of the
                    // intersection. When t=0, A + t*d = A, and when t=1, A + t*d = B.
                    // Note that this arrangement guarantees that t2 > t1 since b and Disc are
                    // positive.
                    double t1 = (-b - Disc) / (2 * a);
                    double t2 = (-b + Disc) / (2 * a);

                    // If both times are less than zero or greater than one, the line segment AB
                    // does not touch the circle.
                    if (t2 < 0 || t1 > 1) continue;

                    // Figure out the coordinates of intersection. The resulting line segment is
                    // wholly within the circle.
                    Point NewA = t1 < 0 ? A : (A + t1 * d);
                    Point NewB = t2 > 1 ? B : (A + t2 * d);

                    Result.Add(Tuple.Create(NewA, NewB));
                }
            }

            return Result;
        }

        public Tuple<Point, int> ClosestRoadToPoint(Point Centre, double Radius, ISet<int> Blacklist)
        {
            var Roads = QueryIdx(Centre, SearchRadius * Radius);
            int MinIdx = -1;
            double MinDist = Double.MaxValue;
            Point MinPoint = new Point(0, 0);
            foreach (int Idx in Roads)
            {
                if (Blacklist.Contains(Idx)) continue;
                var Road = this.Roads[Idx];
                Point A = Road[0];
                foreach (Point B in Road.Skip(1))
                {
                    var AP = Centre - A;
                    var AB = B - A;
                    var ABLen = Math.Pow(AB.Latitude, 2) + Math.Pow(AB.Longitude, 2);
                    var Dot = AP.Dot(AB);
                    var T = Dot / ABLen;
                    T = Math.Min(1, Math.Max(0, T));
                    var P = new Point(A.Latitude + AB.Latitude * T, A.Longitude + AB.Longitude * T);
                    var Dist = P.HaversineDistance(Centre);
                    if (Dist < MinDist)
                    {
                        MinIdx = Idx;
                        MinDist = Dist;
                        MinPoint = P;
                    }
                }
            }
            return Tuple.Create(MinPoint, MinIdx);
        }

        public Point[] GetRoadByIdx(int Idx)
        {
            return Roads[Idx];
        }

        /// <summary>
        /// Create a map prior of roads from a given <paramref name="Centre"/> in a given 
        /// <paramref name="Radius"/>. The roads will be weighted to be <paramref name="Weight"/>
        /// times more likely than surrounding non-road points.
        /// </summary>
        /// <param name="Centre">The centre of the query circle</param>
        /// <param name="Radius">The radius of the query circle</param>
        /// <param name="Weight">How strongly to prefer roads over non-roads</param>
        /// <returns>A prior distribution incorporating the map data</returns>
        public MapPrior MapQuery(Point Centre, double Radius, double Weight = 5.0)
        {
            // We dilate the given radius when looking for roads
            var Roads = Query(Centre, SearchRadius * Radius);
            var Segments = RoadsToNearbySegments(Roads, Centre, SearchRadius * Radius);
            return new MapPrior(Segments, Centre, Radius, Weight);
        }
    }
}
