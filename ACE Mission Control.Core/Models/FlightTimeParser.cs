using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class FlightTimeParser
    {
        DateTime? manualStartTime;
        DateTime? armedStartTime;
        
        public double TotalManualHours { get; private set; }
        public double TotalArmedHours { get; private set; }

        public FlightTimeParser() 
        {
            ResetParser();
        }

        private void ResetParser()
        {
            manualStartTime = null;
            armedStartTime = null;
            TotalManualHours = 0;
            TotalArmedHours = 0;
        }

        private void EndManualTime(DateTime? endTime)
        {
            if (manualStartTime == null)
                return;
            if (endTime != null)
                TotalManualHours += endTime.Value.Subtract(manualStartTime.Value).TotalHours;
            manualStartTime = null;
        }

        private void EndArmedTime(DateTime? endTime)
        {
            if (armedStartTime == null || endTime == null)
                return;
            if (endTime != null)
                TotalArmedHours += endTime.Value.Subtract(armedStartTime.Value).TotalHours;
            armedStartTime = null;
        }

        public void Parse(TextReader input)
        {
            ResetParser();

            string headerLine = input.ReadLine();
            if (headerLine == null)
                return;

            List<string> header = new List<string>(headerLine.Split(','));
            int dateIndex = header.IndexOf("time");
            int autoIndex = header.IndexOf("autopilot_status");
            int armedIndex = header.IndexOf("is_armed");

            if (dateIndex == -1 || autoIndex == -1 || armedIndex == -1)
                return;

            DateTime? time = null;

            while (true)
            {
                string line = input.ReadLine();
                if (line == null)
                    break;
                var lineSplit = line.Split(',');
                time = DateTime.Parse(lineSplit[dateIndex]);

                if (armedStartTime == null && lineSplit[armedIndex] == "TRUE")
                    armedStartTime = time;

                if (armedStartTime != null)
                {
                    if (lineSplit[armedIndex] == "FALSE")
                    {
                        EndManualTime(time);
                        EndArmedTime(time);
                    }

                    if (manualStartTime == null && lineSplit[autoIndex] == "0")
                        manualStartTime = time;
                    else if (manualStartTime != null && lineSplit[autoIndex] == "1")
                        EndManualTime(time);
                }
            }

            EndManualTime(time);
            EndArmedTime(time);
        }
    }
}
