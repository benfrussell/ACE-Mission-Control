using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class IDCoordinate
    {
        private string id;
        public string ID { get => id; private set => id = value; }

        private Coordinate coordinate;
        public Coordinate Coordinate { get => coordinate; private set => coordinate = value; }

        public IDCoordinate(string _id, Coordinate _coordinate)
        {
            ID = _id;
            Coordinate = _coordinate;
        }
    }
}
