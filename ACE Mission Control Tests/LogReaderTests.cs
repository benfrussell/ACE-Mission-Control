using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using Xunit;
using Moq;
using ACE_Mission_Control.Core.Models;

namespace ACE_Mission_Control_Tests
{
    public class LogReaderTests
    {
        string fullAutoLog = @"time,ground_speed,rc_link_quality,course,figure_direction_angle,control_mode,rc_latitude,roll,uplink_present,target_latitude,rangefinder_lat,rangefinder_dist,vertical_speed,target_longitude,takeoff_altitude,target_altitude_amsl,takeoff_longitude,figure_number_of_cycles,home_longitude,figure_alt_amsl,figure_center_lon,rangefinder_alt_raw,rangefinder_lon,satellite_count,rc_longitude,is_armed,home_latitude,figure_center_lat,longitude,heading,gps_fix,autopilot_status,gcs_link_quality,altitude_raw
2022-04-25T13:24:41.030,0,1,,,1,45.33528992,-0.4,TRUE,,,,0,,82.64556,,-75.93758818,,-75.93758818,,,,,16,-75.93769513,FALSE,45.33524765,,-75.93759444,154.1,2,0,1,0
2022-04-25T13:24:52.852,0,1,,,1,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759095,154.5,2,1,1,0
2022-04-25T13:24:53.460,0,1,,,1,45.3170755,0.9,TRUE,,,,1.5,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759037,154.6,2,1,1,15.8
2022-04-25T13:30:33.405,0,1,,,1,45.33530483,-0.6,TRUE,,,,-0.1,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.93768951,TRUE,45.33526513,,-75.93759479,138,2,1,1,1.1
2022-04-25T13:30:33.504,0.100000001,1,270,,1,45.33530483,-1.3,TRUE,,,,-0.2,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.93768951,TRUE,45.33526513,,-75.93759537,138,2,1,1,1
2022-04-25T13:30:42.516,0,1,,,1,45.3170755,-0.6,TRUE,,,,0,,82.64881,,-75.93760021,,-75.93759509,,,,,15,-75.9281863,FALSE,45.33526513,,-75.93760022,138.4,2,0,1,0
";

        string twoManual30sFlightsLog = @"time,ground_speed,rc_link_quality,course,figure_direction_angle,control_mode,rc_latitude,roll,uplink_present,target_latitude,rangefinder_lat,rangefinder_dist,vertical_speed,target_longitude,takeoff_altitude,target_altitude_amsl,takeoff_longitude,figure_number_of_cycles,home_longitude,figure_alt_amsl,figure_center_lon,rangefinder_alt_raw,rangefinder_lon,satellite_count,rc_longitude,is_armed,home_latitude,figure_center_lat,longitude,heading,gps_fix,autopilot_status,gcs_link_quality,altitude_raw
2022-04-25T13:24:28.000,0,1,,,0,45.33528992,-0.4,TRUE,,,,0,,82.64556,,-75.93758818,,-75.93758818,,,,,16,-75.93769513,FALSE,45.33524765,,-75.93759444,154.1,2,0,1,0
2022-04-25T13:24:29.000,0,1,,,0,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759095,154.5,2,1,1,0
2022-04-25T13:24:30.000,0,1,,,0,45.3170755,0.9,TRUE,,,,1.5,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759037,154.6,2,1,1,15.8
2022-04-25T13:25:00.000,0,1,,,0,45.33530483,-0.6,TRUE,,,,-0.1,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.93768951,TRUE,45.33526513,,-75.93759479,138,2,1,1,0
2022-04-25T13:25:28.000,0,1,,,0,45.3170755,-0.6,TRUE,,,,0,,82.64881,,-75.93760021,,-75.93759509,,,,,15,-75.9281863,TRUE,45.33526513,,-75.93760022,138.4,2,0,1,0
2022-04-25T13:25:29.000,0,1,,,0,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759095,154.5,2,1,1,0
2022-04-25T13:25:30.000,0,1,,,0,45.3170755,0.9,TRUE,,,,1.5,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759037,154.6,2,1,1,15.8
2022-04-25T13:26:00.000,0,1,,,0,45.33530483,-0.6,TRUE,,,,-0.1,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.93768951,TRUE,45.33526513,,-75.93759479,138,2,1,1,0
";

        string manualAutoManual30sEachFlightLog = @"time,ground_speed,rc_link_quality,course,figure_direction_angle,control_mode,rc_latitude,roll,uplink_present,target_latitude,rangefinder_lat,rangefinder_dist,vertical_speed,target_longitude,takeoff_altitude,target_altitude_amsl,takeoff_longitude,figure_number_of_cycles,home_longitude,figure_alt_amsl,figure_center_lon,rangefinder_alt_raw,rangefinder_lon,satellite_count,rc_longitude,is_armed,home_latitude,figure_center_lat,longitude,heading,gps_fix,autopilot_status,gcs_link_quality,altitude_raw
2022-04-25T13:24:29.000,0,1,,,0,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759095,154.5,2,1,1,0
2022-04-25T13:24:30.000,0,1,,,0,45.3170755,0.9,TRUE,,,,1.5,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759037,154.6,2,1,1,2
2022-04-25T13:25:00.000,0,1,,,1,45.33530483,-0.6,TRUE,,,,-0.1,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.93768951,TRUE,45.33526513,,-75.93759479,138,2,1,1,15
2022-04-25T13:25:30.000,0,1,,,0,45.3170755,-0.6,TRUE,,,,0,,82.64881,,-75.93760021,,-75.93759509,,,,,15,-75.9281863,TRUE,45.33526513,,-75.93760022,138.4,2,0,1,2
2022-04-25T13:26:00.000,0,1,,,0,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,FALSE,45.33526513,,-75.93759095,154.5,2,1,1,0
";

        string manual1mWithMissingDataFlightLog = @"time,ground_speed,rc_link_quality,course,figure_direction_angle,control_mode,rc_latitude,roll,uplink_present,target_latitude,rangefinder_lat,rangefinder_dist,vertical_speed,target_longitude,takeoff_altitude,target_altitude_amsl,takeoff_longitude,figure_number_of_cycles,home_longitude,figure_alt_amsl,figure_center_lon,rangefinder_alt_raw,rangefinder_lon,satellite_count,rc_longitude,is_armed,home_latitude,figure_center_lat,longitude,heading,gps_fix,autopilot_status,gcs_link_quality,altitude_raw
2022-04-25T13:24:00.000,0,1,,,0,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759095,154.5,2,1,1,0
2022-04-25T13:25:00.000,0,1,,,0,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759095,154.5,2,1,1,15
2022-04-25T13:25:30.000,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
2022-04-25T13:26:00.000,0,1,,,0,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,FALSE,45.33526513,,-75.93759095,154.5,2,1,1,0";

        string altitudeMissingFlightLog = @"time,ground_speed,rc_link_quality,course,figure_direction_angle,control_mode,rc_latitude,roll,uplink_present,target_latitude,rangefinder_lat,rangefinder_dist,vertical_speed,target_longitude,takeoff_altitude,target_altitude_amsl,takeoff_longitude,figure_number_of_cycles,home_longitude,figure_alt_amsl,figure_center_lon,rangefinder_alt_raw,rangefinder_lon,satellite_count,rc_longitude,is_armed,home_latitude,figure_center_lat,longitude,heading,gps_fix,autopilot_status,gcs_link_quality
2022-04-25T13:24:00.000,0,1,,,0,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759095,154.5,2,1,1
2022-04-25T13:26:00.000,0,1,,,0,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,FALSE,45.33526513,,-75.93759095,154.5,2,1,1";

        string manual1mFlightLog = @"time,ground_speed,rc_link_quality,course,figure_direction_angle,control_mode,rc_latitude,roll,uplink_present,target_latitude,rangefinder_lat,rangefinder_dist,vertical_speed,target_longitude,takeoff_altitude,target_altitude_amsl,takeoff_longitude,figure_number_of_cycles,home_longitude,figure_alt_amsl,figure_center_lon,rangefinder_alt_raw,rangefinder_lon,satellite_count,rc_longitude,is_armed,home_latitude,figure_center_lat,longitude,heading,gps_fix,autopilot_status,gcs_link_quality,altitude_raw
2022-04-25T13:25:59.000,0,1,,,0,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759095,154.5,2,1,1,0
2022-04-25T13:26:00.000,0,1,,,0,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759095,154.5,2,1,1,10
2022-04-25T13:27:00.000,0,1,,,0,45.3170755,0.3,TRUE,,,,0,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,FALSE,45.33526513,,-75.93759095,154.5,2,1,1,0";

        string auto1mFlightLog = @"time,ground_speed,rc_link_quality,course,figure_direction_angle,control_mode,rc_latitude,roll,uplink_present,target_latitude,rangefinder_lat,rangefinder_dist,vertical_speed,target_longitude,takeoff_altitude,target_altitude_amsl,takeoff_longitude,figure_number_of_cycles,home_longitude,figure_alt_amsl,figure_center_lon,rangefinder_alt_raw,rangefinder_lon,satellite_count,rc_longitude,is_armed,home_latitude,figure_center_lat,longitude,heading,gps_fix,autopilot_status,gcs_link_quality,altitude_raw
2022-04-25T13:25:59.000,0,1,,,1,45.3170755,0.3,TRUE,,,,1,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759095,154.5,2,1,1,0
2022-04-25T13:26:00.000,0,1,,,1,45.3170755,0.3,TRUE,,,,1,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,TRUE,45.33526513,,-75.93759095,154.5,2,1,1,10
2022-04-25T13:27:00.000,0,1,,,1,45.3170755,0.3,TRUE,,,,1,,82.64881,,-75.93758818,,-75.93759509,,,,,16,-75.9281863,FALSE,45.33526513,,-75.93759095,154.5,2,1,1,0";

        [Fact]
        public void FlightTimeParser_Sums_Zero_Manual_In_Full_Auto_Log()
        {
            var parser = new FlightTimeParser();
            parser.Parse(new StringReader(fullAutoLog));
            Assert.Equal(0, parser.ManualFlightHours);
        }

        [Fact]
        public void FlightTimeParser_Sums_Zero_Auto_In_Full_Manual_Log()
        {
            var parser = new FlightTimeParser();
            parser.Parse(new StringReader(twoManual30sFlightsLog));
            var autoTime = parser.TotalFlightHours - parser.ManualFlightHours;
            Assert.Equal(0, autoTime);
        }

        [Fact]
        public void FlightTimeParser_Sums_Two_30s_Manual_Flights_In_Full_Manual_Log()
        {
            var parser = new FlightTimeParser();
            parser.Parse(new StringReader(twoManual30sFlightsLog));
            Assert.Equal(1d / 60d, parser.ManualFlightHours, 5);
        }

        [Fact]
        public void FlightTimeParser_Sums_Two_30s_Manual_Tracks_In_Mixed_Log()
        {
            var parser = new FlightTimeParser();
            parser.Parse(new StringReader(manualAutoManual30sEachFlightLog));
            Assert.Equal(1d / 60d, parser.ManualFlightHours, 5);
        }

        [Fact]
        public void FlightTimeParser_Sums_Three_30s_Tracks_In_Mixed_Log()
        {
            var parser = new FlightTimeParser();
            parser.Parse(new StringReader(manualAutoManual30sEachFlightLog));
            Assert.Equal(1.5d / 60d, parser.TotalFlightHours, 5);
        }

        [Fact]
        public void FlightTimeParser_Sums_One_30s_Auto_Track_In_Mixed_Log()
        {
            var parser = new FlightTimeParser();
            parser.Parse(new StringReader(manualAutoManual30sEachFlightLog));
            var autoTime = parser.TotalFlightHours - parser.ManualFlightHours;
            Assert.Equal(0.5d / 60d, autoTime, 5);
        }

        [Fact]
        public void FlightTimeParser_Sums_One_1m_Flight_With_Missing_Data()
        {
            var parser = new FlightTimeParser();
            parser.Parse(new StringReader(manual1mWithMissingDataFlightLog));
            Assert.Equal(1d / 60d, parser.TotalFlightHours, 5);
        }

        [Fact]
        public void FlightTimeParser_InputEmpty_With_Empty_Input()
        {
            var parser = new FlightTimeParser();
            parser.Parse(new StringReader(""));
            Assert.True(parser.InputEmpty);
        }

        [Fact]
        public void FlightTimeParser_HeaderInvalid_With_Altitude_Missing()
        {
            var parser = new FlightTimeParser();
            parser.Parse(new StringReader(altitudeMissingFlightLog));
            Assert.True(parser.HeaderInvalid);
        }

        [Fact]
        public void LogReader_Read_Adds_New_Entry()
        {
            var logReader = new LogReader();
            logReader.Read(DateTime.MinValue, "TestPilot", "TestMachine", new StringReader(""));
            Assert.Single(logReader.Entries);
        }

        [Fact]
        public void LogReader_Read_Merges_Entries()
        {
            var logReader = new LogReader();
            logReader.Read(DateTime.MinValue, "TestPilot", "TestMachine", new StringReader(""));
            logReader.Read(DateTime.MinValue, "TestPilot", "TestMachine", new StringReader(""));
            Assert.Single(logReader.Entries);
        }

        [Fact]
        public void LogReader_Sorts_Newest_To_Oldest()
        {
            var logReader = new LogReader();
            logReader.Read(DateTime.MinValue, "OldPilot", "TestMachine", new StringReader(""));
            logReader.Read(DateTime.MaxValue, "NewPilot", "TestMachine", new StringReader(""));
            logReader.SortAndRecalculateEntries();
            Assert.Equal("NewPilot", logReader.Entries.First().Pilot);
        }

        private LogReader CreateLogReader()
        {
            var logReader = new LogReader();
            // Each machine has 4m of flight
            // Each pilot has 4m of flight, 2m total with each machine, 1m manual with each machine
            // Each pilot has 2m of manual flight
            logReader.Read(new DateTime(2022, 09, 23), "Pilot1", "Machine1", new StringReader(auto1mFlightLog));
            logReader.Read(new DateTime(2022, 09, 24), "Pilot1", "Machine1", new StringReader(manual1mFlightLog));
            logReader.Read(new DateTime(2022, 09, 25), "Pilot2", "Machine1", new StringReader(auto1mFlightLog));
            logReader.Read(new DateTime(2022, 09, 26), "Pilot2", "Machine1", new StringReader(manual1mFlightLog));
            logReader.Read(new DateTime(2022, 09, 27), "Pilot1", "Machine2", new StringReader(auto1mFlightLog));
            logReader.Read(new DateTime(2022, 09, 28), "Pilot1", "Machine2", new StringReader(manual1mFlightLog));
            logReader.Read(new DateTime(2022, 09, 29), "Pilot2", "Machine2", new StringReader(auto1mFlightLog));
            logReader.Read(new DateTime(2022, 09, 30), "Pilot2", "Machine2", new StringReader(manual1mFlightLog));
            return logReader;
        }

        [Fact]
        public void LogReader_Top_Entry_Sums_All_Machine_Flight_Hours()
        {
            var logReader = CreateLogReader();
            logReader.SortAndRecalculateEntries();
            Assert.Equal(4d / 60d, logReader.Entries.First().MachineFlightHoursToDate, 5);
        }

        [Fact]
        public void LogReader_Top_Entry_Sums_All_Pilot_Flight_Hours()
        {
            var logReader = CreateLogReader();
            logReader.SortAndRecalculateEntries();
            Assert.Equal(4d / 60d, logReader.Entries.First().PilotFlightHoursAllMachinesToDate, 5);
        }

        [Fact]
        public void LogReader_Top_Entry_Sums_All_Pilot_Manual_Hours()
        {
            var logReader = CreateLogReader();
            logReader.SortAndRecalculateEntries();
            Assert.Equal(2d / 60d, logReader.Entries.First().PilotManualHoursAllMachinesToDate, 5);
        }

        [Fact]
        public void LogReader_Top_Entry_Sums_All_Pilot_Machine_Flight_Hours()
        {
            var logReader = CreateLogReader();
            logReader.SortAndRecalculateEntries();
            Assert.Equal(2d / 60d, logReader.Entries.First().PilotFlightHoursThisMachineToDate, 5);
        }

        [Fact]
        public void LogReader_Top_Entry_Sums_All_Pilot_Machine_Manual_Hours()
        {
            var logReader = CreateLogReader();
            logReader.SortAndRecalculateEntries();
            Assert.Equal(1d / 60d, logReader.Entries.First().PilotManualHoursThisMachineToDate, 5);
        }

        [Fact]
        public void LogReader_Bottom_Entry_Does_Not_Sum_All_Machine_Flight_Hours()
        {
            var logReader = CreateLogReader();
            logReader.SortAndRecalculateEntries();
            Assert.Equal(1d / 60d, logReader.Entries.Last().MachineFlightHoursToDate, 5);
        }

        [Fact]
        public void LogReader_Bottom_Entry_Does_Not_Sum_All_Pilot_Flight_Hours()
        {
            var logReader = CreateLogReader();
            logReader.SortAndRecalculateEntries();
            Assert.Equal(1d / 60d, logReader.Entries.Last().PilotFlightHoursAllMachinesToDate, 5);
        }

        [Fact]
        public void LogReader_Bottom_Entry_Does_Not_Sum_All_Pilot_Machine_Flight_Hours()
        {
            var logReader = CreateLogReader();
            logReader.SortAndRecalculateEntries();
            Assert.Equal(1d / 60d, logReader.Entries.Last().PilotFlightHoursThisMachineToDate, 5);
        }
    }
}
