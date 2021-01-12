using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UGCS.Sdk.Protocol.Encoding;
using System.Numerics;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;
using UGCS.Sdk.Protocol;

namespace ACE_Mission_Control.Core.Models
{
    public class RouteCollectionUpdates<T>
    {
        public bool AnyUpdates 
        {
            get
            {
                return RemovedRouteIDs.Count > 0 ||
                    AddedRoutes.Count > 0 ||
                    ModifiedRoutes.Count > 0;
            }
        }
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

    public class RoutesUpdatedEventArgs : EventArgs
    {
        public RouteCollectionUpdates<WaypointRoute> WaypointRouteChanges;
        public RouteCollectionUpdates<AreaScanPolygon> AreaChanges;

        public RoutesUpdatedEventArgs()
        {
            WaypointRouteChanges = new RouteCollectionUpdates<WaypointRoute>();
            AreaChanges = new RouteCollectionUpdates<AreaScanPolygon>();
        }
    }

    public class MissionData : INotifyPropertyChanged
    {

        // Static Mission Data relevant for all instances

        public static event PropertyChangedEventHandler StaticPropertyChanged;

        public static event EventHandler<RoutesUpdatedEventArgs> RoutesUpdatedEvent;

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

        static MissionData()
        {
            AreaScanPolygons = new List<AreaScanPolygon>();
            WaypointRoutes = new List<WaypointRoute>();
            UGCSClient.ReceivedRecentRoutesEvent += UGCSClient_ReceivedRecentRoutesEvent;
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

        private static void RequestUGCSRoutes(Object source = null, ElapsedEventArgs args = null)
        {
            if (UGCSClient.IsConnected)
                UGCSClient.RequestRecentMissionRoutes();
            else if (IsUGCSPollerRunning)
                ugcsPoller.Start();
        }

        private static void UGCSClient_ReceivedRecentRoutesEvent(object sender, ReceivedRecentRoutesEventArgs e)
        {
            // Update routes such that: Route conversions are minimized, all updates to the route lists are captured, and any unchanged routes are not reinstanced in the list
            // This is probably completely unnecessary but I spent a day doing it anyway
            List<Route> newAreaRoutes = new List<Route>();
            List<Route> newWaypointRoutes = new List<Route>();

            foreach (Route r in e.Routes)
            {
                if (AreaScanPolygon.IsUGCSRouteAreaScanPolygon(r))
                    newAreaRoutes.Add(r);
                else if (WaypointRoute.IsUGCSRouteWaypointRoute(r))
                    newWaypointRoutes.Add(r);
            }

            RouteCollectionUpdates<AreaScanPolygon> areaChanges = RouteUpdatesToAreaScans(DetermineExisitngRouteUpdates(AreaScanPolygons, newAreaRoutes));
            RouteCollectionUpdates<WaypointRoute> waypointRouteChanges = RouteUpdatesToWaypointRoutes(DetermineExisitngRouteUpdates(WaypointRoutes, newWaypointRoutes));

            if (areaChanges.AnyUpdates)
            {
                // Remove anything removed or modified, then add anything new or modified
                AreaScanPolygons.RemoveAll(a => areaChanges.RemovedRouteIDs.Any(r => r == a.Id) || areaChanges.ModifiedRoutes.Any(m => m.Id == a.Id));
                AreaScanPolygons.AddRange(areaChanges.ModifiedRoutes);
                AreaScanPolygons.AddRange(areaChanges.AddedRoutes);
            }
            
            if (waypointRouteChanges.AnyUpdates)
            {
                WaypointRoutes.RemoveAll(a => waypointRouteChanges.RemovedRouteIDs.Any(r => r == a.Id) || waypointRouteChanges.ModifiedRoutes.Any(m => m.Id == a.Id));
                WaypointRoutes.AddRange(waypointRouteChanges.ModifiedRoutes);
                WaypointRoutes.AddRange(waypointRouteChanges.AddedRoutes);
            }

            if (areaChanges.AnyUpdates || waypointRouteChanges.AnyUpdates)
            {
                RoutesUpdatedEventArgs updateArgs = new RoutesUpdatedEventArgs()
                {
                    AreaChanges = areaChanges,
                    WaypointRouteChanges = waypointRouteChanges
                };
                RoutesUpdatedEvent(null, updateArgs);
            }

            if (IsUGCSPollerRunning)
                ugcsPoller.Start();
        }

        // Finds and returns all of the updates to match an exisiting route list to a new route list
        private static RouteCollectionUpdates<Route> DetermineExisitngRouteUpdates<T>(List<T> existingRouteList, List<Route> newRouteList) where T : IComparableRoute
        {
            var changes = new RouteCollectionUpdates<Route>();
            // Start with a list of all IDs and remove them as we find matches in the new list
            changes.RemovedRouteIDs = (from existingRoute in existingRouteList select existingRoute.Id).ToList();

            foreach (Route newRoute in newRouteList)
            {
                var matchInExistingRoutes = existingRouteList.First(existingRoute => existingRoute.Id == newRoute.Id);
                if (matchInExistingRoutes == null)
                {
                    changes.AddedRoutes.Add(newRoute);
                }
                else
                {
                    changes.RemovedRouteIDs.Remove(matchInExistingRoutes.Id);
                    if (newRoute.LastModificationTime > matchInExistingRoutes.LastModificationTime)
                        changes.ModifiedRoutes.Add(newRoute);
                }
            }

            return changes;
        }

        private static RouteCollectionUpdates<AreaScanPolygon> RouteUpdatesToAreaScans(RouteCollectionUpdates<Route> routeUpdates)
        {
            var areaScanUpdates = new RouteCollectionUpdates<AreaScanPolygon>();
            areaScanUpdates.RemovedRouteIDs = routeUpdates.RemovedRouteIDs;
            areaScanUpdates.AddedRoutes = routeUpdates.AddedRoutes.ConvertAll(route => AreaScanPolygon.CreateFromUGCSRoute(route));
            areaScanUpdates.ModifiedRoutes = routeUpdates.ModifiedRoutes.ConvertAll(route => AreaScanPolygon.CreateFromUGCSRoute(route));
            return areaScanUpdates;
        }

        private static RouteCollectionUpdates<WaypointRoute> RouteUpdatesToWaypointRoutes(RouteCollectionUpdates<Route> routeUpdates)
        {
            var areaScanUpdates = new RouteCollectionUpdates<WaypointRoute>();
            areaScanUpdates.RemovedRouteIDs = routeUpdates.RemovedRouteIDs;
            areaScanUpdates.AddedRoutes = routeUpdates.AddedRoutes.ConvertAll(route => WaypointRoute.CreateFromUGCSRoute(route));
            areaScanUpdates.ModifiedRoutes = routeUpdates.ModifiedRoutes.ConvertAll(route => WaypointRoute.CreateFromUGCSRoute(route));
            return areaScanUpdates;
        }

        private static void NotifyStaticPropertyChanged([CallerMemberName] string propertyName = "")
        {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
        }

        // Instance-only Mission Data

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<TreatmentInstruction> TreatmentInstructions;

        public MissionData()
        {
            StaticPropertyChanged += MissionData_StaticPropertyChanged;
            RoutesUpdatedEvent += MissionData_RoutesUpdatedEvent;
            TreatmentInstructions = new ObservableCollection<TreatmentInstruction>();
            UpdateTreatmentInstructions();
        }

        private void MissionData_RoutesUpdatedEvent(object sender, RoutesUpdatedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void UpdateTreatmentInstructions(bool doTreatment = true)
        {
            var areaScanIDs = (from a in AreaScanPolygons select a.Id).ToList();
            // Select and remove all TreatmentInstructions where the treatment area IDs are not among the new area scan IDs
            var removedInstructions = TreatmentInstructions.Where(i => !areaScanIDs.Contains(i.ID));
            foreach (var removed in removedInstructions)
            {
                TreatmentInstructions.Remove(removed);
                System.Diagnostics.Debug.WriteLine($"Removing instruction {removed.Name}");
            }
                

            var treatmentAreaIDs = (from i in TreatmentInstructions select i.ID).ToList();
            // Select and add all AreaScanPolygons where the ID doesn't already exist among the treatment instruction area IDs
            var addedAreas = AreaScanPolygons.Where(a => !treatmentAreaIDs.Contains(a.Id));
            foreach (var addedArea in addedAreas)
            {
                System.Diagnostics.Debug.WriteLine($"Adding instruction {addedArea.Name}");
                TreatmentInstructions.Add(new TreatmentInstruction()
                {
                    TreatmentPolygon = addedArea,
                    AutoCalcUnlock = true,
                    AutoCalcLock = true,
                    DoTreatment = doTreatment
                });
            }

            // Finally update the valid treatment routes for each instruction
            foreach (var instruction in TreatmentInstructions)
                instruction.UpdateValidTreatmentRoutes(WaypointRoutes);
        }

        private void MissionData_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Both AreaScanPolygons and WaypointRoutes will always update at the time
            // WaypointRoutes is the last one to update, so we can do the mission update after that one
            if (e.PropertyName == "WaypointRoutes")
                UpdateTreatmentInstructions();
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Deprecated file import code

        //public async void AddRoutesFromFile(StorageFile file)
        //{
        //    foreach (AreaScanPolygon route in await CreateRoutesFromFile(file))
        //    {
        //        AreaScanRoutes.Add(route);
        //    }
        //}

        //public static async Task<MissionData> CreateMissionDataFromFile(StorageFile file)
        //{
        //    List<AreaScanPolygon> routes = await CreateRoutesFromFile(file);
        //    return new MissionData(new ObservableCollection<AreaScanPolygon>(routes));
        //}

        //public static async Task<List<AreaScanPolygon>> CreateRoutesFromFile(StorageFile file)
        //{
        //    string fileText = await FileIO.ReadTextAsync(file);
        //    JObject fileJson = JObject.Parse(fileText);
        //    List<AreaScanPolygon> routes = new List<AreaScanPolygon>();

        //    if (fileJson.ContainsKey("mission"))
        //    {
        //        var routeTokens = fileJson["mission"]["routes"].Children();
        //        foreach (JToken routeToken in routeTokens)
        //        {
        //            routes = routes.Concat(parseRoute(routeToken)).ToList();
        //        }
        //    }
        //    else if (fileJson.ContainsKey("route"))
        //    {
        //        var routeToken = fileJson["route"];
        //        routes = routes.Concat(parseRoute(routeToken)).ToList();
        //    }

        //    return routes;
        //}

        //private static List<AreaScanPolygon> parseRoute(JToken routeToken)
        //{
        //    List<AreaScanPolygon> routes = new List<AreaScanPolygon>();
        //    string routeName = routeToken["name"].ToObject<string>();

        //    foreach (JToken segmentToken in routeToken["segments"].Children())
        //    {
        //        if (segmentToken["type"].ToObject<string>() == "AreaScan")
        //        {
        //            var pointsToken = segmentToken["polygon"]["points"].Children();
        //            List<BasicGeoposition> routeArea = new List<BasicGeoposition>();

        //            foreach (JToken pointToken in pointsToken)
        //            {
        //                var geop = new BasicGeoposition();
        //                geop.Latitude = (180 / Math.PI) * pointToken["latitude"].ToObject<double>();
        //                geop.Longitude = (180 / Math.PI) * pointToken["longitude"].ToObject<double>();

        //                routeArea.Add(geop);
        //            }

        //            routes.Add(new AreaScanPolygon(routeName, new Geopath(routeArea)));
        //        }
        //    }

        //    return routes;
        //}
    }
}
