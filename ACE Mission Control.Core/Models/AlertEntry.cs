using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class AlertEntry
    {
        public DateTime Timestamp;
        public ACEEnums.AlertLevel Level;
        public ACEEnums.AlertType Type;
        public string Info;
        public AlertEntry(ACEEnums.AlertLevel level, ACEEnums.AlertType type, string info = "")
        {
            Timestamp = DateTime.Now;
            Level = level;
            Type = type;
            Info = info;
        }
    }
}
