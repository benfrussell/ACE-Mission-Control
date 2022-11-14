using ACE_Mission_Control.Core.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using UGCS.Sdk.Protocol.Merging;
using System.Timers;
using UGCS.Sdk.Protocol.Encoding;
using System.Reflection.Metadata.Ecma335;
using System.Collections;

namespace ACE_Drone_Dashboard_Service
{
    public class VehicleTelemetry
    {
        public string Name { get; set; }
        public string Model { get; private set; }
        public bool IsReal { get; private set; }
        public int ID { get; set; }
        public float ArmedAltitude { get; private set; }
        public DateTime LastTelemetryUpdate { get; private set; }
        // Latitude and longitude in degrees
        public double Longitude 
        { 
            get => longitude;
            set 
            {
                if (longitude != value)
                {
                    longitude = value;
                    LastTelemetryUpdate = DateTime.Now;
                }
            }
        }
        public double Latitude
        {
            get => latitude;
            set
            {
                if (latitude != value)
                {
                    latitude = value;
                    LastTelemetryUpdate = DateTime.Now;
                }
            }
        }
        public float GroundSpeed
        {
            get => groundSpeed;
            set
            {
                if (groundSpeed != value)
                {
                    groundSpeed = value;
                    LastTelemetryUpdate = DateTime.Now;
                }
            }
        }
        // Course in degrees
        public float Course
        {
            get => course;
            set
            {
                if (course != value)
                {
                    course = value;
                    LastTelemetryUpdate = DateTime.Now;
                }
            }
        }
        public string FlightState
        {
            get
            {
                if (IsArmed && Altitude > ArmedAltitude + 1)
                    return "IN_AIR";
                else if (IsArmed)
                    return "ON_GROUND";
                return "STOPPED";
            }
        }
        public float Altitude
        {
            get { return altitude; }
            set
            {
                if (altitude != value)
                {
                    altitude = value;
                    LastTelemetryUpdate = DateTime.Now;
                }

                if (resetArmedAltitude)
                {
                    ArmedAltitude = value;
                    resetArmedAltitude = false;
                }
            }
        }
        public bool IsArmed
        {
            get { return isArmed; }
            set
            {
                if (isArmed != value)
                {
                    isArmed = value;
                    LastTelemetryUpdate = DateTime.Now;
                }

                if (isArmed == false && value == true)
                    resetArmedAltitude = true;
            }
        }

        private bool resetArmedAltitude = false;
        private double longitude;
        private double latitude;
        private float groundSpeed;
        private float course;
        private float altitude;
        private bool isArmed;

        public VehicleTelemetry(string name, int id, bool isReal)
        {
            Name = name;
            Model = name.Split(' ', '-')[0];
            ID = id;
            IsReal = isReal;
        }
    }

    public class DashboardService
    {
        public ServiceStatus Status { get; private set; }
        public bool Halted { get; private set; }

        Dictionary<int, VehicleTelemetry> telemetry;

        readonly ILogger<WindowsBackgroundService> logger;
        SqlConnectionStringBuilder cxnBuilder;
        SqlConnection? sqlCxn;
        System.Timers.Timer sqlUpdateTimer;
        DateTime? lastSQLUpdate;
        bool requestedVehicleListUpdate;

        public DashboardService(ILogger<WindowsBackgroundService> logger)
        {
            Status = ServiceStatus.NotRunning;

            cxnBuilder = new SqlConnectionStringBuilder();
            cxnBuilder.DataSource = "10.1.1.85";
            cxnBuilder.IntegratedSecurity = true;
            cxnBuilder.InitialCatalog = "GDGArcGIS";
            cxnBuilder.ConnectTimeout = 5;
            cxnBuilder.TrustServerCertificate = true;

            this.logger = logger;

            UGCSClient.ReceivedVehicleListEvent += UGCSClient_ReceivedVehicleListEvent;
            UGCSClient.StaticPropertyChanged += UGCSClient_StaticPropertyChanged;

            sqlUpdateTimer = new System.Timers.Timer();
            sqlUpdateTimer.AutoReset = false;
            sqlUpdateTimer.Interval = 3000;
            sqlUpdateTimer.Elapsed += SQLUpdateTimer_Elapsed;

            telemetry = new Dictionary<int, VehicleTelemetry>();
            requestedVehicleListUpdate = false;
        }

        public void StartSQLUpdates()
        {
            if (!sqlUpdateTimer.Enabled)
                sqlUpdateTimer.Start();
        }

        public bool IsConnectionUp()
        {
            if (!UGCSClient.IsConnected || !IsDBConnectionUp())
                return false;
            if (Status != ServiceStatus.Running)
                SetStatus(ServiceStatus.Running);
            return true;
        }

        public void Halt(ServiceStatus haltedStatus)
        {
            SetStatus(haltedStatus);

            if (Halted)
                return;

            Halted = true;
            if (sqlCxn != null && sqlCxn.State == System.Data.ConnectionState.Open)
            {
                sqlCxn.Close();
                sqlCxn.Dispose();
            }
                
            if (UGCSClient.IsConnected)
                UGCSClient.Disconnect();

            logger.LogInformation("Service halted");
        }

        public void Resume()
        {
            if (!Halted)
                return;

            Halted = false;
            logger.LogInformation("Service resumed");
        }

        public async void Connect(CancellationToken stoppingToken)
        {
            if (Halted)
                throw new InvalidOperationException("Attempting to connect while the service is halted is an invalid operation.");

            if (!UGCSClient.TryingConnections)
            {
                UGCSClient.StartTryingConnections();
                SetStatus(ServiceStatus.Starting);
                return;
            }
                
            if (!UGCSClient.IsConnected)
            {
                SetStatus(ServiceStatus.RunningNoUgCSConnection);
                return;
            }

            var dbReachable = await DatabaseReachable(stoppingToken);
            // We have to check after awaiting to see if the service was halted while we were gone
            if (Halted)
                return;
            
            if (!dbReachable)
            {
                SetStatus(ServiceStatus.RunningDatabaseNotReachable);
                return;
            }

            if (sqlCxn != null)
            {
                sqlCxn.Close();
                sqlCxn.Dispose();
            }
                

            sqlCxn = await EstablishDBConnection(stoppingToken);
            if (Halted)
                return;

            if (sqlCxn == null)
            {
                if (!Halted)
                    Halt(ServiceStatus.HaltedDatabaseConnectionRefused);
                return;
            }

            var realVehicles = telemetry.Where((entry) => entry.Value.IsReal == true).Select((entry) => entry.Value);
            if (realVehicles.Count() > 0)
                EnsureTableHasDroneRows(realVehicles);

            if (sqlCxn.State == System.Data.ConnectionState.Open)
            {
                StartSQLUpdates();
                SetStatus(ServiceStatus.Running);
            }
        }

        private bool IsDBConnectionUp()
        {
            return sqlCxn != null && sqlCxn.State == System.Data.ConnectionState.Open;
        }

        private void SQLUpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (!IsDBConnectionUp())
                return;

            UpdateDroneTable();
            sqlUpdateTimer.Start();
        }

        private void UGCSClient_StaticPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsConnected" && UGCSClient.IsConnected)
            {
                if (!UGCSClient.SubscribedToTelemetry)
                    UGCSClient.StartTelemetrySubscription(TelemetryNotificationHandler);
                RequestVehicleListUpdate();
            }
        }

        private void UGCSClient_ReceivedVehicleListEvent(object? sender, ReceivedVehicleListEventArgs e)
        {
            foreach (var vehicle in e.Vehicles)
            {
                if (telemetry.ContainsKey(vehicle.Id))
                {
                    telemetry[vehicle.Id].Name = vehicle.Name;
                }
                else
                {
                    telemetry[vehicle.Id] = new VehicleTelemetry(vehicle.Name, vehicle.Id, vehicle.IsReal);
                    if (vehicle.IsReal && IsDBConnectionUp())
                        EnsureTableHasDroneRows(new[] { telemetry[vehicle.Id] });
                }
            }
        }

        private void UpdateDroneTable()
        {
            foreach (var vehicle in telemetry.Values)
            {
                // Skip updating this vehicle if the drone isn't real 
                if (!vehicle.IsReal)
                    continue;
                // or if the last telemetry received is older than the last time we updated
                if (lastSQLUpdate != null && vehicle.LastTelemetryUpdate < lastSQLUpdate)
                    continue;

                bool success = ExecuteSQLQuery(
$@"UPDATE sde.DRONE
    SET
    FlightState = '{vehicle.FlightState}',
    Symbology = '{vehicle.Model}_{vehicle.FlightState}',
    Longitude = {vehicle.Longitude},
    Latitude = {vehicle.Latitude},
    Altitude = {vehicle.Altitude},
    Course = {vehicle.Course},
    GroundSpeed = {vehicle.GroundSpeed},
    Shape = geometry::STGeomFromText('POINT({vehicle.Longitude} {vehicle.Latitude})', 4326)),
    ladate = SYSDATETIME()
WHERE Name = '{vehicle.Name}'");

                if (!success)
                    break;

                lastSQLUpdate = DateTime.Now;
            }
        }

        private void EnsureTableHasDroneRows(IEnumerable<VehicleTelemetry> vehicles)
        {
            foreach (var vehicle in vehicles)
            {
                bool success = ExecuteSQLQuery(
$@"BEGIN
    IF NOT EXISTS (SELECT * FROM sde.DRONE WHERE Name = '{vehicle.Name}')
    BEGIN
        INSERT INTO sde.DRONE (OBJECTID, Name, Drone_model, GlobalID)
        VALUES ((SELECT COUNT(1) + 1 FROM sde.DRONE), '{vehicle.Name}', '{vehicle.Model}', NEWID())
    END
END");
                if (!success)
                    break;
            }
        }

        private bool ExecuteSQLQuery(string query)
        {
            try
            {
                var command = new SqlCommand(query, sqlCxn);
                command.ExecuteReader();
            }
            catch (SqlException e)
            {
                // Permission error
                if (e.Number == 229)
                {
                    Halt(ServiceStatus.HaltedDatabasePermissionDenied);
                    return false;
                }

                if (e.Class >= 20)
                {
                    logger.LogInformation("SQL Connection closed due to a severe error: {e}", e.Message);
                    Halt(ServiceStatus.HaltedDatabaseSevereError);
                    return false;
                }
                throw;
            }
            return true;
        }

        private void TelemetryNotificationHandler(Notification notification)
        {
            foreach (Telemetry t in notification.Event.TelemetryEvent.Telemetry)
            {
                var vehicleID = notification.Event.TelemetryEvent.Vehicle.Id;
                if (!telemetry.ContainsKey(vehicleID))
                {
                    RequestVehicleListUpdate();
                    continue;
                }

                if (t.Value == null)
                    continue;

                switch (t.TelemetryField.Code)
                {
                    case "longitude":
                        telemetry[vehicleID].Longitude = t.Value.DoubleValue * (180/Math.PI);
                        break;
                    case "latitude":
                        telemetry[vehicleID].Latitude = t.Value.DoubleValue * (180 / Math.PI);
                        break;
                    case "altitude_raw":
                        telemetry[vehicleID].Altitude = t.Value.FloatValue;
                        break;
                    case "is_armed":
                        telemetry[vehicleID].IsArmed = t.Value.BoolValue;
                        break;
                    case "course":
                        telemetry[vehicleID].Course = (float)(t.Value.FloatValue * (180 / Math.PI));
                        break;
                    case "ground_speed":
                        telemetry[vehicleID].GroundSpeed = t.Value.FloatValue;
                        break;
                    default:
                        break;
                }
            }
        }

        private void RequestVehicleListUpdate()
        {
            if (requestedVehicleListUpdate)
                return;
            requestedVehicleListUpdate = true;
            UGCSClient.RequestVehicleList();
        }

        private Task<bool> DatabaseReachable(CancellationToken stoppingToken)
        {
            return Task.Run(() =>
            {
                using (var pinger = new Ping())
                    return pinger.Send(cxnBuilder.DataSource, 3000).Status == IPStatus.Success;
            }, stoppingToken);
        }

        private Task<SqlConnection?> EstablishDBConnection(CancellationToken stoppingToken)
        {
            var sqlCxn = new SqlConnection(cxnBuilder.ConnectionString);
            return Task.Run(() =>
            {
                try
                {
                    sqlCxn.Open();
                }
                catch (SqlException e)
                {
                    foreach (var error in e.Errors)
                        logger.LogInformation("SQL Connection failed with error: {e}", e.Message);
                }

                if (sqlCxn.State == System.Data.ConnectionState.Open)
                    return sqlCxn;

                sqlCxn.Dispose();
                return null;
            }, stoppingToken);
        }

        private void SetStatus(ServiceStatus newstatus)
        {
            if (Status != newstatus)
                logger.LogInformation("Service status is now '{status}'", newstatus.ToString());
            Status = newstatus;
        }
    }
}
