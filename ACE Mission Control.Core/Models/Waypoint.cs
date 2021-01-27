using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class Waypoint
    {
        private string id;
        public string ID { get => id; private set => id = value; }

        private Coordinate coordinate;
        public Coordinate Coordinate { get => coordinate; private set => coordinate = value; }

        private string turnType;
        public string TurnType { get => turnType; private set => turnType = value; }

        public Waypoint(string id, string turnType, Coordinate _coordinate)
        {
            ID = id;
            Coordinate = _coordinate;
            TurnType = turnType;
        }

        public Waypoint(string _id, string turnType, double longitude, double latitude)
        {
            ID = id;
            Coordinate = new Coordinate(longitude, latitude);
            TurnType = turnType;
        }
    }
}
