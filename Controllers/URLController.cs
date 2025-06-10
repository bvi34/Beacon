using Microsoft.AspNetCore.Mvc;
using Beacon.Models;
using Beacon.Services;

namespace Beacon.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UrlMonitorController : ControllerBase
    {
        private readonly IUrlMonitoringService _urlMonitoringService;
        private readonly ILogger<UrlMonitorController> _logger;

        public UrlMonitorController(IUrlMonitoringService urlMonitoringService, ILogger<UrlMonitorController> logger)
        {
            _urlMonitoringService = urlMonitoringService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<UrlMonitor>>> GetMonitors()
        {
            var monitors = await _urlMonitoringService.GetAllMonitorsAsync();
            return Ok(monitors);
        }

        [HttpGet("status")]
        public async Task<ActionResult<object>> GetStatus()
        {
            var monitors = await _urlMonitoringService.GetAllMonitorsAsync();

            var upCount = monitors.Count(m => m.Status == UrlStatus.Up);
            var downCount = monitors.Count(m => m.Status == UrlStatus.Down);
            var errorCount = monitors.Count(m => m.Status == UrlStatus.Error || m.Status == UrlStatus.Timeout);
            var expiringCerts = monitors.Count(m => m.Certificate?.IsExpiringSoon == true);
            var expiredCerts = monitors.Count(m => m.Certificate?.IsExpired == true);

            return Ok(new
            {
                totalUrls = monitors.Count,
                upUrls = upCount,
                downUrls = downCount,
                errorUrls = errorCount,
                expiringCertificates = expiringCerts,
                expiredCertificates = expiredCerts,
                monitors = monitors.Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    url = m.Url,
                    status = m.Status.ToString(),
                    lastChecked = m.LastChecked,
                    responseTime = m.LastResponseTimeMs,
                    certificate = m.Certificate != null ? new
                    {
                        commonName = m.Certificate.CommonName,
                        expiryDate = m.Certificate.ExpiryDate,
                        daysUntilExpiry = m.Certificate.DaysUntilExpiry,
                        isExpired = m.Certificate.IsExpired,
                        isExpiringSoon = m.Certificate.IsExpiringSoon,
                        status = m.Certificate.Status.ToString()
                    } : null
                })
            });
        }

        [HttpPost]
        public async Task<ActionResult<UrlMonitor>> AddMonitor([FromBody] AddUrlMonitorRequest request)
        {
            try
            {
                var monitor = await _urlMonitoringService.AddMonitorAsync(
                    request.Url,
                    request.Name,
                    request.Description
                );
                return CreatedAtAction(nameof(GetMonitor), new { id = monitor.Id }, monitor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding URL monitor for {Url}", request.Url);
                return BadRequest($"Error adding monitor: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UrlMonitor>> GetMonitor(int id)
        {
            var monitors = await _urlMonitoringService.GetAllMonitorsAsync();
            var monitor = monitors.FirstOrDefault(m => m.Id == id);

            if (monitor == null)
                return NotFound();

            return Ok(monitor);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMonitor(int id, [FromBody] UpdateUrlMonitorRequest request)
        {
            var monitors = await _urlMonitoringService.GetAllMonitorsAsync();
            var monitor = monitors.FirstOrDefault(m => m.Id == id);

            if (monitor == null)
                return NotFound();

            monitor.Name = request.Name ?? monitor.Name;
            monitor.Description = request.Description ?? monitor.Description;
            monitor.IsActive = request.IsActive;
            monitor.TimeoutSeconds = request.TimeoutSeconds;
            monitor.CheckIntervalMinutes = request.CheckIntervalMinutes;

            await _urlMonitoringService.UpdateMonitorAsync(monitor);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMonitor(int id)
        {
            await _urlMonitoringService.DeleteMonitorAsync(id);
            return NoContent();
        }

        [HttpPost("{id}/check")]
        public async Task<ActionResult<UrlMonitor>> CheckMonitor(int id)
        {
            var monitors = await _urlMonitoringService.GetAllMonitorsAsync();
            var monitor = monitors.FirstOrDefault(m => m.Id == id);

            if (monitor == null)
                return NotFound();

            var updatedMonitor = await _urlMonitoringService.CheckUrlAsync(monitor);
            return Ok(updatedMonitor);
        }

        [HttpPost("check-all")]
        public async Task<IActionResult> CheckAllMonitors()
        {
            await _urlMonitoringService.CheckAllActiveUrlsAsync();
            return Ok(new { message = "All active monitors checked" });
        }
    }

    public class AddUrlMonitorRequest
    {
        public string Url { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class UpdateUrlMonitorRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 30;
        public int CheckIntervalMinutes { get; set; } = 5;
    }
}