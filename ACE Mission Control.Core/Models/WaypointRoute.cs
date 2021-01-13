using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        

        public WaypointRoute(int id, string name, long modifiedTime, Coordinate[] points) : base(points)
        {
            Id = id;
            Name = name;
            LastModificationTime = modifiedTime;
        }

        public static WaypointRoute CreateFromUGCSRoute(Route route)
        {
            // UGCS Waypoints: A waypoint route is represented by a series of segments with single figure points
            var coordinates = route.Segments.ConvertAll(
                segment => 
                {
                    if (!IsFigureWaypoint(segment.Figure))
                        return null;

                    var point = segment.Figure.Points[0];
                    return new Coordinate(point.Longitude, point.Latitude);
                });

            return new WaypointRoute(route.Id, route.Name, route.LastModificationTime, coordinates.ToArray());
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
            return (figure.Type == FigureType.FT_POINT ||
                figure.Type == FigureType.FT_TAKEOFF_POINT ||
                figure.Type == FigureType.FT_LANDING_POINT) &&
                figure.Points[0].LongitudeSpecified &&
                figure.Points[0].LatitudeSpecified;
        }

        public Coordinate CalcIntersectWithArea(AreaScanPolygon area, bool reverse=false)
        {
            var areaScanSegments = area.GetLineSegments().ToList();
            Coordinate intersect;

            var waypoints = GetLineSegments();
            if (reverse)
            {
                waypoints = waypoints.ToList();
                waypoints.Reverse();
            }

            foreach (LineSegment waypointSegment in GetLineSegments())
            {
                foreach (LineSegment areaSegment in areaScanSegments)
                {
                    intersect = waypointSegment.LineIntersection(areaSegment);
                    if (intersect != null)
                    {
                        return intersect;
                    }
                }
            }

            return null;
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

        //public string GetVerticesString()
        //{
        //    string vertString = "";
        //    foreach (BasicGeoposition position in Area.Positions)
        //    {
        //        if (vertString.Length != 0)
        //            vertString = vertString + ";";
        //        vertString = vertString + string.Format("{0},{1}", (Math.PI / 180) * position.Latitude, (Math.PI / 180) * position.Longitude);
        //    }
        //    return vertString;
        //}

        //public string GetEntryVetexString()
        //{
        //    string entryString = string.Format(
        //        "{0},{1}",
        //        Area.Positions[EntryVertex].Latitude,
        //        Area.Positions[EntryVertex].Longitude);
        //    return entryString;
        //}
    }
}
