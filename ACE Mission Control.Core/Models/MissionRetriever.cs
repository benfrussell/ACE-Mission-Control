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
        public RouteCollectionUpdates<AreaScanPolygon> Updates { get; set; }
    }

    public class WaypointRoutesUpdatedArgs
    {
        public RouteCollectionUpdates<WaypointRoute> Updates { get; set; }
        public bool AreaScanPolygonsUpdateFollowing;
    }

    public class MissionRetriever : INotifyPropertyChanged
    {
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        public static event EventHandler<AreaScanPolygonsUpdatedArgs> AreaScanPolygonsUpdated;

        public static event EventHandler<WaypointRoutesUpdatedArgs> WaypointRoutesUpdated;

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
                AreaScanPolygonsUpdated?.Invoke(null, new AreaScanPolygonsUpdatedArgs() { Updates = removedAreas });
                AreaScanPolygons.Clear();
            }
            
            WaypointRoutes.Clear();
            TreatmentInstruction.InterceptCollection.Clear();
        }

        private static void RequestUGCSRoutes(Object source = null, ElapsedEventArgs args = null)
        {
            if (UGCSClient.IsConnected)
            {
                if (SelectedMission == null)
                {
                    if (!UGCSClient.RequestingMissions)
                        UGCSClient.RequestMissions();
                }
                else
                {
                    if (!UGCSClient.RequestingRoutes)
                        UGCSClient.RequestRoutes(SelectedMission.Id);
                }
            } 
            else if (IsUGCSPollerRunning)
            {
                ugcsPoller.Start();
            }
        }

        private static void UGCSClient_ReceivedMissionsEvent(object sender, ReceivedMissionsEventArgs e)
        {
            var allRoutes = from mission in e.Missions from route in mission.Routes select route;
            var mostRecentRoute = allRoutes.Aggregate((r1, r2) => r1.LastModificationTime > r2.LastModificationTime ? r1 : r2);
            var mostRecentMission = e.Missions.FirstOrDefault(m => m.Id == mostRecentRoute.Mission.Id);

            if (SelectedMission == null || SelectedMission.Id != mostRecentMission.Id)
            {
                SelectedMission = e.Missions.FirstOrDefault(m => m.Id == mostRecentRoute.Mission.Id);
                DroneController.AlertAllDrones(new AlertEntry(
                    AlertEntry.AlertLevel.Info,
                    AlertEntry.AlertType.UGCSStatus,
                    $"Selecting mission '{SelectedMission.Name}' with ID {SelectedMission.Id}"));

                if (!UGCSClient.RequestingRoutes)
                    UGCSClient.RequestRoutes(SelectedMission.Id);
            }
            else
            {
                DroneController.AlertAllDrones(new AlertEntry(
                    AlertEntry.AlertLevel.Info,
                    AlertEntry.AlertType.UGCSStatus,
                    $"Mission '{SelectedMission.Name}' with ID {SelectedMission.Id} is still the most recent"));
            }
            
            AvailableMissions = e.Missions;
        }

        private static void UGCSClient_ReceivedRoutesEvent(object sender, ReceivedRoutesEventArgs e)
        {
            List<AreaScanPolygon> newAreaRoutes = new List<AreaScanPolygon>();
            List<WaypointRoute> newWaypointRoutes = new List<WaypointRoute>();

            foreach (Route r in e.Routes)
            {
                if (AreaScanPolygon.IsUGCSRouteAreaScanPolygon(r))
                    newAreaRoutes.Add(AreaScanPolygon.CreateFromUGCSRoute(r));
                else if (WaypointRoute.IsUGCSRouteWaypointRoute(r))
                    newWaypointRoutes.Add(WaypointRoute.CreateFromUGCSRoute(r));
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
                // Remove both removed waypoint routes AND modified waypoint routes. Modified routes will be re-added.
                var routeIDsToRemove = new List<int>(waypointRouteChanges.RemovedRouteIDs);
                routeIDsToRemove.AddRange(from r in waypointRouteChanges.ModifiedRoutes select r.Id);

                WaypointRoutes.RemoveAll(a => routeIDsToRemove.Contains(a.Id));
                TreatmentInstruction.InterceptCollection.RemoveWaypointRoutesByIDs(routeIDsToRemove);

                var routesToAdd = new List<WaypointRoute>(waypointRouteChanges.AddedRoutes);
                routesToAdd.AddRange(waypointRouteChanges.ModifiedRoutes);
                WaypointRoutes.AddRange(routesToAdd);

                // Unmodified areas only need to check against new routes
                foreach (AreaScanPolygon area in AreaScanPolygons)
                    TreatmentInstruction.InterceptCollection.AddTreatmentRouteIntercepts(area, routesToAdd);

                NotifyStaticPropertyChanged("WaypointRoutes");

                WaypointRoutesUpdated?.Invoke(null, new WaypointRoutesUpdatedArgs { Updates = waypointRouteChanges, AreaScanPolygonsUpdateFollowing = areaChanges.AnyChanges });

                DroneController.AlertAllDrones(new AlertEntry(
                    AlertEntry.AlertLevel.Info,
                    AlertEntry.AlertType.UGCSStatus,
                    $"Updated routes. ({waypointRouteChanges.RemovedRouteIDs.Count}) routes removed, ({waypointRouteChanges.ModifiedRoutes.Count}) routes modified, ({waypointRouteChanges.AddedRoutes.Count}) routes added."));
            }

            if (areaChanges.AnyChanges)
            {
                AreaScanPolygons.AddRange(areaChanges.ModifiedRoutes);
                AreaScanPolygons.AddRange(areaChanges.AddedRoutes);

                // New and modified areas need to check against every waypoint route
                foreach (AreaScanPolygon area in areaChanges.ModifiedRoutes)
                    TreatmentInstruction.InterceptCollection.AddTreatmentRouteIntercepts(area, WaypointRoutes);
                foreach (AreaScanPolygon area in areaChanges.AddedRoutes)
                    TreatmentInstruction.InterceptCollection.AddTreatmentRouteIntercepts(area, WaypointRoutes);

                NotifyStaticPropertyChanged("AreaScanPolygons");

                AreaScanPolygonsUpdated?.Invoke(null, new AreaScanPolygonsUpdatedArgs() { Updates = areaChanges });

                DroneController.AlertAllDrones(new AlertEntry(
                    AlertEntry.AlertLevel.Info,
                    AlertEntry.AlertType.UGCSStatus,
                    $"Updated areas. ({areaChanges.RemovedRouteIDs.Count}) areas removed, ({areaChanges.ModifiedRoutes.Count}) areas modified, ({areaChanges.AddedRoutes.Count}) areas added."));
            }

            if (IsUGCSPollerRunning)
                ugcsPoller.Start();
        }

        // TODO: New AreaScan name change and route to waypoints didn't sync!

        // Finds and returns all of the updates to match an exisiting route list to a new route list
        private static RouteCollectionUpdates<T> DetermineExisitngRouteUpdates<T>(IEnumerable<T> existingRouteList, IEnumerable<T> newRouteList) where T : IComparableRoute<T>
        {
            var changes = new RouteCollectionUpdates<T>();
            // Start with a list of all IDs and remove them as we find matches in the new list
            changes.RemovedRouteIDs = (from existingRoute in existingRouteList select existingRoute.Id).ToList();

            foreach (T newRoute in newRouteList)
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
                    if (!newRoute.Equals(matchInExistingRoutes))
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
