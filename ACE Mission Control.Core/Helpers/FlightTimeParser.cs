using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE_Mission_Control.Core.Helpers
{
    public class FlightTimeParser
    {
        DateTime? manualFlightStartTime;
        DateTime? flyingStartTime;

        public int TotalFlights { get; private set; }
        public double ManualFlightHours { get; private set; }
        public double TotalFlightHours { get; private set; }
        public bool HeaderInvalid { get; private set; }
        public bool InputEmpty { get; private set; }

        public FlightTimeParser()
        {
            ResetParser();
        }

        private void ResetParser()
        {
            manualFlightStartTime = null;
            flyingStartTime = null;
            TotalFlights = 0;
            ManualFlightHours = 0;
            TotalFlightHours = 0;
            InputEmpty = false;
            HeaderInvalid = false;
        }

        private void EndManualTime(DateTime endTime)
        {
            if (manualFlightStartTime == null)
                return;
            ManualFlightHours += endTime.Subtract(manualFlightStartTime.Value).TotalHours;
            manualFlightStartTime = null;
        }

        private void EndFlyingTime(DateTime endTime)
        {
            if (flyingStartTime == null || endTime == null)
                return;
            TotalFlights += 1;
            TotalFlightHours += endTime.Subtract(flyingStartTime.Value).TotalHours;
            flyingStartTime = null;
        }

        public void Parse(TextReader input)
        {
            ResetParser();

            string headerLine = input.ReadLine();
            if (headerLine == null)
            {
                InputEmpty = true;
                return;
            }


            List<string> header = new List<string>(headerLine.Split(','));
            int dateIndex = header.IndexOf("time");
            int controlIndex = header.IndexOf("control_mode");
            int armedIndex = header.IndexOf("is_armed");
            int altitudeIndex = header.IndexOf("altitude_raw");

            if (dateIndex == -1 || controlIndex == -1 || altitudeIndex == -1 || armedIndex == -1)
            {
                HeaderInvalid = true;
                return;
            }

            var maxIndex = new[] { dateIndex, controlIndex, armedIndex, altitudeIndex }.Max();

            DateTime time = DateTime.MinValue;
            double groundAltitude = double.NaN;

            while (true)
            {
                string line = input.ReadLine();
                if (line == null)
                    break;
                var lineSplit = line.Split(',');

                if (lineSplit.Length - 1 < maxIndex)
                    continue;

                // Set the ground altitude every time the machine is armed, in ArduPilot logs the altitude does not seem reliable until just before flight
                if (double.IsNaN(groundAltitude) && lineSplit[armedIndex].ToLower() == "true")
                    if (!double.TryParse(lineSplit[altitudeIndex], out groundAltitude))
                        continue;
                    else if (!double.IsNaN(groundAltitude) && lineSplit[armedIndex].ToLower() == "false")
                        groundAltitude = double.NaN;

                if (!DateTime.TryParse(lineSplit[dateIndex], out time))
                    continue;

                double altitude = double.NaN;

                // Flight time is not recording and armed (groundAltitude is a number)
                if (flyingStartTime == null && !double.IsNaN(groundAltitude))
                {
                    if (!double.TryParse(lineSplit[altitudeIndex], out altitude))
                        continue;
                    if (altitude > groundAltitude + 1)
                        flyingStartTime = time;
                }

                if (flyingStartTime != null)
                {
                    if (double.IsNaN(altitude) && !double.TryParse(lineSplit[altitudeIndex], out altitude))
                        continue;
                    // If unarmed (groundAltitude is NAN) or landed
                    if (double.IsNaN(groundAltitude) || altitude < groundAltitude + 1)
                    {
                        EndManualTime(time);
                        EndFlyingTime(time);
                    }
                    // If still flying
                    else
                    {
                        if (lineSplit[controlIndex].Length != 0)
                        {
                            if (manualFlightStartTime == null && lineSplit[controlIndex][0] == '0')
                                manualFlightStartTime = time;
                            else if (manualFlightStartTime != null && lineSplit[controlIndex][0] == '1')
                                EndManualTime(time);
                        }
                    }
                }
            }

            if (manualFlightStartTime != null)
                EndManualTime(time);
            if (flyingStartTime != null)
                EndFlyingTime(time);
        }
    }
}
