using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Beacon.Services;

namespace Beacon.Services
{
    public class UrlMonitorBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UrlMonitorBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Default check interval

        public UrlMonitorBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<UrlMonitorBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("URL Monitor Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var urlMonitoringService = scope.ServiceProvider.GetRequiredService<IUrlMonitoringService>();

                    _logger.LogDebug("Starting URL monitoring check cycle");
                    await urlMonitoringService.CheckAllActiveUrlsAsync();
                    _logger.LogDebug("URL monitoring check cycle completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during URL monitoring check cycle");
                }

                // Calculate next check time based on the minimum interval of all monitors
                var nextCheckDelay = await CalculateNextCheckDelay();

                try
                {
                    await Task.Delay(nextCheckDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
            }

            _logger.LogInformation("URL Monitor Background Service stopped");
        }

        private async Task<TimeSpan> CalculateNextCheckDelay()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var urlMonitoringService = scope.ServiceProvider.GetRequiredService<IUrlMonitoringService>();
                var monitors = await urlMonitoringService.GetAllMonitorsAsync();

                if (!monitors.Any() || !monitors.Any(m => m.IsActive))
                {
                    return _checkInterval; // Use default if no active monitors
                }

                // Find the minimum check interval among active monitors
                var minInterval = monitors
                    .Where(m => m.IsActive)
                    .Min(m => m.CheckIntervalMinutes);

                return TimeSpan.FromMinutes(Math.Max(1, minInterval)); // Minimum 1 minute
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not calculate next check delay, using default interval");
                return _checkInterval;
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("URL Monitor Background Service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
}