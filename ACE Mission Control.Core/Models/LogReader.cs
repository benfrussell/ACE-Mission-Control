using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

namespace ACE_Mission_Control.Core.Models
{
    public class LogReader
    {
        static List<IFlightTimeEntry> entries = new List<IFlightTimeEntry>();
        static FlightTimeParser timeParser = new FlightTimeParser();

        public static void ReadFromDirectory(string directory)
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
        }

        public static void ReadFromFilepath(string pilot, string filepath)
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

        public static void Read(DateTime date, string pilot, string machine, TextReader input)
        {
            IFlightTimeEntry entry = entries.FirstOrDefault(e => e.Date == date && e.Pilot == pilot && e.Machine == machine);
            if (entry == null)
            {
                entry = new FlightTimeEntry(pilot, machine);
                entries.Add(entry);
            }

            timeParser.Parse(input);
            entry.AddFlightHours(timeParser.TotalArmedHours);
            entry.AddManualHours(timeParser.TotalManualHours);
        }
    }
}
