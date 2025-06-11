using Microsoft.Extensions.Hosting;

namespace Beacon.Services
{
    public class BackgroundMonitoringService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundMonitoringService> _logger;
        private readonly TimeSpan _defaultInterval = TimeSpan.FromMinutes(5);

        public BackgroundMonitoringService(IServiceProvider serviceProvider, ILogger<BackgroundMonitoringService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background monitoring service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var monitoringService = scope.ServiceProvider.GetRequiredService<IUrlMonitoringService>();

                    await monitoringService.RunMonitoringCycleAsync();
                    _logger.LogInformation("Monitoring cycle completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during monitoring cycle");
                }

                await Task.Delay(_defaultInterval, stoppingToken);
            }

            _logger.LogInformation("Background monitoring service stopped");
        }
    }
}