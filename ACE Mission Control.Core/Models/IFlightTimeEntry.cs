using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core.Models
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
}
