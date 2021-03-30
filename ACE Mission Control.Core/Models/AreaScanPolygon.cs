using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetTopologySuite.Geometries;
using UGCS.Sdk.Protocol;
using UGCS.Sdk.Protocol.Encoding;

namespace ACE_Mission_Control.Core.Models
{
    public class AreaScanPolygon : Polygon, IComparableRoute<AreaScanPolygon>
    {
        private static List<int> knownIdList = new List<int>();

        // An ID assigned from a sequence, starting at 0 and incremented with each new UGCS ID seen
        public int SequentialID { get; protected set; }

        // An ID assigned by UGCS
        public int Id { get; protected set; }

        public long LastModificationTime { get; protected set; }

        public Dictionary<string, string> Parameters;

        public string Name;

        public AreaScanPolygon(int id, string name, long modifiedTime, LinearRing polygonPoints, Dictionary<string, string> parameters) : base(polygonPoints)
        {
            Id = id;

            if (!knownIdList.Contains(id))
                knownIdList.Add(id);
            SequentialID = knownIdList.IndexOf(id);

            Name = name;
            LastModificationTime = modifiedTime;
            Parameters = parameters;
        }

        public static AreaScanPolygon CreateFromUGCSRoute(Route route)
        {
            try
            {
                // UGCS AreaScan: An area scan is represented by one segment with a series of figure points
                var coordinates = route.Segments[0].Figure.Points.ConvertAll(
                    point => new Coordinate(point.Longitude, point.Latitude));
                LinearRing ring = new LinearRing(coordinates.ToArray());
                Dictionary<string, string> parameters = new Dictionary<string, string>();

                foreach (ParameterValue param in route.Segments[0].ParameterValues)
                    parameters.Add(param.Name, param.Value);

                return new AreaScanPolygon(route.Id, route.Name, route.LastModificationTime, ring, parameters);
            } 
            catch (Exception e)
            {
                throw new Exception($"Could not convert UGCS route to AreaScanPolygon ({e.Message})");
            }
        }

        public static bool IsUGCSRouteAreaScanPolygon(Route route)
        {
            if (route.Segments == null || route.Segments.Count == 0)
                return false;

            var firstSegment = route.Segments[0];

            if (firstSegment.Figure.Type != FigureType.FT_POLYGON || 
                firstSegment.Figure.Points.Count < 3 ||
                firstSegment.Figure.Points.First().Longitude != firstSegment.Figure.Points.Last().Longitude ||
                firstSegment.Figure.Points.First().Latitude != firstSegment.Figure.Points.Last().Latitude)
                return false;

            return true;
        }

        public bool IntersectsCoordinate(Coordinate coord)
        {
            var point = GeometryFactory.Default.CreatePoint(coord);
            return Intersects(point);
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

        public IEnumerable<Tuple<double, double>> GetBasicCoordinates()
        {
            foreach (Coordinate coord in Coordinates)
                yield return new Tuple<double, double>(coord.X, coord.Y);
        }

        public bool Equals(AreaScanPolygon obj)
        {
            if (Id != obj.Id)
                return false;

            if (Coordinates.Length != obj.Coordinates.Length)
                return false;

            if (!Coordinates.SequenceEqual(obj.Coordinates))
                return false;

            if (Parameters.Count != obj.Parameters.Count || Parameters.Except(obj.Parameters).Any())
                return false;

            return true;
        }
    }
}
