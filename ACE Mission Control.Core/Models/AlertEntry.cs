﻿using System;
using System.Collections.Generic;
using System.Text;

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
            ConnectionReady,
            ConnectionTimedOut,
            ConnectionNoResponse,
            ConnectionSearching,
            OBCSlow,
            OBCError,
            CommandResponse,
            CommandError
        }

        public DateTime Timestamp;
        public AlertLevel Level;
        public AlertType Type;
        public string Info;
        public AlertEntry(AlertLevel level, AlertType type, string info = "")
        {
            Timestamp = DateTime.Now;
            Level = level;
            Type = type;
            Info = info;
        }
    }
}
