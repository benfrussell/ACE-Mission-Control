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

namespace ACE_Drone_Dashboard_Service
{
    public class VehicleTelemetry
    {
        public string Name { get; set; }
        public int ID { get; set; }
        public string FlightState { get; set; }
        public string MissionStage { get; set; }
        public long Longitude { get; set; }
        public long Latitude { get; set; }
        public long Altitude { get; set; }
        public long VelX { get; set; }
        public long VelY { get; set; }
        public long VelZ { get; set; }
    }

    public class DashboardService
    {
        public ServiceStatus Status { get; private set; }

        Dictionary<int, VehicleTelemetry> telemetry;

        readonly ILogger<Worker> logger;
        SqlConnectionStringBuilder cxnBuilder;
        SqlConnection? sqlCxn;
        System.Timers.Timer sqlUpdateTimer;
        bool requestedVehicleListUpdate;

        public DashboardService(ILogger<Worker> logger)
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

            sqlUpdateTimer = new System.Timers.Timer();
            sqlUpdateTimer.AutoReset = false;
            sqlUpdateTimer.Interval = 3000;
            sqlUpdateTimer.Elapsed += SQLUpdateTimer_Elapsed;

            telemetry = new Dictionary<int, VehicleTelemetry>();
            requestedVehicleListUpdate = false;
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
                    telemetry[vehicle.Id] = new VehicleTelemetry();
                    telemetry[vehicle.Id].Name = vehicle.Name;
                    telemetry[vehicle.Id].ID = vehicle.Id;
                }
            }
        }

        public void StartSQLUpdates()
        {
            if (!sqlUpdateTimer.Enabled)
                sqlUpdateTimer.Start();
        }

        public bool IsConnectionUp()
        {
            if (!UGCSClient.IsConnected || sqlCxn == null || sqlCxn.State != System.Data.ConnectionState.Open)
                return false;
            return true;
        }

        public async void Connect(CancellationToken stoppingToken)
        {
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

            if (!await DatabaseReachable(stoppingToken))
            {
                SetStatus(ServiceStatus.RunningDatabaseNotReachable);
                return;
            }

            if (sqlCxn != null)
                sqlCxn.Dispose();

            sqlCxn = await EstablishDBConnection(stoppingToken);
            if (sqlCxn == null)
            {
                SetStatus(ServiceStatus.RunningDatabaseConnectionRefused);
                return;
            }

            if (!UGCSClient.SubscribedToTelemetry)
                UGCSClient.StartTelemetrySubscription(TelemetryNotificationHandler);
            RequestVehicleListUpdate();

            SetStatus(ServiceStatus.Running);
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

                switch (t.TelemetryField.Code)
                {
                    case "longitude":
                        telemetry[vehicleID].Longitude = t.Value.LongValue;
                        break;
                    case "latitude":
                        telemetry[vehicleID].Latitude = t.Value.LongValue;
                        break;
                    case "altitude_raw":
                        telemetry[vehicleID].Altitude = t.Value.LongValue;
                        break;
                    case "ground_speed_x":
                        telemetry[vehicleID].VelX = t.Value.LongValue;
                        break;
                    case "ground_speed_y":
                        telemetry[vehicleID].VelY = t.Value.LongValue;
                        break;
                    case "vertical_speed":
                        telemetry[vehicleID].VelZ = t.Value.LongValue;
                        break;
                    default:
                        logger.LogInformation("Vehicle id: {0} Code: {1} Semantic {2} Subsystem {3}", notification.Event.TelemetryEvent.Vehicle.Id, t.TelemetryField.Code, t.TelemetryField.Semantic, t.TelemetryField.Subsystem);
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

        private void SQLUpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            
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
