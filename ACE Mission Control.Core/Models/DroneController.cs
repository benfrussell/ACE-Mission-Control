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
        private static List<int> DroneIDs;

        private static ObservableCollection<IDrone> drones;
        public static ObservableCollection<IDrone> Drones
        {
            get { return drones; }
        }

        static DroneController()
        {
            DroneIDs = new List<int>();
            drones = new ObservableCollection<IDrone>();
            UGCSClient.ReceivedMissionEvent += UGCSClient_ReceivedMissionEvent;
            UGCSClient.ReceivedVehicleListEvent += UGCSClient_ReceivedVehicleListEvent;
        }

        private static void UGCSClient_ReceivedVehicleListEvent(object sender, ReceivedVehicleListEventArgs e)
        {
            foreach (Vehicle v in e.Vehicles)
            {
                foreach (IDrone d in Drones)
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
                var matchedIDrone = Drones.Where(IDrone => IDrone.ID == v.Id).FirstOrDefault();
                if (matchedIDrone == null)
                {
                    AddDrone(v.Id, FindDroneName(v.Id));
                }
            }
        }

        private static string FindDroneName(int IDroneID)
        {
            var matchedVehicle = UGCSClient.Vehicles.Where(v => v.Id == IDroneID).FirstOrDefault();
            if (matchedVehicle == null)
            {
                if (!UGCSClient.RequestingVehicles)
                    UGCSClient.RequestVehicleList();
            }
            else if (matchedVehicle.NameSpecified)
            {
                return matchedVehicle.Name;
            }
            return "IDrone " + IDroneID.ToString();
        }

        private static IDrone AddDrone(int id, string name)
        {
            var obc = new OnboardComputerClient("");
            var IDrone = new Drone(id, name, obc, new Mission());
            Drones.Add(IDrone);
            return IDrone;
        }
    }
}
