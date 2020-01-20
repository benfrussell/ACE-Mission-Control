using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class DroneController : ObservableCollection<Drone>
    {
        public const int MAX_DRONES = 5;
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
        }

        public static void LoadDroneConfig()
        {
            AddDrone("Drone 0");
        }

        public static void AddDrone()
        {
            if (drones.Count < MAX_DRONES)
            {
                var id = getNewDroneID();
                drones.Add(new Drone(id, "Drone " + id.ToString(), "", ""));
            }
        }

        public static void AddDrone(string name)
        {
            if (drones.Count < MAX_DRONES)
            {
                drones.Add(new Drone(getNewDroneID(), name, "", ""));
            }
        }

        // Set the number of drones available
        public static void SetDroneNum(int number)
        {
            if (number < drones.Count)
            {
                // Remove drones
                int end_val = number - 1;
                for (int i = drones.Count - 1; i > end_val; i--)
                {
                    drones.RemoveAt(i);
                }
            }
            else if (number > drones.Count && number <= MAX_DRONES)
            {
                // Add drones
                int end_val = number - drones.Count;
                for (int i = 0; i < end_val; i++)
                {
                    AddDrone();
                }
            }
        }

        private static int getNewDroneID()
        {
            bool found = false;
            int id = 0;
            while (found == false)
            {
                if (!droneIDs.Contains(id))
                {
                    found = true;
                    droneIDs.Add(id);
                }
                else
                {
                    id++;
                }
            }
            return id;
        }
    }
}
