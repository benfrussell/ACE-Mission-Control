using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE_Mission_Control.Core.Models
{
    // Precalculated list of all possible AreaScan / WaypointRoute intercepts indexed by the corresponding AreaScanPolygon ID
    public class WaypointRouteInterceptCollection : Dictionary<int, List<WaypointRouteIntercept>>
    {
        public void RemoveAreaScansByIDs(List<int> ids)
        {
            foreach (int id in ids)
            {
                Remove(id);
            }
        }

        public void RemoveWaypointRoutesByIDs(List<int> ids)
        {
            foreach (List<WaypointRouteIntercept> intercepts in Values)
            {
                var interceptToRemove = intercepts.First(i => ids.Contains(i.WaypointRoute.Id));
                if (interceptToRemove != null)
                    intercepts.Remove(interceptToRemove);
            }
        }

        public void AddTreatmentRouteIntercepts(AreaScanPolygon polygon, List<WaypointRoute> routesToCheck)
        {
            var validTreatmentRoutes = (from route in routesToCheck where route.Intersects(polygon) select route).ToList();
            // TODO: For some reason it can't get an entry and exit point on the same LineSegment
            List<WaypointRouteIntercept> routesIntercepts = validTreatmentRoutes.ConvertAll(
                r =>
                {
                    return new WaypointRouteIntercept()
                    {
                        WaypointRoute = r,
                        EntryCoordinate = r.CalcIntersectWithArea(polygon),
                        ExitCoordinate = r.CalcIntersectWithArea(polygon, reverse: true)
                    };
                });

            if (!ContainsKey(polygon.Id))
                this[polygon.Id] = new List<WaypointRouteIntercept>();
            this[polygon.Id].AddRange(routesIntercepts);
        }
    }
}
