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

    public class AreaScanPolygonsUpdatedArgs
    {
        public RouteCollectionUpdates<AreaScanPolygon> updates { get; set; }
    }

    public class InstructionsUpdatedEventArgs
    {
        public List<TreatmentInstruction> Instructions { get; set; }

        public InstructionsUpdatedEventArgs() { Instructions = new List<TreatmentInstruction>(); }
    }

    public class MissionData : INotifyPropertyChanged
    {

        // Static Mission Data relevant for all instances

        public static event PropertyChangedEventHandler StaticPropertyChanged;

        public static event EventHandler<AreaScanPolygonsUpdatedArgs> AreaScanPolygonsUpdated;

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
                TreatmentInstruction.RemoveAreaScansByIDs(areaIDsToRemove);
            }

            if (waypointRouteChanges.AnyChanges)
            {
                var routeIDsToRemove = new List<int>(waypointRouteChanges.RemovedRouteIDs);
                routeIDsToRemove.AddRange(from r in waypointRouteChanges.ModifiedRoutes select r.Id);
                WaypointRoutes.RemoveAll(a => routeIDsToRemove.Contains(a.Id));
                TreatmentInstruction.RemoveWaypointRoutesByIDs(routeIDsToRemove);

                var routesToAdd = RoutesToWaypointRoutes(waypointRouteChanges.AddedRoutes).ToList();
                routesToAdd.AddRange(RoutesToWaypointRoutes(waypointRouteChanges.ModifiedRoutes));
                WaypointRoutes.AddRange(routesToAdd);

                // Unmodified areas only need to check against new routes
                foreach (AreaScanPolygon area in AreaScanPolygons)
                    TreatmentInstruction.AddTreatmentRouteIntercepts(area, routesToAdd);

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
                    TreatmentInstruction.AddTreatmentRouteIntercepts(area, WaypointRoutes);
                foreach (AreaScanPolygon area in areasUpdate.AddedRoutes)
                    TreatmentInstruction.AddTreatmentRouteIntercepts(area, WaypointRoutes);

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

        // Instance-only Mission Data

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler<InstructionsUpdatedEventArgs> InstructionUpdated;

        public ObservableCollection<TreatmentInstruction> TreatmentInstructions;

        public MissionData()
        {
            AreaScanPolygonsUpdated += MissionData_AreaScanPolygonsUpdated;
            TreatmentInstructions = new ObservableCollection<TreatmentInstruction>();
        }

        private void MissionData_AreaScanPolygonsUpdated(object sender, AreaScanPolygonsUpdatedArgs e)
        {
            // Remove all instructions that have removed polygons
            foreach (int removedID in e.updates.RemovedRouteIDs)
            {
                var removedInstruction = TreatmentInstructions.FirstOrDefault(i => removedID == i.TreatmentPolygon.Id);
                if (removedInstruction != null)
                    TreatmentInstructions.Remove(removedInstruction);
            }

            InstructionsUpdatedEventArgs updatedInstructions = new InstructionsUpdatedEventArgs();

            foreach (TreatmentInstruction instruction in TreatmentInstructions)
            {
                var newTreatmentArea = e.updates.ModifiedRoutes.FirstOrDefault(r => r.Id == instruction.TreatmentPolygon.Id);
                if (newTreatmentArea != null)
                {
                    // Update treatment areas if they were modified
                    instruction.UpdateTreatmentArea(newTreatmentArea);
                    updatedInstructions.Instructions.Add(instruction);
                }
                else
                {
                    var routeUpdated = instruction.RevalidateTreatmentRoute();
                    if (routeUpdated)
                        updatedInstructions.Instructions.Add(instruction);
                }   
            }

            // Add new treatment areas as instructions
            foreach (AreaScanPolygon addedArea in e.updates.AddedRoutes)
                TreatmentInstructions.Add(new TreatmentInstruction(addedArea));

            if (updatedInstructions.Instructions.Count > 0)
                InstructionUpdated?.Invoke(this, updatedInstructions);
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
