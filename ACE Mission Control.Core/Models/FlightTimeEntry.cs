using ACE_Mission_Control.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core
{
    public interface IFlightTimeEntry : IComparable<IFlightTimeEntry>
    {
        DateTime Date { get; }
        string Machine { get; }
        string Pilot { get; }
        int TotalFlights { get; }
        double FlightHours { get; }
        double ManualHours { get; }
        double MachineFlightHoursToDate { get; }
        double PilotFlightHoursThisMachineToDate { get; }
        double PilotManualHoursThisMachineToDate { get; }
        double PilotFlightHoursAllMachinesToDate { get; }
        double PilotManualHoursAllMachinesToDate { get; }

        void AddFlights(int flights);
        void AddFlightHours(double hours);

        void AddManualHours(double hours);

        void SetCalculatedValues(double machineHours, double pilotFlightHoursThis, double pilotManualHoursThis, double pilotFlightHoursAll, double pilotManualHoursAll);
    }

    public class FlightTimeEntry : IFlightTimeEntry
    {
        public DateTime Date { get; private set; }

        public string Machine { get; private set; }

        public string Pilot { get; private set; }

        public int TotalFlights { get; private set; }

        public double FlightHours { get; private set; }

        public double ManualHours { get; private set; }

        public double MachineFlightHoursToDate { get; private set; }

        public double PilotFlightHoursThisMachineToDate { get; private set; }

        public double PilotFlightHoursAllMachinesToDate { get; private set; }

        public double PilotManualHoursThisMachineToDate { get; private set; }

        public double PilotManualHoursAllMachinesToDate { get; private set; }

        public FlightTimeEntry(DateTime date, string pilot, string machine)
        {
            Date = date;
            Pilot = pilot;
            Machine = machine;
        }

        public void AddFlights(int flights)
        {
            TotalFlights += flights;
        }

        public void AddFlightHours(double hours)
        {
            FlightHours += hours;
        }

        public void AddManualHours(double hours)
        {
            ManualHours += hours;
        }

        public int CompareTo(IFlightTimeEntry other)
        {
            if (Date > other.Date)
                return -1;
            else if (Date == other.Date)
                return 0;
            else
                return 1;
        }

        public void SetCalculatedValues(double machineHours, double pilotFlightHoursThis, double pilotManualHoursThis, double pilotFlightHoursAll, double pilotManualHoursAll)
        {
            MachineFlightHoursToDate = machineHours;
            PilotFlightHoursThisMachineToDate = pilotFlightHoursThis;
            PilotManualHoursThisMachineToDate = pilotManualHoursThis;
            PilotFlightHoursAllMachinesToDate = pilotFlightHoursAll;
            PilotManualHoursAllMachinesToDate = pilotManualHoursAll;
        }
    }
}
