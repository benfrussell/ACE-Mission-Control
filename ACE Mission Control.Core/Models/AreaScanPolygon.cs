using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Devices.Geolocation;
using NetTopologySuite.Geometries;
using UGCS.Sdk.Protocol.Encoding;

namespace ACE_Mission_Control.Core.Models
{
    public class AreaScanPolygon : Polygon
    {
        public int ID;
        public string Name;
        public Geopath Area;
        public int EntryVertex;

        public AreaScanPolygon(int id, string name, LinearRing polygonPoints) : base(polygonPoints)
        {
            ID = id;
            Name = name;
        }

        public static AreaScanPolygon CreateFromUGCSRoute(Route route)
        {
            try
            {
                // UGCS AreaScan: An area scan is represented by one segment with a series of figure points
                var coordinates = route.Segments[0].Figure.Points.ConvertAll(
                    point => new Coordinate(point.Longitude, point.Latitude));
                LinearRing ring = new LinearRing(coordinates.ToArray());
                return new AreaScanPolygon(route.Id, route.Name, ring);
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

            if (route.Segments[0].Figure.Type != FigureType.FT_POLYGON)
                return false;

            return true;
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

        public string GetVerticesString()
        {
            string vertString = "";
            foreach (BasicGeoposition position in Area.Positions)
            {
                if (vertString.Length != 0)
                    vertString = vertString + ";";
                vertString = vertString + string.Format("{0},{1}", (Math.PI / 180) * position.Latitude, (Math.PI / 180) * position.Longitude);
            }
            return vertString;
        }

        public string GetEntryVetexString()
        {
            string entryString = string.Format(
                "{0},{1}",
                Area.Positions[EntryVertex].Latitude,
                Area.Positions[EntryVertex].Longitude);
            return entryString;
        }
    }
}
