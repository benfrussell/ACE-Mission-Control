using ACE_Mission_Control.Core.Models;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace ACE_Drone_Dashboard_Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        UGCSClient ugcsClient;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ugcsClient = new UGCSClient();
            while (!stoppingToken.IsCancellationRequested)
            {
                // Is the database detectable?
                // Can we connect to UgCS?
                // Can we connect to the database?
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}