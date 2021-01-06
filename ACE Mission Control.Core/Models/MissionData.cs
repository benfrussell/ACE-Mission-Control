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

namespace ACE_Mission_Control.Core.Models
{
    public class MissionData
    {
        // TODO: Change entry point method to calculate based on the intersection of AreaScanRoutes and WaypointRoutes
        public ObservableCollection<AreaScanRoute> AreaScanRoutes;
        public MissionData(ObservableCollection<AreaScanRoute> areaScanRoutes)
        {
            AreaScanRoutes = areaScanRoutes;
        }

        public MissionData()
        {
            AreaScanRoutes = new ObservableCollection<AreaScanRoute>();
        }

        public async void AddRoutesFromFile(StorageFile file)
        {
            foreach (AreaScanRoute route in await CreateRoutesFromFile(file))
            {
                AreaScanRoutes.Add(route);
            }
        }

        public static async Task<MissionData> CreateMissionDataFromFile(StorageFile file)
        {
            List<AreaScanRoute> routes = await CreateRoutesFromFile(file);
            return new MissionData(new ObservableCollection<AreaScanRoute>(routes));
        }

        public static async Task<List<AreaScanRoute>> CreateRoutesFromFile(StorageFile file)
        {
            string fileText = await FileIO.ReadTextAsync(file);
            JObject fileJson = JObject.Parse(fileText);
            List<AreaScanRoute> routes = new List<AreaScanRoute>();

            if (fileJson.ContainsKey("mission"))
            {
                var routeTokens = fileJson["mission"]["routes"].Children();
                foreach (JToken routeToken in routeTokens)
                {
                    routes = routes.Concat(parseRoute(routeToken)).ToList();
                }
            }
            else if (fileJson.ContainsKey("route"))
            {
                var routeToken = fileJson["route"];
                routes = routes.Concat(parseRoute(routeToken)).ToList();
            }

            return routes;
        }

        private static List<AreaScanRoute> parseRoute(JToken routeToken)
        {
            List<AreaScanRoute> routes = new List<AreaScanRoute>();
            string routeName = routeToken["name"].ToObject<string>();

            foreach (JToken segmentToken in routeToken["segments"].Children())
            {
                if (segmentToken["type"].ToObject<string>() == "AreaScan")
                {
                    var pointsToken = segmentToken["polygon"]["points"].Children();
                    List<BasicGeoposition> routeArea = new List<BasicGeoposition>();

                    foreach (JToken pointToken in pointsToken)
                    {
                        var geop = new BasicGeoposition();
                        geop.Latitude = (180 / Math.PI) * pointToken["latitude"].ToObject<double>();
                        geop.Longitude = (180 / Math.PI) * pointToken["longitude"].ToObject<double>();

                        routeArea.Add(geop);
                    }

                    routes.Add(new AreaScanRoute(routeName, new Geopath(routeArea)));
                }
            }

            return routes;
        }
    }
}
