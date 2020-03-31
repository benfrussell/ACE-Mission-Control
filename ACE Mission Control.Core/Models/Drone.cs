using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Numerics;
using Pbdrone;
using System.Collections.ObjectModel;

namespace ACE_Mission_Control.Core.Models
{
    public class Drone
    {
        public int ID;
        public string Name;
        public OnboardComputerClient OBCClient;
        public FlightStatus.Types.State FlightState;
        public List<Vector<double>> MissionArea;
        public ObservableCollection<AlertEntry> AlertLog;

        public Drone(int id, string name, string clientHostname, string clientUsername)
        {
            ID = id;
            Name = name;
            OBCClient = new OnboardComputerClient(this, clientHostname, clientUsername);
            AlertLog = new ObservableCollection<AlertEntry>();
        }

        public void AddAlert(AlertEntry entry)
        {
            AlertLog.Add(entry);
        }
    }
}
