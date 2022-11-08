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

namespace ACE_Drone_Dashboard_Service
{
    public class DashboardService
    {
        public ServiceStatus Status { get; private set; }

        readonly ILogger<Worker> logger;
        SqlConnectionStringBuilder cxnBuilder;
        SqlConnection? sqlCxn;
        System.Timers.Timer sqlUpdateTimer;

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
            sqlUpdateTimer.Elapsed += VehicleListRequestTimer_Elapsed;
        }

        private void UGCSClient_ReceivedVehicleListEvent(object? sender, ReceivedVehicleListEventArgs e)
        {
            throw new NotImplementedException();
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

            SetStatus(ServiceStatus.Running);
        }

        private void VehicleListRequestTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (UGCSClient.IsConnected)
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
