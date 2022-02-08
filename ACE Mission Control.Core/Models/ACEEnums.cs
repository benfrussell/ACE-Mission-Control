using System;
using System.Collections.Generic;
using System.Text;
using Pbdrone;
using Google.Protobuf;

namespace ACE_Mission_Control.Core.Models
{
    public class ACEEnums
    {
        public enum MessageType : int
        {
            Heartbeat = 0,
            InterfaceStatus = 1,
            FlightStatus = 2,
            ControlDevice = 3,
            Telemetry = 4,
            FlightAnomaly = 5,
            ACEError = 6,
            MissionStatus = 7,
            MissionConfig = 8,
            CommandResponse = 9,
            AreaResult = 10,
            Configuration = 11,
            ConfigEntry = 12,
            MissionRoute = 13
        }

        public enum TurnType : int
        {
            NotSpecified = 0,
            StopAndTurn = 1,
            FlyThrough = 2
        }

        public enum ConnectionSummary
        {
            ConnectionDisabled,
            TryingACEConnection,
            ConnectedACELimited,
            ConnectedACE,
            TryingDroneConnection,
            ConnectedACEDroneLimited,
            ConnectedACEDrone
        }
    }
}
