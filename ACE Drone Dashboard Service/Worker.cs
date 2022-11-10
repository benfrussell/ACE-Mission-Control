using ACE_Mission_Control.Core.Models;
using Microsoft.Data.SqlClient;
using System.Net.NetworkInformation;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace ACE_Drone_Dashboard_Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> logger;

        ResponseServer server;
        DashboardService service;

        public Worker(ILogger<Worker> logger)
        {
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            server = new ResponseServer();
            service = new DashboardService(logger);

            await server.StartAsync("tcp://localhost:5538", HandleServerRequest, HandleServerFailure);
            if (server.Status == ServerStatus.Failed)
                return;

            while (!stoppingToken.IsCancellationRequested)
            {
                // Try to start the service connection if it's not running
                // If we had permission denied or severe error then don't try connecting again
                if (!service.IsConnectionUp())
                {
                    if (service.Status != ServiceStatus.StoppedDatabasePermissionDenied && service.Status != ServiceStatus.StoppedDatabaseSevereError)
                        service.Connect(stoppingToken);
                }
                    
                await Task.Delay(3000, stoppingToken);
            }
        }

        private string HandleServerRequest(string request)
        {
            switch (request)
            {
                case "ping":
                    return "pong";
                case "status":
                    return ((int)service.Status).ToString();
                default:
                    return "Unknown request";
            }
        }

        private void HandleServerFailure(Exception e)
        {
            logger.LogError(e, "ACE Drone Dashboard Service will not run because the service's Response Server failed to run.");
        }
    }
}