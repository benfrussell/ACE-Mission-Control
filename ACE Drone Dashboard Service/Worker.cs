using ACE_Mission_Control.Core.Models;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace ACE_Drone_Dashboard_Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        UGCSClient ugcsClient;
        ResponseServer server;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            ugcsClient = new UGCSClient();
            server = new ResponseServer();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await server.StartAsync("tcp://localhost:5538", HandleServerRequest, HandleServerFailure);
            if (server.Status == ServerStatus.Failed)
                return;

            while (!stoppingToken.IsCancellationRequested)
            {
                // Is the database detectable?
                // Can we connect to UgCS?
                // Can we connect to the database?
                await Task.Delay(1000, stoppingToken);
            }
        }

        private string HandleServerRequest(string request)
        {
            switch (request)
            {
                case "ping":
                    return "pong";
                case "status":
                    _logger.LogInformation("Received status request");
                    return ((int)ServiceStatus.Running).ToString();
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