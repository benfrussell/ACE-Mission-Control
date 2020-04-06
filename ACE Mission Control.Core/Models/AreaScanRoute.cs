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
        public IEnumerable<int> Vertices;
        public int EntryVertex;

        public AreaScanRoute(string name, List<double[]> area)
        {
            Name = name;
            Area = area;
            Vertices = Enumerable.Range(0, area.Count - 1);
            EntryVertex = 0;
        }

        public AreaScanRoute()
        {
            Name = "";
            Area = new List<double[]>();
            Vertices = Enumerable.Empty<int>();
            EntryVertex = 0;
        }
    }
}
