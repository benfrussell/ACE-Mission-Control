using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Devices.Geolocation;

namespace ACE_Mission_Control.Core.Models
{
    public class AreaScanRoute
    {
        public string Name;
        public Geopath Area;
        public int EntryVertex;

        public AreaScanRoute(string name, Geopath area)
        {
            Name = name;
            Area = area;
            EntryVertex = 0;
        }

        public AreaScanRoute()
        {
            Name = "";
            Area = null;
            EntryVertex = 0;
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
                (Math.PI / 180) * Area.Positions[EntryVertex].Latitude,
                (Math.PI / 180) * Area.Positions[EntryVertex].Longitude);
            return entryString;
        }
    }
}
