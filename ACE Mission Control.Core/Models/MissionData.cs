using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UGCS.Sdk.Protocol.Encoding;
using System.Numerics;
using System.Collections.ObjectModel;

namespace ACE_Mission_Control.Core.Models
{
    public class MissionData
    {
        private string _name;
        public string Name
        {
            get { return _name; }
        }

        public ObservableCollection<AreaScanRoute> AreaScanRoutes;
        public MissionData(string name, ObservableCollection<AreaScanRoute> areaScanRoutes)
        {
            _name = name;
            AreaScanRoutes = areaScanRoutes;
        }

        public MissionData()
        {
            _name = null;
            AreaScanRoutes = new ObservableCollection<AreaScanRoute>();
        }

        public static async Task<MissionData> CreateMissionDataFromFile(StorageFile file)
        {
            string fileText = await FileIO.ReadTextAsync(file);
            JObject fileJson = JObject.Parse(fileText);
            string missionName = fileJson["mission"]["name"].ToObject<string>();

            var routeTokens = fileJson["mission"]["routes"].Children();
            ObservableCollection<AreaScanRoute> routes = new ObservableCollection<AreaScanRoute>();

            foreach (JToken routeToken in routeTokens)
            {
                string routeName = routeToken["name"].ToObject<string>();

                foreach (JToken segmentToken in routeToken["segments"].Children())
                {
                    if (segmentToken["type"].ToObject<string>() == "AreaScan")
                    {
                        var pointsToken = segmentToken["polygon"]["points"].Children();
                        List<double[]> routeArea = new List<double[]>();

                        foreach (JToken pointToken in pointsToken)
                        {
                            double lat = pointToken["latitude"].ToObject<double>();
                            double lon = pointToken["longitude"].ToObject<double>();

                            routeArea.Add(new double[2] { lat, lon });
                        }

                        routes.Add(new AreaScanRoute(routeName, routeArea));
                    }
                }
            }

            return new MissionData(missionName, routes);
        }
    }
}
