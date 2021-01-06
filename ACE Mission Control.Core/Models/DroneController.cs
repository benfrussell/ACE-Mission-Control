using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;
using UGCS.Sdk.Protocol.Encoding;

namespace ACE_Mission_Control.Core.Models
{
    public class DroneController
    {
        public const int MAX_DRONES = 10;
        private static List<int> droneIDs;

        private static ObservableCollection<Drone> drones;
        public static ObservableCollection<Drone> Drones
        {
            get { return drones; }
        }

        static DroneController()
        {
            droneIDs = new List<int>();
            drones = new ObservableCollection<Drone>();
            UGCSClient.StaticPropertyChanged += UGCSClient_StaticPropertyChanged;
            UGCSClient.ReceivedVehicleListEvent += UGCSClient_ReceivedVehicleListEvent;
        }

        public static void LoadDroneConfig()
        {
            throw new NotImplementedException();
        }

        public static void LoadUGCSDrones()
        {
            if (!UGCSClient.IsConnected && !UGCSClient.TryingConnections)
            {
                UGCSClient.StartTryingConnections();
            } 
            else if (UGCSClient.IsConnected)
            {
                UGCSClient.RequestVehicleList();
            }
        }

        private static void UGCSClient_StaticPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsConnected" && UGCSClient.IsConnected)
            {
                UGCSClient.RequestAvailableRoutes();
                UGCSClient.RequestVehicleList();
            }
            else if (e.PropertyName == "AvailableRoutes")
            {
                var routes = UGCSClient.AvailableRoutes;
                System.Diagnostics.Debug.WriteLine("Routes received");
            }
        }

        private static void UGCSClient_ReceivedVehicleListEvent(object sender, ReceivedVehicleListEventArgs e)
        {
            foreach (Vehicle v in e.Vehicles)
            {
                var matchedDrone = Drones.Where(drone => drone.ID == v.Id).FirstOrDefault();
                if (matchedDrone == null)
                {
                    if (v.NameSpecified)
                        Drones.Add(new Drone(v.Id, v.Name, "", ""));
                    else
                        Drones.Add(new Drone(v.Id, "Drone " + v.Id.ToString(), "", ""));
                } 
                else
                {
                    // Update all properties of the ACE Drone which are related to the UGCS Vehicle
                    matchedDrone.Name = v.Name;
                }
            }
        }
    }
}
