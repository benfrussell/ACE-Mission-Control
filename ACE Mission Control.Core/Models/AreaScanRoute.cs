using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class AreaScanRoute
    {
        public string Name;
        public List<double[]> Area;
        public List<int> Vertices;
        public int EntryVertex;

        public AreaScanRoute(string name, List<double[]> area)
        {
            Name = name;
            Area = area;
            Vertices = Enumerable.Range(1, area.Count).ToList();
            EntryVertex = 0;
        }

        public AreaScanRoute()
        {
            Name = "";
            Area = new List<double[]>();
            Vertices = new List<int>();
            EntryVertex = 0;
        }

        public string GetVerticesString()
        {
            string vertString = "";
            foreach (double[] vert in Area)
            {
                if (vertString.Length != 0)
                    vertString = vertString + ",";
                vertString = vertString + string.Format("{0},{1}", vert[0], vert[1]);
            }
            return vertString;
        }

        public string GetEntryVetexString()
        {
            string entryString = string.Format("{0},{1}", Area[EntryVertex][0], Area[EntryVertex][1]);
            return entryString;
        }
    }
}
