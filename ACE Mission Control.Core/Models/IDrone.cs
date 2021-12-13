using Pbdrone;
using System.Collections.Generic;
using System.ComponentModel;

namespace ACE_Mission_Control.Core.Models
{
    public interface IDrone : INotifyPropertyChanged
    {
        bool AwayOnMission { get; }
        List<ConfigEntry> ConfigEntries { get; set; }
        FlightStatus.Types.State FlightState { get; set; }
        InterfaceStatus.Types.State InterfaceState { get; set; }
        bool ManualCommandsOnly { get; set; }
        IMission Mission { get; set; }
        string Name { get; set; }
        Drone.SyncState Synchronization { get; set; }
        bool HasUnsentChanges { get; }

        void AddAlert(AlertEntry entry, bool blockDuplicates = false);
        void SendCommand(Command command);
        void SendCommand(string command, bool autoCommand = false, bool syncCommand = false, object tag = null);
        void Synchronize(bool manualSyncronize = false);
        void UploadMission();
    }
}