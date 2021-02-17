using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using UGCS.Sdk.Protocol;
using UGCS.Sdk.Protocol.Encoding;

namespace ACE_Mission_Control.Core.Models
{
    public class WaypointRouteIntercept
    {
        public WaypointRoute WaypointRoute { get; set; }
        public Coordinate EntryCoordinate { get; set; }
        public Coordinate ExitCoordinate { get; set; }
    }

    public class WaypointRoute : LineString, IComparableRoute
    {
        public int Id { get; protected set; }
        public long LastModificationTime { get; protected set; }

        public string Name;

        private readonly List<Waypoint> waypoints;
        public List<Waypoint> Waypoints => waypoints;

        // Expects radians in Long Lat format
        public WaypointRoute(int id, string name, long modifiedTime, List<Waypoint> coords) : base(coords.Select(c => c.Coordinate).ToArray())
        {
            Id = id;
            Name = name;
            LastModificationTime = modifiedTime;
            waypoints = coords;
        }

        public static WaypointRoute CreateFromUGCSRoute(Route route)
        {
            List<Waypoint> coords = new List<Waypoint>();

            // UGCS Waypoints: A waypoint route is represented by a series of segments with single figure points
            foreach (SegmentDefinition segment in route.Segments)
            {
                if (!IsFigureWaypoint(segment.Figure))
                    continue;

                var point = segment.Figure.Points[0];
                var turnType = segment.ParameterValues.FirstOrDefault(p => p.Name == "wpTurnType")?.Value;
                coords.Add(new Waypoint(segment.Uuid, turnType, new Coordinate(point.Longitude, point.Latitude)));
            }

            return new WaypointRoute(route.Id, route.Name, route.LastModificationTime, coords);
        }

        public static bool IsUGCSRouteWaypointRoute(Route route)
        {
            if (route.Segments == null || route.Segments.Count < 2)
                return false;

            var firstFigure = route.Segments[0].Figure;

            if (!route.Segments.Any(segment => IsFigureWaypoint(segment.Figure)))
                return false;

            return true;
        }

        private static bool IsFigureWaypoint(Figure figure)
        {
            return figure != null &&
                (figure.Type == FigureType.FT_POINT ||
                figure.Type == FigureType.FT_TAKEOFF_POINT ||
                figure.Type == FigureType.FT_LANDING_POINT) &&
                figure.Points[0].LongitudeSpecified &&
                figure.Points[0].LatitudeSpecified;
        }

        public Coordinate CalcIntersectWithArea(AreaScanPolygon area, bool reverse=false)
        {
            return CalcIntersectWithArea(area, GetLineSegments(), reverse);
        }

        public Coordinate CalcIntersectWithArea(AreaScanPolygon area, IEnumerable<LineSegment> specificSegments, bool reverse = false)
        {
            var areaScanSegments = area.GetLineSegments().ToList();

            var waypoints = specificSegments;
            if (reverse)
                waypoints = waypoints.Reverse();

            var intersector = new RobustLineIntersector();

            foreach (LineSegment waypointSegment in waypoints)
            {
                List<Coordinate> segmentIntersections = new List<Coordinate>();
                foreach (LineSegment areaSegment in areaScanSegments)
                {
                    intersector.ComputeIntersection(waypointSegment.P0, waypointSegment.P1, areaSegment.P0, areaSegment.P1);
                    if (intersector.IsProper)
                        segmentIntersections.Add(intersector.GetIntersection(0));
                }

                if (segmentIntersections.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("Intersect with Area");
                    System.Diagnostics.Debug.WriteLine($"Pair is {waypointSegment.P0}, {waypointSegment.P1}");
                    foreach (Coordinate coord in segmentIntersections)
                        System.Diagnostics.Debug.WriteLine($"Intersection at {coord.X}, {coord.Y}");

                    if (reverse)
                        return segmentIntersections.OrderBy(intersect => intersect.Distance(waypointSegment.P1)).First();
                    else
                        return segmentIntersections.OrderBy(intersect => intersect.Distance(waypointSegment.P0)).First();
                }
            }

            return null;
        }

        public Coordinate CalcIntersectAfterWaypoint(Waypoint waypoint, AreaScanPolygon area)
        {
            var index = Waypoints.FindIndex(w => w.ID == waypoint.ID);
            var slicedWaypoints = Waypoints.GetRange(index, Waypoints.Count - index - 1);
            WaypointRoute subRoute = new WaypointRoute(0, "", 0, slicedWaypoints);

            return subRoute.CalcIntersectWithArea(area);
        }

        public Coordinate CalcIntersectAfterCoordinate(Coordinate coord, float findWaypointRange, AreaScanPolygon area)
        {
            var waypPair = FindWaypointPairAroundCoordinate(coord, findWaypointRange);
            if (waypPair == null)
                return null;

            bool wayp1Intersects = waypPair.Item1.IntersectsArea(area);
            bool wayp2Intersects = waypPair.Item2.IntersectsArea(area);

            System.Diagnostics.Debug.WriteLine("Calc Intersect After Coordinate");

            if (wayp1Intersects && wayp2Intersects)
            {
                IEnumerable<LineSegment> segment = new List<LineSegment> { new LineSegment(waypPair.Item1.Coordinate, waypPair.Item2.Coordinate) };
                bool closerToWayp2 = waypPair.Item1.Coordinate.Distance(coord) > waypPair.Item2.Coordinate.Distance(coord);
                return CalcIntersectWithArea(area, segment, reverse: closerToWayp2); 
            }
            else if (!wayp1Intersects)
            {
                return CalcIntersectAfterWaypoint(waypPair.Item1, area);
            }
            else
            {
                return CalcIntersectAfterWaypoint(waypPair.Item2, area);
            }
        }

        public Waypoint FindWaypointInArea(Coordinate areaCentre, float areaMetres)
        {
            var bufferRadiansDist = AviationFormularyApproxRadiansDiff(areaCentre, areaMetres, 0);
            Geometry bufferedPoint = Factory.CreatePoint(areaCentre).Buffer(bufferRadiansDist);

            foreach (Waypoint wayp in Waypoints)
            {
                var coordAsPoint = Factory.CreatePoint(wayp.Coordinate);
                if (bufferedPoint.Intersects(coordAsPoint))
                    return wayp;
            }

            return null;
        }

        public Tuple<Waypoint, Waypoint> FindWaypointPairAroundCoordinate(Coordinate coord, float bufferMetres)
        {
            var bufferRadiansDist = AviationFormularyApproxRadiansDiff(coord, bufferMetres, 0);
            Geometry bufferedPoint = Factory.CreatePoint(coord).Buffer(bufferRadiansDist);

            Tuple<Waypoint, Waypoint> intersectionPair = null;
            double intersectionLength = 0;
            
            foreach (Tuple<Waypoint, Waypoint> pair in GetWaypointPairs())
            {
                var lineString = Factory.CreateLineString(new Coordinate[] {pair.Item1.Coordinate, pair.Item2.Coordinate});
                
                if (bufferedPoint.Intersects(lineString))
                {
                    var newLengthIntersected = bufferedPoint.Intersection(lineString).Length;
                    if (newLengthIntersected > intersectionLength)
                    {
                        intersectionPair = pair;
                        intersectionLength = newLengthIntersected;
                        System.Diagnostics.Debug.WriteLine($"Intersection length {intersectionLength}");
                    }
                }
            }

            return intersectionPair;
        }

        public bool DoesRoutePassCoordinate(Coordinate coord, float bufferMetres)
        {
            var preceedingWaypoint = FindWaypointPairAroundCoordinate(coord, bufferMetres);
            // If it has a preceeding waypoint, then the route does pass through the waypoint
            return preceedingWaypoint != null;
        }

        public IEnumerable<LineSegment> GetLineSegments()
        {
            for (int i = 0; i < NumPoints - 1; i++)
            {
                var coord = Coordinates[i];
                var nextCoord = Coordinates[i + 1];
                yield return new LineSegment(coord, nextCoord);
            }
        }

        public IEnumerable<Tuple<Waypoint, Waypoint>> GetWaypointPairs()
        {
            for (int i = 0; i < NumPoints - 1; i++)
            {
                var coord = Waypoints[i];
                var nextCoord = Waypoints[i + 1];
                yield return new Tuple<Waypoint, Waypoint>(coord, nextCoord);
            }
        }

        public IEnumerable<Tuple<double, double>> GetBasicCoordinates()
        {
            foreach (Coordinate coord in Coordinates)
                yield return new Tuple<double, double>(coord.X, coord.Y);
        }

        public static bool IsCoordinateInArea(Waypoint coordinate, Coordinate areaCentre, float areaMetres)
        {
            var bufferRadiansDist = AviationFormularyApproxRadiansDiff(areaCentre, areaMetres, 0);
            Geometry bufferedPoint = new Point(areaCentre).Buffer(bufferRadiansDist);

            var coordAsPoint = new Point(coordinate.Coordinate);
            if (bufferedPoint.Intersects(coordAsPoint))
                return true;

            return false;
        }

        // Approximates the difference in radians from the coordinate to another position specified by north-east offset in metres
        // Only reliable for short distances (5% error I think?)
        private static double AviationFormularyApproxRadiansDiff(Coordinate coord, float northMetres, float eastMetres)
        {
            double longDeg = (coord.X / Math.PI) * 180;
            double latDeg = (coord.Y / Math.PI) * 180;

            //Earth’s radius, sphere
            var R = 6378137;

            // Coordinate difference in radians
            double dLat = northMetres / R;
            double dLon = eastMetres / (R * Math.Cos(Math.PI * latDeg / 180));

            // Return the length of the difference
            return Math.Sqrt((dLon * dLon) + (dLat * dLat));
        }
    }
}
