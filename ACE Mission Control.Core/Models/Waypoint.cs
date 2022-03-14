using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class Waypoint
    {
        public enum TurnType : int
        {
            NotSpecified = 0,
            StopAndTurn = 1,
            FlyThrough = 2
        }

        public string ID { get; private set; }

        public Coordinate Coordinate { get; private set; }

        public TurnType Turn { get; private set; }

        public Waypoint(string id, string turnType, Coordinate _coordinate)
        {
            ID = id;
            Coordinate = _coordinate;
            Turn = turnType == "STOP_AND_TURN" ? TurnType.StopAndTurn : TurnType.FlyThrough;
        }

        public Waypoint(string id, string turnType, double longitude, double latitude)
        {
            ID = id;
            Coordinate = new Coordinate(longitude, latitude);
            Turn = turnType == "STOP_AND_TURN" ? TurnType.StopAndTurn : TurnType.FlyThrough;
        }

        public bool IntersectsArea(AreaScanPolygon area)
        {
            var point = GeometryFactory.Default.CreatePoint(Coordinate);
            return area.Intersects(point);
        }
    }
}
