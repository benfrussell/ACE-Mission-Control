using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE_Mission_Control.Core.Models
{
    public class InterceptCollectionChangedArgs
    {
        public List<int> AreaIDsAffected { get; set; }
    }

    // Precalculated list of all possible AreaScan / WaypointRoute intercepts indexed by the corresponding AreaScanPolygon ID
    public class WaypointRouteInterceptCollection
    {
        public event EventHandler<InterceptCollectionChangedArgs> AreaInterceptsModified;

        private Dictionary<AreaScanPolygon, List<WaypointRouteIntercept>> collection;

        public WaypointRouteInterceptCollection()
        {
            collection = new Dictionary<AreaScanPolygon, List<WaypointRouteIntercept>>();
        }

        public List<WaypointRouteIntercept> GetIntercepts(int areaScanID)
        {
            var areaScan = collection.Keys.FirstOrDefault(a => a.Id == areaScanID);
            if (areaScan == null)
                return new List<WaypointRouteIntercept>();
            return collection[areaScan];
        }

        public void RemoveAreaScansByIDs(List<int> ids)
        {
            foreach (int id in ids)
            {
                var area = collection.Keys.FirstOrDefault(a => a.Id == id);
                if (area != null)
                    collection.Remove(area);
            }

            var eventArgs = new InterceptCollectionChangedArgs() { AreaIDsAffected = ids };
            AreaInterceptsModified?.Invoke(this, eventArgs);
        }

        public void RemoveWaypointRoutesByIDs(List<int> routeIDs)
        {
            var modifiedAreaIDs = new List<int>();

            foreach (AreaScanPolygon area in collection.Keys)
            {
                var interceptToRemove = collection[area].FirstOrDefault(i => routeIDs.Contains(i.WaypointRoute.Id));

                if (interceptToRemove != null)
                {
                    collection[area].Remove(interceptToRemove);
                    modifiedAreaIDs.Add(area.Id);
                }
            }

            if (modifiedAreaIDs.Count > 0)
            {
                var eventArgs = new InterceptCollectionChangedArgs() { AreaIDsAffected = modifiedAreaIDs };
                AreaInterceptsModified?.Invoke(this, eventArgs);
            }
        }

        public void AddAreaScanWithWaypointRoutes(AreaScanPolygon area, List<WaypointRoute> routesToCheck)
        {
            var routeIntercepts = CalculateWaypointRouteIntercepts(area, routesToCheck);

            if (!collection.ContainsKey(area))
                collection[area] = new List<WaypointRouteIntercept>();
            
            collection[area].AddRange(routeIntercepts);

            var eventArgs = new InterceptCollectionChangedArgs() { AreaIDsAffected = new List<int> { area.Id } };
            AreaInterceptsModified?.Invoke(this, eventArgs);
        }

        public void AddWaypointRoutes(List<WaypointRoute> routesToAdd)
        {
            var modifiedAreaIDs = new List<int>();

            foreach (AreaScanPolygon area in collection.Keys)
            {
                var routeIntercepts = CalculateWaypointRouteIntercepts(area, routesToAdd);
                collection[area].AddRange(routeIntercepts);

                if (routeIntercepts.Count > 0)
                    modifiedAreaIDs.Add(area.Id);
            }

            if (modifiedAreaIDs.Count > 0)
            {
                var eventArgs = new InterceptCollectionChangedArgs() { AreaIDsAffected = modifiedAreaIDs };
                AreaInterceptsModified?.Invoke(this, eventArgs);
            }
        }

        public void ModifyExistingWaypointRouteIntercepts(List<WaypointRoute> modifiedRoutes)
        {
            var modifiedAreaIDs = new List<int>();

            foreach (AreaScanPolygon area in collection.Keys)
            {
                var matchingIDs = modifiedRoutes.Where(r => collection[area].Exists(i => i.WaypointRoute.Id == r.Id)).Select(r => r.Id);
                collection[area].RemoveAll(i => matchingIDs.Contains(i.WaypointRoute.Id));

                var routeIntercepts = CalculateWaypointRouteIntercepts(area, modifiedRoutes);
                collection[area].AddRange(routeIntercepts);

                if (matchingIDs.Count() > 0 || routeIntercepts.Count > 0)
                    modifiedAreaIDs.Add(area.Id);
            }

            if (modifiedAreaIDs.Count > 0)
            {
                var eventArgs = new InterceptCollectionChangedArgs() { AreaIDsAffected = modifiedAreaIDs };
                AreaInterceptsModified?.Invoke(this, eventArgs);
            }
        }

        public void ModifyExistingAreaScans(List<AreaScanPolygon> modifiedAreas, List<WaypointRoute> routesToCheck)
        {
            var modifiedAreaIDs = new List<int>();

            foreach (AreaScanPolygon area in modifiedAreas)
            {
                var matchingArea = collection.Keys.FirstOrDefault(a => a.Id == area.Id);
                if (matchingArea == null)
                    continue;

                collection.Remove(matchingArea);
                collection.Add(area, new List<WaypointRouteIntercept>());
                modifiedAreaIDs.Add(area.Id);

                var routeIntercepts = CalculateWaypointRouteIntercepts(area, routesToCheck);
                collection[area].AddRange(routeIntercepts);
            }

            if (modifiedAreaIDs.Count > 0)
            {
                var eventArgs = new InterceptCollectionChangedArgs() { AreaIDsAffected = modifiedAreaIDs };
                AreaInterceptsModified?.Invoke(this, eventArgs);
            }
        }

        private List<WaypointRouteIntercept> CalculateWaypointRouteIntercepts(AreaScanPolygon area, List<WaypointRoute> routesToCheck)
        {
            var validTreatmentRoutes = (from route in routesToCheck where route.Intersects(area) select route).ToList();
            // TODO: For some reason it can't get an entry and exit point on the same LineSegment
            List<WaypointRouteIntercept> routesIntercepts = validTreatmentRoutes.ConvertAll(
                r =>
                {
                    return new WaypointRouteIntercept()
                    {
                        WaypointRoute = r,
                        EntryCoordinate = r.CalcIntersectWithArea(area),
                        ExitCoordinate = r.CalcIntersectWithArea(area, reverse: true)
                    };
                });

            return routesIntercepts;
        }

        public void Clear()
        {
            var eventArgs = new InterceptCollectionChangedArgs() { AreaIDsAffected = collection.Keys.Select(a => a.Id).ToList() };
            collection.Clear();
            AreaInterceptsModified?.Invoke(this, eventArgs);
        }
    }
}
