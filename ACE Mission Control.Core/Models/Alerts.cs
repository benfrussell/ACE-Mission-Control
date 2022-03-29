using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace ACE_Mission_Control.Core.Models
{
    public class AlertEntry
    {
        public enum AlertLevel
        {
            None,
            Info, // Info about what's going on
            Medium, // Something unexpected, action not critical
            High, // Something unexpected, action IS critical
        }

        public enum AlertType
        {
            None,
            NoConnection,
            NoConnectionNotConfigured,
            ConnectionStarting,
            OBCReady,
            ChaperoneConnectionFailed,
            DirectorConnectionFailed,
            OBCSlow,
            OBCError,
            CommandResponse,
            CommandError,
            UGCSStatus,
            FinishedExecution,
            ExecutionTimeUpdated
        }

        public DateTime Timestamp;
        public IDrone AssociatedDrone;
        public AlertLevel Level;
        public AlertType Type;
        public string Info;
        public AlertEntry(AlertLevel level, AlertType type, string info = "")
        {
            Timestamp = DateTime.Now;
            AssociatedDrone = null;
            Level = level;
            Type = type;
            Info = info;
        }

        public AlertEntry(IDrone drone, AlertLevel level, AlertType type, string info = "")
        {
            Timestamp = DateTime.Now;
            AssociatedDrone = drone;
            Level = level;
            Type = type;
            Info = info;
        }
    }
    public class Alerts : INotifyPropertyChanged
    {
        public static event PropertyChangedEventHandler StaticPropertyChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private static AlertEntry.AlertType LastAlertType
        {
            get
            {
                if (AlertLog.Count == 0)
                    return AlertEntry.AlertType.None;
                return AlertLog[AlertLog.Count - 1].Type;
            }
        }

        private static ObservableCollection<AlertEntry> alertLog;
        public static ObservableCollection<AlertEntry> AlertLog
        {
            get => alertLog;
            private set
            {
                if (alertLog == value)
                    return;
                alertLog = value;
                NotifyStaticPropertyChanged();
            }
        }

        private static SynchronizationContext syncContext;
        private static bool initialized;

        static Alerts()
        {
            AlertLog = new ObservableCollection<AlertEntry>();
            initialized = false;
        }

        public static void Initialize()
        {
            syncContext = SynchronizationContext.Current;
            initialized = true;
        }

        public static void AddDroneAlert(object sender, AlertEntry.AlertLevel level, AlertEntry.AlertType type, string info = "", bool blockDuplicates = false)
        {
            IDrone associatedDrone = null;
            switch (sender)
            {
                case IDrone d:
                    associatedDrone = d;
                    break;
                case IMission m:
                    foreach (IDrone d in DroneController.Drones)
                        if (d.Mission == m)
                            associatedDrone = d;
                    break;
                case IOnboardComputerClient o:
                    foreach (IDrone d in DroneController.Drones)
                        if (d.OBCClient == o)
                            associatedDrone = d;
                    break;
            }

            if (associatedDrone != null)
                AddAlert(new AlertEntry(associatedDrone, level, type, info), blockDuplicates);
            else
                AddAlert(new AlertEntry(level, type, info), blockDuplicates);
        }

        public static void AddAlert(AlertEntry.AlertLevel level, AlertEntry.AlertType type, string info = "", bool blockDuplicates = false)
        {
            AddAlert(new AlertEntry(level, type, info), blockDuplicates);
        }

        public static void AddAlert(AlertEntry entry, bool blockDuplicates = false)
        {
            if (!initialized)
                throw new InvalidOperationException("Tried to add an alert before Alerts was initialized.");

            if (blockDuplicates && entry.Type == LastAlertType)
                return;
            
            syncContext.Post(
                new SendOrPostCallback((_) => AlertLog.Add(entry)),
                null
            );
        }

        private static void NotifyStaticPropertyChanged([CallerMemberName] string propertyName = "")
        {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
        }
    }
}
