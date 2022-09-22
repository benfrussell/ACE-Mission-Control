using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public interface IFlightTimeEntry
    {
        DateTime Date { get; }
        string Machine { get; }
        string Pilot { get; }
        double FlightHours { get; }
        double ManualHours { get; }

        void AddFlightHours(double hours);

        void AddManualHours(double hours);
    }
}
