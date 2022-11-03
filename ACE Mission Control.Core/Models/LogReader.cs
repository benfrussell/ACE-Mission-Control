using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ACE_Mission_Control.Core.Models
{
    public class LogReader
    {
        List<IFlightTimeEntry> entries;
        FlightTimeParser timeParser;

        public LogReader()
        {
            entries = new List<IFlightTimeEntry>();
            timeParser = new FlightTimeParser();
        }

        public IEnumerable<IFlightTimeEntry> Entries { get => entries.AsEnumerable(); }

        public static string GetMachineNameFromFilename(string filename)
        {
            string[] splitFilename = filename.Split(' ');
            return splitFilename[0];
        }

        public static DateTime GetDateFromFilename(string filename)
        {
            string[] splitFilename = filename.Split(' ');
            if (splitFilename.Length < 2)
                return DateTime.MinValue;

            string[] splitDate = splitFilename[1].Split('_');
            DateTime date;
            try
            {
                date = new DateTime(int.Parse(splitDate[2]), int.Parse(splitDate[0]), int.Parse(splitDate[1]));
                return date;
            }
            catch (FormatException) { return DateTime.MinValue; }
            catch (IndexOutOfRangeException) { return DateTime.MinValue; }
        }

        public void ClearEntries()
        {
            entries.Clear();
        }

        public Task ReadAsync(DateTime date, string pilot, string machine, TextReader input)
        {
            return Task.Run(() => Read(date, pilot, machine, input));
        }

        public void Read(DateTime date, string pilot, string machine, TextReader input)
        {
            IFlightTimeEntry entry = entries.FirstOrDefault(e => e.Date == date && e.Pilot == pilot && e.Machine == machine);
            if (entry == null)
            {
                entry = new FlightTimeEntry(date, pilot, machine);
                entries.Add(entry);
            }

            timeParser.Parse(input);
            entry.AddFlights(timeParser.TotalFlights);
            entry.AddFlightHours(timeParser.TotalFlightHours);
            entry.AddManualHours(timeParser.ManualFlightHours);
        }

        private V SetOrAdd<K, V>(Dictionary<K, V> dict, K key, V value, Func<V, V, V> addFunction)
        {
            if (dict.ContainsKey(key))
            {
                var newValue = addFunction(dict[key], value);
                dict[key] = newValue;
                return newValue;
            }
                
            dict[key] = value;
            return value;
        }

        public void SortAndRecalculateEntries()
        {
            entries.Sort();
            Dictionary<string, double> allHoursByMachine = new Dictionary<string, double>();
            Dictionary<Tuple<string, string>, double> allHoursByPilotMachine = new Dictionary<Tuple<string, string>, double>();
            Dictionary<Tuple<string, string>, double> manualHoursByPilotMachine = new Dictionary<Tuple<string, string>, double>();
            Dictionary<string, double> allHoursByPilot = new Dictionary<string, double>();
            Dictionary<string, double> manualHoursByPilot = new Dictionary<string, double>();


            foreach (IFlightTimeEntry entry in Entries.Reverse())
            {
                var pilotMachine = new Tuple<string, string>(entry.Pilot, entry.Machine);

                entry.SetCalculatedValues(
                    SetOrAdd(allHoursByMachine, entry.Machine, entry.FlightHours, (x, y) => x + y),
                    SetOrAdd(allHoursByPilotMachine, pilotMachine, entry.FlightHours, (x, y) => x + y),
                    SetOrAdd(manualHoursByPilotMachine, pilotMachine, entry.ManualHours, (x, y) => x + y),
                    SetOrAdd(allHoursByPilot, entry.Pilot, entry.FlightHours, (x, y) => x + y),
                    SetOrAdd(manualHoursByPilot, entry.Pilot, entry.ManualHours, (x, y) => x + y));
            }
        }

        public void ExportEntries(TextWriter output)
        {
            output.WriteLineAsync("Date,Machine,Pilot,Flights,Flight Hours,Manual Flight Hours,Machine Flight Hours to Date,Pilot Flying Hours To Date This Machine,Pilot Manual Flying Hours To Date This Machine,Pilot Flying Hours To Date All Machines,Pilot Manual Flying Hours To Date All Machines");
            var entriesList = Entries.ToList();
            for (int i = 0; i < entriesList.Count(); i++)
            {
                var entry = entriesList[i];
                var rowString = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                    entry.Date.ToString("yyyy-MM-dd"),
                    entry.Machine,
                    entry.Pilot,
                    entry.TotalFlights,
                    entry.FlightHours,
                    entry.ManualHours,
                    entry.MachineFlightHoursToDate,
                    entry.PilotFlightHoursThisMachineToDate,
                    entry.PilotManualHoursThisMachineToDate,
                    entry.PilotFlightHoursAllMachinesToDate,
                    entry.PilotManualHoursAllMachinesToDate);

                // On the last line don't include a line terminator
                if (i == entriesList.Count() - 1)
                    output.WriteAsync(rowString);
                else
                    output.WriteLineAsync(rowString);
            }
        }
    }
}
