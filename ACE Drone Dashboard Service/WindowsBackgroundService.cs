using ACE_Mission_Control.Core.Models;
using Microsoft.Data.SqlClient;
using System.Net.NetworkInformation;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace ACE_Drone_Dashboard_Service
{
    public class WindowsBackgroundService : BackgroundService
    {
        private readonly ILogger<WindowsBackgroundService> logger;

        private readonly ResponseServer server;
        private readonly DashboardService service;

        public WindowsBackgroundService(ResponseServer server, DashboardService service, ILogger<WindowsBackgroundService> logger)
        {
            this.logger = logger;
            this.server = new ResponseServer();
            this.service = new DashboardService(logger);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await server.StartAsync("tcp://localhost:5538", HandleServerRequest, HandleServerFailure);
                if (server.Status == ServerStatus.Failed)
                    return;

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Try to start the service connection if it's not running
                    // If we had permission denied or severe error then don't try connecting again
                    if (!service.IsConnectionUp() && !service.Halted)
                        service.Connect(stoppingToken);

                    await Task.Delay(3000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Message}", ex.Message);
                Environment.Exit(1);
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
                case "halt":
                    if (service.Halted)
                        return "Service is already halted";
                    service.Halt(ServiceStatus.HaltedByRequest);
                    return "Halting service";
                case "resume":
                    if (!service.Halted)
                        return "Service is already running";
                    service.Resume();
                    return "Resuming service";
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