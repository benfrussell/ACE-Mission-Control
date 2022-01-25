using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE_Mission_Control.Core.Models
{
    public class InterceptCollectionChangedArgs
    {
        public List<int> AreaIDsAffected { get; set; }
        public List<WaypointRouteIntercept> InterceptsAffected { get; set; }

        public static InterceptCollectionChangedArgs JoinChanges(List<InterceptCollectionChangedArgs> changesList)
        {
            var joinedAreaIDs = from changes in changesList
                                from areaIDs in changes.AreaIDsAffected 
                                select areaIDs;
            joinedAreaIDs = joinedAreaIDs.Distinct();

            var joinedIntercepts = from changes in changesList
                                   from intercepts in changes.InterceptsAffected
                                   select intercepts;
            joinedIntercepts = joinedIntercepts.Distinct();

            return new InterceptCollectionChangedArgs() { AreaIDsAffected = joinedAreaIDs.ToList(), InterceptsAffected = joinedIntercepts.ToList() };
        }
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
            List<WaypointRouteIntercept> affectedIntercepts = new List<WaypointRouteIntercept>();

            foreach (int id in ids)
            {
                var area = collection.Keys.FirstOrDefault(a => a.Id == id);
                if (area != null)
                {
                    affectedIntercepts.AddRange(collection[area]);
                    collection.Remove(area);
                }
            }

            var eventArgs = new InterceptCollectionChangedArgs() { AreaIDsAffected = ids, InterceptsAffected = affectedIntercepts };
            AreaInterceptsModified?.Invoke(this, eventArgs);
        }

        public void RemoveWaypointRoutesByIDs(List<int> routeIDs)
        {
            var modifiedAreaIDs = new List<int>();
            List<WaypointRouteIntercept> affectedIntercepts = new List<WaypointRouteIntercept>();

            foreach (AreaScanPolygon area in collection.Keys)
            {
                var interceptsToRemove = collection[area].Where(i => routeIDs.Contains(i.WaypointRoute.Id));

                if (interceptsToRemove.Count() > 0)
                    modifiedAreaIDs.Add(area.Id);

                foreach (WaypointRouteIntercept interceptToRemove in interceptsToRemove)
                {
                    affectedIntercepts.Add(interceptToRemove);
                    collection[area].Remove(interceptToRemove);
                }
            }

            if (modifiedAreaIDs.Count > 0)
            {
                var eventArgs = new InterceptCollectionChangedArgs() { AreaIDsAffected = modifiedAreaIDs, InterceptsAffected = affectedIntercepts };
                AreaInterceptsModified?.Invoke(this, eventArgs);
            }
        }

        public void AddAreaScanWithWaypointRoutes(AreaScanPolygon area, List<WaypointRoute> routesToCheck)
        {
            var routeIntercepts = DetermineIntersectingRoutes(area, routesToCheck);

            if (!collection.ContainsKey(area))
                collection[area] = new List<WaypointRouteIntercept>();
            
            collection[area].AddRange(routeIntercepts);

            var eventArgs = new InterceptCollectionChangedArgs() { AreaIDsAffected = new List<int> { area.Id }, InterceptsAffected = collection[area] };
            AreaInterceptsModified?.Invoke(this, eventArgs);
        }

        public void AddWaypointRoutes(List<WaypointRoute> routesToAdd)
        {
            var modifiedAreaIDs = new List<int>();
            List<WaypointRouteIntercept> affectedIntercepts = new List<WaypointRouteIntercept>();

            foreach (AreaScanPolygon area in collection.Keys)
            {
                var routeIntercepts = DetermineIntersectingRoutes(area, routesToAdd);
                collection[area].AddRange(routeIntercepts);
                affectedIntercepts.AddRange(routeIntercepts);

                if (routeIntercepts.Count > 0)
                    modifiedAreaIDs.Add(area.Id);
            }

            if (modifiedAreaIDs.Count > 0)
            {
                var eventArgs = new InterceptCollectionChangedArgs() { AreaIDsAffected = modifiedAreaIDs, InterceptsAffected = affectedIntercepts };
                AreaInterceptsModified?.Invoke(this, eventArgs);
            }
        }

        public void ModifyExistingWaypointRouteIntercepts(List<WaypointRoute> modifiedRoutes)
        {
            List<InterceptCollectionChangedArgs> changesList = new List<InterceptCollectionChangedArgs>();

            foreach (AreaScanPolygon area in collection.Keys)
            {
                changesList.Add(ApplyChangesToAreasIntersects(area, modifiedRoutes));
            }

            var joinedChanges = InterceptCollectionChangedArgs.JoinChanges(changesList);
            if (joinedChanges.AreaIDsAffected.Count > 0)
                AreaInterceptsModified?.Invoke(this, joinedChanges);
        }

        public void ModifyExistingAreaScans(List<AreaScanPolygon> modifiedAreas, List<WaypointRoute> routesToCheck)
        {
            List<InterceptCollectionChangedArgs> changesList = new List<InterceptCollectionChangedArgs>();

            foreach (AreaScanPolygon modifiedArea in modifiedAreas)
            {
                var matchingArea = collection.Keys.FirstOrDefault(a => a.Id == modifiedArea.Id);

                // This should always find a match, because this method is meant to modify EXISTING area scans
                if (matchingArea == null)
                    continue;

                changesList.Add(ApplyChangesToAreasIntersects(modifiedArea, routesToCheck));
            }

            var joinedChanges = InterceptCollectionChangedArgs.JoinChanges(changesList);
            if (joinedChanges.AreaIDsAffected.Count > 0)
                AreaInterceptsModified?.Invoke(this, joinedChanges);
        }

        private InterceptCollectionChangedArgs ApplyChangesToAreasIntersects(AreaScanPolygon area, List<WaypointRoute> routesToCheck)
        {
            var modifiedAreaIDs = new List<int>();
            var modifiedIntercepts = new List<WaypointRouteIntercept>();

            // For each route that was modified, determine if this area has intersects that need to be updated or removed, OR if a new intersect needs to be added
            foreach (WaypointRoute modifiedRoute in routesToCheck)
            {
                var matchingIntercept = collection[area].FirstOrDefault(i => i.WaypointRoute.Id == modifiedRoute.Id);

                if (matchingIntercept != null)
                {
                    if (modifiedRoute.Intersects(area))
                    {
                        var modified = matchingIntercept.UpdateWaypointRoute(modifiedRoute);
                        if (modified)
                            modifiedIntercepts.Add(matchingIntercept);
                    }
                    else
                    {
                        collection[area].Remove(matchingIntercept);
                    }
                    modifiedAreaIDs.Add(area.Id);
                }
                else
                {
                    if (modifiedRoute.Intersects(area))
                    {
                        collection[area].Add(WaypointRouteIntercept.CreateFromIntersectingRoute(modifiedRoute, area));
                        modifiedAreaIDs.Add(area.Id);
                    }
                }
            }

            return new InterceptCollectionChangedArgs() { AreaIDsAffected = modifiedAreaIDs, InterceptsAffected = modifiedIntercepts };
        }

        private List<WaypointRouteIntercept> DetermineIntersectingRoutes(AreaScanPolygon area, List<WaypointRoute> routesToCheck)
        {
            var validTreatmentRoutes = (from route in routesToCheck where route.Intersects(area) select route).ToList();
            List<WaypointRouteIntercept> routesIntercepts = validTreatmentRoutes.ConvertAll(r => WaypointRouteIntercept.CreateFromIntersectingRoute(r, area));

            return routesIntercepts;
        }

        public void Clear()
        {
            var eventArgs = new InterceptCollectionChangedArgs() 
            { 
                AreaIDsAffected = collection.Keys.Select(a => a.Id).ToList(), 
                InterceptsAffected = collection.Values.SelectMany(v => v).ToList() 
            };

            collection.Clear();
            AreaInterceptsModified?.Invoke(this, eventArgs);
        }
    }
}
