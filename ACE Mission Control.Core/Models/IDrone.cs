using Pbdrone;
using System.Collections.Generic;
using System.ComponentModel;

namespace ACE_Mission_Control.Core.Models
{
    public interface IDrone : INotifyPropertyChanged
    {
        int ID { get; }
        List<ConfigEntry> ConfigEntries { get; set; }
        FlightStatus.Types.State FlightState { get; set; }
        InterfaceStatus.Types.State InterfaceState { get; set; }
        bool ManualCommandsOnly { get; set; }
        IMission Mission { get; set; }
        IOnboardComputerClient OBCClient { get; set; }
        string Name { get; set; }
        Drone.SyncState Synchronization { get; set; }
        ACEEnums.ConnectionSummary ConnectionStage { get; }
        bool IsNotConnected { get; }

        void SendCommand(Command command);
        void SendCommand(string command, bool autoCommand = false, Command.TriggerType trigger = Command.TriggerType.Normal, object tag = null);
        void Synchronize(bool manualSyncronize = false);
        void ToggleLock();
    }
}