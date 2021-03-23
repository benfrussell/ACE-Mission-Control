using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Timers;
using UGCS.Sdk.Protocol.Encoding;

namespace ACE_Mission_Control.Core.Models
{
    public class AreaScanPolygonsUpdatedArgs
    {
        public RouteCollectionUpdates<AreaScanPolygon> updates { get; set; }
    }

    public class MissionRetriever : INotifyPropertyChanged
    {
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        public static event EventHandler<AreaScanPolygonsUpdatedArgs> AreaScanPolygonsUpdated;

        private static List<UGCS.Sdk.Protocol.Encoding.Mission> availableMissions;
        public static List<UGCS.Sdk.Protocol.Encoding.Mission> AvailableMissions
        {
            get => availableMissions;
            private set
            {
                if (availableMissions == value)
                    return;
                availableMissions = value;
                NotifyStaticPropertyChanged("AvailableMissions");
            }
        }

        private static UGCS.Sdk.Protocol.Encoding.Mission selectedMission;
        public static UGCS.Sdk.Protocol.Encoding.Mission SelectedMission
        {
            get => selectedMission;
            set
            {
                if (selectedMission == value)
                    return;
                selectedMission = value;
                RemoveAllMissionData();
                NotifyStaticPropertyChanged("SelectedMission");
            }
        }

        private static List<WaypointRoute> waypointRoutes;
        public static List<WaypointRoute> WaypointRoutes
        {
            get => waypointRoutes;
            private set
            {
                if (waypointRoutes == value)
                    return;
                waypointRoutes = value;
                NotifyStaticPropertyChanged("WaypointRoutes");
            }
        }

        private static List<AreaScanPolygon> areaScanPolygons;
        public static List<AreaScanPolygon> AreaScanPolygons
        {
            get => areaScanPolygons;
            private set
            {
                if (areaScanPolygons == value)
                    return;
                areaScanPolygons = value;
                NotifyStaticPropertyChanged("AreaScanPolygons");
            }
        }
        private static bool isUGCSPollerRunning;
        public static bool IsUGCSPollerRunning
        {
            get { return isUGCSPollerRunning; }
            private set
            {
                if (value == isUGCSPollerRunning)
                    return;
                isUGCSPollerRunning = value;
                NotifyStaticPropertyChanged();
            }
        }

        private static Timer ugcsPoller;

        public event PropertyChangedEventHandler PropertyChanged;

        static MissionRetriever()
        {
            WaypointRoutes = new List<WaypointRoute>();
            AreaScanPolygons = new List<AreaScanPolygon>();
            AvailableMissions = new List<UGCS.Sdk.Protocol.Encoding.Mission>();
            UGCSClient.ReceivedMissionsEvent += UGCSClient_ReceivedMissionsEvent;
            UGCSClient.ReceivedRoutesEvent += UGCSClient_ReceivedRoutesEvent;
            UGCSClient.StaticPropertyChanged += UGCSClient_StaticPropertyChanged;
        }

        public static void StartUGCSPoller()
        {
            // Prepare the poller but start one request attempt right away if we're connected
            ugcsPoller = new Timer(3000);
            ugcsPoller.Elapsed += RequestUGCSRoutes;
            ugcsPoller.AutoReset = false;
            IsUGCSPollerRunning = true;

            if (UGCSClient.IsConnected)
            {
                RequestUGCSRoutes();
            }
            else
            {
                if (UGCSClient.TryingConnections)
                    ugcsPoller.Start();
                else
                    UGCSClient.StartTryingConnections();
            }
        }

        private static void UGCSClient_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // If the UGCS client lost connection while the poller was paused (probably for a request) restart the poller
            if (e.PropertyName == "IsConnected" && !UGCSClient.IsConnected && IsUGCSPollerRunning && !ugcsPoller.Enabled)
                ugcsPoller.Start();
        }

        private static void RemoveAllMissionData()
        {
            if (AreaScanPolygons.Count > 0)
            {
                var allAreaIDs = from area in AreaScanPolygons select area.SequentialID;
                RouteCollectionUpdates<AreaScanPolygon> removedAreas = new RouteCollectionUpdates<AreaScanPolygon>() { RemovedRouteIDs = allAreaIDs.ToList() };
                AreaScanPolygonsUpdated?.Invoke(null, new AreaScanPolygonsUpdatedArgs() { updates = removedAreas });
                AreaScanPolygons.Clear();
            }
            
            WaypointRoutes.Clear();
            TreatmentInstruction.InterceptCollection.Clear();
        }

        private static void RequestUGCSRoutes(Object source = null, ElapsedEventArgs args = null)
        {
            if (UGCSClient.IsConnected)
            {
                UGCSClient.RequestMissions();
                if (SelectedMission != null)
                    UGCSClient.RequestRoutes(SelectedMission.Id);
            } 
            else if (IsUGCSPollerRunning)
            {
                ugcsPoller.Start();
            }
        }

        private static void UGCSClient_ReceivedMissionsEvent(object sender, ReceivedMissionsEventArgs e)
        {
            if (AvailableMissions.Count == 0 && e.Missions.Count > 0)
            {
                var allRoutes = from mission in e.Missions from route in mission.Routes select route;
                var mostRecentRoute = allRoutes.Aggregate((r1, r2) => r1.LastModificationTime > r2.LastModificationTime ? r1 : r2);
                SelectedMission = mostRecentRoute.Mission;
                UGCSClient.RequestRoutes(SelectedMission.Id);
            }

            AvailableMissions = e.Missions;
        }

        private static void UGCSClient_ReceivedRoutesEvent(object sender, ReceivedRoutesEventArgs e)
        {
            List<Route> newAreaRoutes = new List<Route>();
            List<Route> newWaypointRoutes = new List<Route>();

            foreach (Route r in e.Routes)
            {
                if (AreaScanPolygon.IsUGCSRouteAreaScanPolygon(r))
                    newAreaRoutes.Add(r);
                else if (WaypointRoute.IsUGCSRouteWaypointRoute(r))
                    newWaypointRoutes.Add(r);
            }

            var areaChanges = DetermineExisitngRouteUpdates(AreaScanPolygons, newAreaRoutes);
            var waypointRouteChanges = DetermineExisitngRouteUpdates(WaypointRoutes, newWaypointRoutes);

            if (areaChanges.AnyChanges)
            {
                var areaIDsToRemove = new List<int>(areaChanges.RemovedRouteIDs);
                areaIDsToRemove.AddRange(from r in areaChanges.ModifiedRoutes select r.Id);
                AreaScanPolygons.RemoveAll(a => areaIDsToRemove.Contains(a.Id));
                TreatmentInstruction.InterceptCollection.RemoveAreaScansByIDs(areaIDsToRemove);
            }

            if (waypointRouteChanges.AnyChanges)
            {
                var routeIDsToRemove = new List<int>(waypointRouteChanges.RemovedRouteIDs);
                routeIDsToRemove.AddRange(from r in waypointRouteChanges.ModifiedRoutes select r.Id);
                WaypointRoutes.RemoveAll(a => routeIDsToRemove.Contains(a.Id));
                TreatmentInstruction.InterceptCollection.RemoveWaypointRoutesByIDs(routeIDsToRemove);

                var routesToAdd = RoutesToWaypointRoutes(waypointRouteChanges.AddedRoutes).ToList();
                routesToAdd.AddRange(RoutesToWaypointRoutes(waypointRouteChanges.ModifiedRoutes));
                WaypointRoutes.AddRange(routesToAdd);

                // Unmodified areas only need to check against new routes
                foreach (AreaScanPolygon area in AreaScanPolygons)
                    TreatmentInstruction.InterceptCollection.AddTreatmentRouteIntercepts(area, routesToAdd);

                NotifyStaticPropertyChanged("WaypointRoutes");
            }

            if (areaChanges.AnyChanges)
            {
                var areasUpdate = new RouteCollectionUpdates<AreaScanPolygon>()
                {
                    RemovedRouteIDs = areaChanges.RemovedRouteIDs,
                    ModifiedRoutes = RoutesToAreaScans(areaChanges.ModifiedRoutes).ToList(),
                    AddedRoutes = RoutesToAreaScans(areaChanges.AddedRoutes).ToList()
                };

                AreaScanPolygons.AddRange(areasUpdate.ModifiedRoutes);
                AreaScanPolygons.AddRange(areasUpdate.AddedRoutes);

                // New and modified areas need to check against every waypoint route
                foreach (AreaScanPolygon area in areasUpdate.ModifiedRoutes)
                    TreatmentInstruction.InterceptCollection.AddTreatmentRouteIntercepts(area, WaypointRoutes);
                foreach (AreaScanPolygon area in areasUpdate.AddedRoutes)
                    TreatmentInstruction.InterceptCollection.AddTreatmentRouteIntercepts(area, WaypointRoutes);

                NotifyStaticPropertyChanged("AreaScanPolygons");

                AreaScanPolygonsUpdated(null, new AreaScanPolygonsUpdatedArgs() { updates = areasUpdate });
            }

            if (IsUGCSPollerRunning)
                ugcsPoller.Start();
        }

        // TODO: New AreaScan name change and route to waypoints didn't sync!

        // Finds and returns all of the updates to match an exisiting route list to a new route list
        private static RouteCollectionUpdates<Route> DetermineExisitngRouteUpdates<T>(IEnumerable<T> existingRouteList, List<Route> newRouteList) where T : IComparableRoute
        {
            var changes = new RouteCollectionUpdates<Route>();
            // Start with a list of all IDs and remove them as we find matches in the new list
            changes.RemovedRouteIDs = (from existingRoute in existingRouteList select existingRoute.Id).ToList();

            foreach (Route newRoute in newRouteList)
            {
                var matchInExistingRoutes = existingRouteList.FirstOrDefault(existingRoute => existingRoute.Id == newRoute.Id);
                if (matchInExistingRoutes == null)
                {
                    changes.AddedRoutes.Add(newRoute);
                }
                else
                {
                    changes.RemovedRouteIDs.Remove(matchInExistingRoutes.Id);
                    // For some reason UGCS reports ALL routes as being modified whenever you add/remove/modify a single route
                    if (newRoute.LastModificationTime > matchInExistingRoutes.LastModificationTime)
                        changes.ModifiedRoutes.Add(newRoute);
                }
            }

            return changes;
        }

        private static IEnumerable<AreaScanPolygon> RoutesToAreaScans(IEnumerable<Route> routes)
        {
            foreach (Route r in routes)
                yield return AreaScanPolygon.CreateFromUGCSRoute(r);
        }

        private static IEnumerable<WaypointRoute> RoutesToWaypointRoutes(IEnumerable<Route> routes)
        {
            foreach (Route r in routes)
                yield return WaypointRoute.CreateFromUGCSRoute(r);
        }

        private static void NotifyStaticPropertyChanged([CallerMemberName] string propertyName = "")
        {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
        }
    }
}
