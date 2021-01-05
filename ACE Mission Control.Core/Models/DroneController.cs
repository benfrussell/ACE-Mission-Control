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
            UGCSClient.VehicleModificationEvent += UGCSClient_VehicleModificationEvent;
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
            if (e.PropertyName == "IsConnected")
            {
                UGCSClient.RequestVehicleList();
            }
        }

        private static void UGCSClient_VehicleModificationEvent(object sender, VehicleModificationEventArgs e)
        {
            if (e.Modification == ModificationType.MT_CREATE && !Drones.Any(item => item.ID == e.Vehicle.Id))
            {
                if (e.Vehicle.NameSpecified)
                    Drones.Add(new Drone(e.Vehicle.Id, e.Vehicle.Name, "", ""));
                else
                    Drones.Add(new Drone(e.Vehicle.Id, "Drone " + e.Vehicle.Id.ToString(), "", ""));
            }
        }
    }
}
