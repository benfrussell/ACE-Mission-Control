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

        public AlertEntry.AlertType LastAlertType
        {
            get
            {
                if (AlertLog.Count == 0)
                    return AlertEntry.AlertType.None;
                return AlertLog[AlertLog.Count - 1].Type;
            }
        }

        public Drone(int id, string name, string clientHostname, string clientUsername)
        {
            ID = id;
            Name = name;
            OBCClient = new OnboardComputerClient(this, clientHostname, clientUsername);
            AlertLog = new ObservableCollection<AlertEntry>();
        }

        public void AddAlert(AlertEntry entry, bool blockDuplicates = false)
        {
            if (blockDuplicates && entry.Type == LastAlertType)
                return;
            AlertLog.Add(entry);
        }
    }
}
