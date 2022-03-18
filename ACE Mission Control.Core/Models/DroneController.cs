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
            UGCSClient.ReceivedMissionEvent += UGCSClient_ReceivedMissionEvent;
            UGCSClient.ReceivedVehicleListEvent += UGCSClient_ReceivedVehicleListEvent;
        }

        private static void UGCSClient_ReceivedVehicleListEvent(object sender, ReceivedVehicleListEventArgs e)
        {
            foreach (Vehicle v in e.Vehicles)
            {
                foreach (Drone d in Drones)
                {
                    if (v.Id == d.ID && v.NameSpecified)
                        d.Name = v.Name;
                }
            }
        }

        private static void UGCSClient_ReceivedMissionEvent(object sender, ReceivedMissionEventArgs e)
        {
            foreach (MissionVehicle mv in e.Mission.Vehicles)
            {
                var v = mv.Vehicle;
                var matchedDrone = Drones.Where(drone => drone.ID == v.Id).FirstOrDefault();
                if (matchedDrone == null)
                {
                    AddDrone(v.Id, FindDroneName(v.Id));
                }
            }
        }

        private static string FindDroneName(int droneID)
        {
            var matchedVehicle = UGCSClient.Vehicles.Where(v => v.Id == droneID).FirstOrDefault();
            if (matchedVehicle == null)
            {
                if (!UGCSClient.RequestingVehicles)
                    UGCSClient.RequestVehicleList();
            }
            else if (matchedVehicle.NameSpecified)
            {
                return matchedVehicle.Name;
            }
            return "Drone " + droneID.ToString();
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

        private static Drone AddDrone(int id, string name)
        {
            var obc = new OnboardComputerClient(id, "");
            var drone = new Drone(id, name, obc, new Mission());
            Drones.Add(drone);
            return drone;
        }
    }
}
