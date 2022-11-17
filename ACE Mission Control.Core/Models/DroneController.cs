using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;
using UGCS.Sdk.Protocol.Encoding;
using System.Globalization;

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
                // Disabled for loading the static drone
                //UGCSClient.RequestVehicleList();
            }
        }

        public static void LoadStaticDrone()
        {
            AddDrone(0, "Drone");
        }

        public static void AlertDroneByID(int id, AlertEntry entry, bool blockDuplicates = false)
        {
            Drone drone = Drones.FirstOrDefault(d => d.ID == id);
            if (drone == null)
                throw new ArgumentException($"Tried to alert drone with ID {id} but that drone does not exist.");
            else
                drone.AddAlert(entry, blockDuplicates);
        }

        public static void AlertAllDrones(AlertEntry entry, bool blockDuplicates = false)
        {
            foreach (Drone d in Drones)
                d.AddAlert(entry, blockDuplicates);
        }

        private static void UGCSClient_ReceivedVehicleListEvent(object sender, ReceivedVehicleListEventArgs e)
        {
            foreach (Vehicle v in e.Vehicles)
            {
                var matchedDrone = Drones.Where(drone => drone != null && drone.ID == v.Id).FirstOrDefault();
                if (matchedDrone == null)
                {
                    if (v.NameSpecified)
                        AddDrone(v.Id, v.Name);
                    else
                        AddDrone(v.Id, "Drone " + v.Id.ToString());
                }
                else
                {
                    // Update all properties of the ACE Drone which are related to the UGCS Vehicle
                    matchedDrone.Name = v.Name;
                }
            }
        }

        private static void AddDrone(int id, string name)
        {
            var obc = new OnboardComputerClient(id, "");
            Drones.Add(new Drone(id, name, obc, new Mission()));
        }
    }
}
