using ACE_Mission_Control.Core.Models;
using Microsoft.Data.SqlClient;
using System.Net.NetworkInformation;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace ACE_Drone_Dashboard_Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        ResponseServer server;
        ServiceStatus status;
        SqlConnectionStringBuilder cxnBuilder;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            server = new ResponseServer();
            status = ServiceStatus.NotRunning;

            cxnBuilder = new SqlConnectionStringBuilder();
            cxnBuilder.DataSource = "10.1.1.85";
            cxnBuilder.IntegratedSecurity = true;
            cxnBuilder.InitialCatalog = "GDGArcGIS";
            cxnBuilder.ConnectTimeout = 5;
            cxnBuilder.TrustServerCertificate = true;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            SetStatus(ServiceStatus.Starting);

            await server.StartAsync("tcp://localhost:5538", HandleServerRequest, HandleServerFailure);
            if (server.Status == ServerStatus.Failed)
                return;

            UGCSClient.StartTryingConnections();

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!UGCSClient.IsConnected)
                    SetStatus(ServiceStatus.RunningNoUgCSConnection);

                while (UGCSClient.IsConnected)
                {
                    if (!await DatabaseReachable(stoppingToken))
                    {
                        SetStatus(ServiceStatus.RunningDatabaseNotReachable);
                        await Task.Delay(3000, stoppingToken);
                        continue;
                    }

                    var sqlCxn = await EstablishDBConnection(stoppingToken);
                    if (sqlCxn == null)
                    {
                        SetStatus(ServiceStatus.RunningDatabaseConnectionRefused);
                        await Task.Delay(3000, stoppingToken);
                        continue;
                    }

                    SetStatus(ServiceStatus.Running);

                    await Task.Delay(3000, stoppingToken);
                }
                
                await Task.Delay(1000, stoppingToken);
            }
        }

        private Task<bool> DatabaseReachable(CancellationToken stoppingToken)
        {
            var pinger = new Ping();
            return Task.Run(() =>
            {
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
                        _logger.LogInformation("SQL Connection failed with error: {e}", e.Message);
                }

                if (sqlCxn.State == System.Data.ConnectionState.Open)
                    return sqlCxn;

                sqlCxn.Dispose();
                return null;
            }, stoppingToken);
        }

        private void SetStatus(ServiceStatus newstatus)
        {
            if (status != newstatus)
                _logger.LogInformation("Service status is now '{status}'", newstatus.ToString());
            status = newstatus;
        }

        private string HandleServerRequest(string request)
        {
            switch (request)
            {
                case "ping":
                    return "pong";
                case "status":
                    return ((int)status).ToString();
                default:
                    return "Unknown request";
            }
        }

        private void HandleServerFailure(Exception e)
        {
            _logger.LogError(e, "ACE Drone Dashboard Service will not run because the service's Response Server failed to run.");
        }
    }
}