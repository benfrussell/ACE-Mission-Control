using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class TreatmentInstruction
    {
        // Static TreatmentInstruction members

        // Precalculated list of all possible AreaScan / WaypointRoute intercepts
        private static Dictionary<int, List<WaypointRouteIntercept>> AreaScanWaypointRouteIntercepts = new Dictionary<int, List<WaypointRouteIntercept>>();

        public static void RemoveAreaScansByIDs(List<int> ids)
        {
            foreach (int id in ids)
            {
                AreaScanWaypointRouteIntercepts.Remove(id);
            }
        }

        public static void RemoveWaypointRoutesByIDs(List<int> ids)
        {
            foreach (List<WaypointRouteIntercept> intercepts in AreaScanWaypointRouteIntercepts.Values)
            {
                var interceptToRemove = intercepts.First(i => ids.Contains(i.WaypointRoute.Id));
                if (interceptToRemove != null)
                    intercepts.Remove(interceptToRemove);
            }
        }

        public static void AddTreatmentRouteIntercepts(AreaScanPolygon polygon, List<WaypointRoute> routesToCheck)
        {
            var validTreatmentRoutes = (from route in routesToCheck where route.Intersects(polygon) select route).ToList();
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

            if (!AreaScanWaypointRouteIntercepts.ContainsKey(polygon.Id))
                AreaScanWaypointRouteIntercepts[polygon.Id] = new List<WaypointRouteIntercept>();
            AreaScanWaypointRouteIntercepts[polygon.Id].AddRange(routesIntercepts);
        }

        // Dynamic TreatmentInstruction members

        private AreaScanPolygon treatmentPolygon;
        public AreaScanPolygon TreatmentPolygon 
        { 
            get => treatmentPolygon;
            private set
            {
                if (treatmentPolygon != null && 
                    treatmentPolygon.Id == value.Id && 
                    treatmentPolygon.LastModificationTime == value.LastModificationTime)
                    return;
                treatmentPolygon = value;
            } 
        }

        private WaypointRouteIntercept selectedInterceptRoute;
        public WaypointRoute TreatmentRoute 
        { 
            get => selectedInterceptRoute != null ? selectedInterceptRoute.WaypointRoute : null;
            set
            {
                if (selectedInterceptRoute != null && selectedInterceptRoute.WaypointRoute == value)
                    return;
                selectedInterceptRoute = AreaScanWaypointRouteIntercepts[TreatmentPolygon.Id].FirstOrDefault(r => r.WaypointRoute == value);
            }
        }
        public Coordinate PayloadUnlockCoordinate
        {
            get => selectedInterceptRoute.EntryCoordinate;
        }
        public Coordinate PayloadLockCoordinate
        {
            get => selectedInterceptRoute.ExitCoordinate;
        }

        public IEnumerable<WaypointRoute> ValidTreatmentRoutes
        {
            get => AreaScanWaypointRouteIntercepts[TreatmentPolygon.Id].Select(r => r.WaypointRoute);
        }

        private bool autoCalcUnlock = true;
        public bool AutoCalcUnlock 
        { 
            get => autoCalcUnlock;
            set
            {
                if (autoCalcUnlock == value)
                    return;
                autoCalcUnlock = value;
            }
        }

        private bool autoCalcLock = true;
        public bool AutoCalcLock 
        { 
            get => autoCalcLock; 
            set
            {
                if (autoCalcLock == value)
                    return;
                autoCalcLock = value;
            }
        }

        public string Name { get => TreatmentPolygon.Name; }

        private bool doTreatment;
        public bool DoTreatment 
        { 
            get => doTreatment;
            set
            {
                if (doTreatment == value)
                    return;
                doTreatment = value;
            }
        }

        public TreatmentInstruction(AreaScanPolygon treatmentArea)
        {
            TreatmentPolygon = treatmentArea;
            DoTreatment = true;
            RevalidateTreatmentRoute();
        }

        public void UpdateTreatmentArea(AreaScanPolygon treatmentArea)
        {
            TreatmentPolygon = treatmentArea;
            RevalidateTreatmentRoute();
        }

        public bool IsTreatmentRouteValid()
        {
            return AreaScanWaypointRouteIntercepts[TreatmentPolygon.Id].Contains(selectedInterceptRoute);
        }

        public bool RevalidateTreatmentRoute()
        {
            if (!IsTreatmentRouteValid())
            {
                selectedInterceptRoute = AreaScanWaypointRouteIntercepts[TreatmentPolygon.Id].FirstOrDefault();
                return true;
            }
            return false;
        }
    }
}
