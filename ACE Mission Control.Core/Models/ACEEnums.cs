using System;
using System.Collections.Generic;
using System.Text;
using Pbdrone;
using Google.Protobuf;

namespace ACE_Mission_Control.Core.Models
{
    public class ACEEnums
    {
        public enum StatusEnum
        {
            PrivateKeyClosed,
            NotConfigured,
            SearchingPreMission,
            ConnectingPreMission,
            ConnectedPreMission
        }

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
            NoConnectionKeyClosed,
            NoConnectionNotConfigured,
            MonitorSocketError,
            MonitorSSHError,
            MonitorCouldNotConnect,
            MonitorStarting,
            MonitorConnecting,
            CommanderConnecting,
            CommanderSocketError,
            CommanderSSHError,
            CommanderCouldNotConnect,
            CommanderStarting,
            ConnectionReady,
            ConnectionTimedOut,
            OBCStoppedResponding,
            OBCSlow,
            OBCError
        }

        public enum MessageType : int
        {
            Heartbeat = 0,
            InterfaceStatus = 1,
            FlightStatus = 2,
            ControlDevice = 3,
            Position = 4,
            FlightAnomaly = 5,
            ACEError = 6
        }
    }
}
