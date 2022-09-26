using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class FlightTimeParser
    {
        DateTime? manualFlightStartTime;
        DateTime? flyingStartTime;
        
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

            DateTime time = DateTime.MinValue;
            double? groundAltitude = null;

            while (true)
            {
                string line = input.ReadLine();
                if (line == null)
                    break;
                var lineSplit = line.Split(',');

                try
                {
                    // Set the ground altitude every time the machine is armed, in ArduPilot logs the altitude does not seem reliable until just before flight
                    if (groundAltitude == null && lineSplit[armedIndex] == "TRUE")
                        groundAltitude = double.Parse(lineSplit[altitudeIndex]);
                    else if (groundAltitude != null && lineSplit[armedIndex] == "FALSE")
                        groundAltitude = null;

                    if (!DateTime.TryParse(lineSplit[dateIndex], out time))
                        continue;

                    double altitude = double.NaN;

                    // Flight time is not recording and armed (groundAltitude non nulled)
                    if (flyingStartTime == null && groundAltitude != null)
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
                        // If unarmed (groundAltitude nulled) or landed
                        if (groundAltitude == null || altitude < groundAltitude + 1)
                        {
                            EndManualTime(time);
                            EndFlyingTime(time);
                        }
                        // If still flying
                        else
                        {
                            if (manualFlightStartTime == null && lineSplit[controlIndex] == "0")
                                manualFlightStartTime = time;
                            else if (manualFlightStartTime != null && lineSplit[controlIndex] == "1")
                                EndManualTime(time);
                        }
                    }
                }
                catch (IndexOutOfRangeException) { continue; }
            }

            if (manualFlightStartTime != null)
                EndManualTime(time);
            if (flyingStartTime != null)
                EndFlyingTime(time);
        }
    }
}
