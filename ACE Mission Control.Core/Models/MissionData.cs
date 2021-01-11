using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Windows.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UGCS.Sdk.Protocol.Encoding;
using System.Numerics;
using System.Collections.ObjectModel;
using Windows.Devices.Geolocation;
using NetTopologySuite.Geometries;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;

namespace ACE_Mission_Control.Core.Models
{
    public class MissionData : INotifyPropertyChanged
    {
        // Static Mission Data relevant for all instances

        public static event PropertyChangedEventHandler StaticPropertyChanged;

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

        private static void RequestUGCSRoutes(Object source = null, ElapsedEventArgs args = null)
        {
            if (UGCSClient.IsConnected)
                UGCSClient.RequestRecentMissionRoutes();
            else if (IsUGCSPollerRunning)
                ugcsPoller.Start();
        }

        private static void UGCSClient_ReceivedRecentRoutesEvent(object sender, ReceivedRecentRoutesEventArgs e)
        {
            var areaScans = new List<AreaScanPolygon>();
            var waypointRoutes = new List<WaypointRoute>();

            foreach (Route r in e.Routes)
            {
                if (AreaScanPolygon.IsUGCSRouteAreaScanPolygon(r))
                    areaScans.Add(AreaScanPolygon.CreateFromUGCSRoute(r));
                else if (WaypointRoute.IsUGCSRouteWaypointRoute(r))
                    waypointRoutes.Add(WaypointRoute.CreateFromUGCSRoute(r));
            }

            AreaScanPolygons = areaScans;
            // This update needs to happen last to properly trigger the update
            WaypointRoutes = waypointRoutes;

            if (IsUGCSPollerRunning)
                ugcsPoller.Start();
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
            TreatmentInstructions = new ObservableCollection<TreatmentInstruction>();
            UpdateTreatmentInstructions();
        }

        private void UpdateTreatmentInstructions(bool doTreatment = true)
        {
            var areaScanIDs = (from a in AreaScanPolygons select a.ID).ToList();
            // Select and remove all TreatmentInstructions where the treatment area IDs are not among the new area scan IDs
            var removedInstructions = TreatmentInstructions.Where(i => !areaScanIDs.Contains(i.ID));
            foreach (var removed in removedInstructions)
                TreatmentInstructions.Remove(removed);

            var treatmentAreaIDs = (from i in TreatmentInstructions select i.ID).ToList();
            // Select and add all AreaScanPolygons where the ID doesn't already exist among the treatment instruction area IDs
            var addedAreas = AreaScanPolygons.Where(a => !treatmentAreaIDs.Contains(a.ID));
            foreach (var addedArea in addedAreas)
            {
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
