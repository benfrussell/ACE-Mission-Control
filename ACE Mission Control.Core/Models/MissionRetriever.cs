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
            UGCSClient.ReceivedMissionEvent += UGCSClient_ReceivedMissionEvent;
            UGCSClient.StaticPropertyChanged += UGCSClient_StaticPropertyChanged;
        }

        public static void StartUGCSPoller()
        {
            // Prepare the poller but start one request attempt right away if we're connected
            ugcsPoller = new Timer(3000);
            ugcsPoller.Elapsed += RequestUGCSMission;
            ugcsPoller.AutoReset = false;
            IsUGCSPollerRunning = true;

            if (UGCSClient.IsConnected)
            {
                RequestUGCSMission();
            }
            else
            {
                if (!UGCSClient.TryingConnections)
                    UGCSClient.StartTryingConnections();
            }
            ugcsPoller.Start();
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
                var allAreaIDs = from area in AreaScanPolygons select area.Id;
                RouteCollectionUpdates<AreaScanPolygon> removedAreas = new RouteCollectionUpdates<AreaScanPolygon>() { RemovedRouteIDs = allAreaIDs.ToList() };
                AreaScanPolygonsUpdated?.Invoke(null, new AreaScanPolygonsUpdatedArgs() { Updates = removedAreas });
                AreaScanPolygons.Clear();
            }
            
            WaypointRoutes.Clear();
            TreatmentInstruction.InterceptCollection.Clear();
        }

        private static void RequestUGCSMission(Object source = null, ElapsedEventArgs args = null)
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
                    if (!UGCSClient.RequestingMission)
                        UGCSClient.RequestMission(SelectedMission.Id);
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
                Alerts.AddAlert(
                    AlertEntry.AlertLevel.Info,
                    AlertEntry.AlertType.UGCSStatus,
                    $"Selecting mission '{SelectedMission.Name}' with ID {SelectedMission.Id}");

                if (!UGCSClient.RequestingRoutes)
                    UGCSClient.RequestMission(SelectedMission.Id);
            }
            else
            {
                Alerts.AddAlert(
                    AlertEntry.AlertLevel.Info,
                    AlertEntry.AlertType.UGCSStatus,
                    $"Mission '{SelectedMission.Name}' with ID {SelectedMission.Id} is still the most recent");
            }
            
            AvailableMissions = e.Missions;
        }

        private static void UGCSClient_ReceivedMissionEvent(object sender, ReceivedMissionEventArgs e)
        {
            List<AreaScanPolygon> newAreaRoutes = new List<AreaScanPolygon>();
            List<WaypointRoute> newWaypointRoutes = new List<WaypointRoute>();

            foreach (Route r in e.Mission.Routes)
            {
                if (AreaScanPolygon.IsUGCSRouteAreaScanPolygon(r))
                    newAreaRoutes.Add(AreaScanPolygon.CreateFromUGCSRoute(r));
                else if (WaypointRoute.IsUGCSRouteWaypointRoute(r))
                    newWaypointRoutes.Add(WaypointRoute.CreateFromUGCSRoute(r));
            }

            var areaChanges = DetermineExisitngRouteUpdates(AreaScanPolygons, newAreaRoutes);
            var waypointRouteChanges = DetermineExisitngRouteUpdates(WaypointRoutes, newWaypointRoutes);

            UpdateRouteCollections(areaChanges, waypointRouteChanges);

            if (IsUGCSPollerRunning)
                ugcsPoller.Start();
        }

        private static void UpdateRouteCollections(RouteCollectionUpdates<AreaScanPolygon> areaChanges, RouteCollectionUpdates<WaypointRoute> waypointRouteChanges)
        {
            // First remove areas. When we add routes to the intercept collection further down we don't want to bother recalculating intercepts for these anway.
            if (areaChanges.AnyChanges && areaChanges.RemovedRouteIDs.Count > 0)
            {
                AreaScanPolygons.RemoveAll(a => areaChanges.RemovedRouteIDs.Contains(a.Id));
                TreatmentInstruction.InterceptCollection.RemoveAreaScansByIDs(areaChanges.RemovedRouteIDs);
            }

            if (waypointRouteChanges.AnyChanges)
            {
                if (waypointRouteChanges.RemovedRouteIDs.Count > 0)
                {
                    WaypointRoutes.RemoveAll(a => waypointRouteChanges.RemovedRouteIDs.Contains(a.Id));
                    TreatmentInstruction.InterceptCollection.RemoveWaypointRoutesByIDs(waypointRouteChanges.RemovedRouteIDs);
                }

                if (waypointRouteChanges.ModifiedRoutes.Count > 0)
                {
                    WaypointRoutes.RemoveAll(a => waypointRouteChanges.ModifiedRoutes.Exists(m => m.Id == a.Id));
                    WaypointRoutes.AddRange(waypointRouteChanges.ModifiedRoutes);
                    TreatmentInstruction.InterceptCollection.ModifyExistingWaypointRouteIntercepts(waypointRouteChanges.ModifiedRoutes, AreaScanPolygons);
                }

                if (waypointRouteChanges.AddedRoutes.Count > 0)
                {
                    WaypointRoutes.AddRange(waypointRouteChanges.AddedRoutes);
                    TreatmentInstruction.InterceptCollection.AddWaypointRoutes(waypointRouteChanges.AddedRoutes, AreaScanPolygons);
                }

                NotifyStaticPropertyChanged("WaypointRoutes");
                WaypointRoutesUpdated?.Invoke(null, new WaypointRoutesUpdatedArgs { Updates = waypointRouteChanges, AreaScanPolygonsUpdateFollowing = areaChanges.AnyChanges });

                Alerts.AddAlert(
                    AlertEntry.AlertLevel.Info,
                    AlertEntry.AlertType.UGCSStatus,
                    $"Updated routes. ({waypointRouteChanges.RemovedRouteIDs.Count}) routes removed, ({waypointRouteChanges.ModifiedRoutes.Count}) routes modified, ({waypointRouteChanges.AddedRoutes.Count}) routes added.");
            }

            // Adding or modifying any areas should be done only after we have the updated WaypointRoutes collection
            // When adding to or modifying the intercept collection we need to provide the current list of all waypoint routes to check for intercepts against
            if (areaChanges.AnyChanges)
            {
                if (areaChanges.ModifiedRoutes.Count > 0)
                {
                    AreaScanPolygons.RemoveAll(a => areaChanges.ModifiedRoutes.Exists(m => m.Id == a.Id));
                    AreaScanPolygons.AddRange(areaChanges.ModifiedRoutes);
                    TreatmentInstruction.InterceptCollection.ModifyExistingAreaScans(areaChanges.ModifiedRoutes, WaypointRoutes);
                }

                if (areaChanges.AddedRoutes.Count > 0)
                {
                    AreaScanPolygons.AddRange(areaChanges.AddedRoutes);
                    foreach (AreaScanPolygon area in areaChanges.AddedRoutes)
                        TreatmentInstruction.InterceptCollection.AddAreaScanWithWaypointRoutes(area, WaypointRoutes);
                }


                NotifyStaticPropertyChanged("AreaScanPolygons");
                AreaScanPolygonsUpdated?.Invoke(null, new AreaScanPolygonsUpdatedArgs() { Updates = areaChanges });

                Alerts.AddAlert(
                    AlertEntry.AlertLevel.Info,
                    AlertEntry.AlertType.UGCSStatus,
                    $"Updated areas. ({areaChanges.RemovedRouteIDs.Count}) areas removed, ({areaChanges.ModifiedRoutes.Count}) areas modified, ({areaChanges.AddedRoutes.Count}) areas added.");
            }
        }

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
                    // For some reason UGCS sets a new last modification time for ALL routes whenever you add/remove/modify a single route
                    // This implemented Equals function checks for modifications based on coordinates/ID/parameters
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
