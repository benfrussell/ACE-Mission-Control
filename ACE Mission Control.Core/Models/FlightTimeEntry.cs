using ACE_Mission_Control.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core
{
    public class FlightTimeEntry : IFlightTimeEntry
    {
        public DateTime Date => throw new NotImplementedException();

        public string Machine { get; private set; }

        public string Pilot { get; private set; }

        public double FlightHours { get; private set; }

        public double ManualHours { get; private set; }

        public FlightTimeEntry(string pilot, string machine)
        {
            Pilot = pilot;
            Machine = machine;
        }

        public void AddFlightHours(double hours)
        {
            FlightHours += hours;
        }

        public void AddManualHours(double hours)
        {
            ManualHours += hours;
        }

    }
}
