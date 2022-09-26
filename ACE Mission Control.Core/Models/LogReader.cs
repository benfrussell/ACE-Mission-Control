using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

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

        public void ReadFromDirectory(string directory)
        {
            string[] topLevelCSVs = Directory.GetFiles(directory, "*.csv", SearchOption.TopDirectoryOnly);
            string[] subdirectories = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);

            foreach (string topLevelCSV in topLevelCSVs)
                ReadFromFilepath("Unnamed", topLevelCSV);

            foreach (string subdirectory in subdirectories)
            {
                string[] filepaths = Directory.GetFiles(subdirectory, "*.csv", SearchOption.AllDirectories);
                string pilotName = Path.GetDirectoryName(subdirectory);

                foreach (string filepath in filepaths)
                    ReadFromFilepath(pilotName, filepath);
            }

            SortAndRecalculateEntries();
        }

        public void ReadFromFilepath(string pilot, string filepath)
        {
            string[] splitFilename = Path.GetFileNameWithoutExtension(filepath).Split(' ');
            if (splitFilename.Length < 2)
                return;

            string[] splitDate = splitFilename[1].Split('_');
            DateTime date;
            try
            {
                date = new DateTime(int.Parse(splitDate[2]), int.Parse(splitDate[0]), int.Parse(splitDate[1]));
            }
            catch (FormatException) { return; }
            catch (IndexOutOfRangeException) { return; }

            Read(date, pilot, splitFilename[0], new StreamReader(filepath));
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
    }
}
