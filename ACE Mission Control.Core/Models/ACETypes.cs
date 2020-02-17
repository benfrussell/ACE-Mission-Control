﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public static class ACETypes
    {
        public enum StatusEnum
        {
            PrivateKeyClosed,
            NotConfigured,
            SearchingPreMission,
            ConnectingPreMission,
            ConnectedPreMission
        }

        public struct AlertEntry
        {
            public AlertLevel Level;
            public AlertType Type;
            public string Info;
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
            ConnectionTimedOut
        }

        public static AlertEntry MakeAlertEntry(AlertLevel level, AlertType type, string info = "")
        {
            AlertEntry entry = new AlertEntry();
            entry.Level = level;
            entry.Type = type;
            entry.Info = info;
            return entry;
        }
    }
}
