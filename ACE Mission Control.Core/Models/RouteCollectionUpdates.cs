using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class RouteCollectionUpdates<T>
    {
        public bool AnyChanges { get => RemovedRouteIDs.Count + AddedRoutes.Count + ModifiedRoutes.Count > 0; }
        public List<int> RemovedRouteIDs { get; set; }
        public List<T> AddedRoutes { get; set; }
        public List<T> ModifiedRoutes { get; set; }

        public RouteCollectionUpdates()
        {
            RemovedRouteIDs = new List<int>();
            AddedRoutes = new List<T>();
            ModifiedRoutes = new List<T>();
        }
    }
}
