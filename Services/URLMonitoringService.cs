using Microsoft.AspNetCore.Mvc;
using Beacon.Models;
using Beacon.Services;
using Microsoft.AspNetCore.Authorization;

namespace Beacon.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UrlController : ControllerBase
    {
        private readonly IUrlMonitoringService _urlMonitoringService;
        private readonly ILogger<UrlController> _logger;

        public UrlController(IUrlMonitoringService urlMonitoringService, ILogger<UrlController> logger)
        {
            _urlMonitoringService = urlMonitoringService;
            _logger = logger;
        }

        // GET: api/url
        [HttpGet]
        public async Task<ActionResult<List<UrlMonitor>>> GetAllMonitors()
        {
            try
            {
                var monitors = await _urlMonitoringService.GetAllMonitorsAsync();
                return Ok(monitors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving URL monitors");
                return StatusCode(500, "An error occurred while retrieving URL monitors");
            }
        }

        // GET: api/url/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<UrlMonitor>> GetMonitor(int id)
        {
            try
            {
                var monitors = await _urlMonitoringService.GetAllMonitorsAsync();
                var monitor = monitors.FirstOrDefault(m => m.Id == id);

                if (monitor == null)
                {
                    return NotFound($"URL monitor with ID {id} not found");
                }

                return Ok(monitor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving URL monitor {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the URL monitor");
            }
        }

        // POST: api/url
        [HttpPost]
        public async Task<ActionResult<UrlMonitor>> CreateMonitor([FromBody] CreateUrlMonitorRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(request.Url))
                {
                    return BadRequest("URL is required");
                }

                // Validate URL format
                if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return BadRequest("Invalid URL format. URL must be a valid HTTP or HTTPS URL");
                }

                var monitor = await _urlMonitoringService.AddMonitorAsync(
                    request.Url,
                    request.Name ?? string.Empty,
                    request.Description ?? string.Empty
                );

                return CreatedAtAction(nameof(GetMonitor), new { id = monitor.Id }, monitor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating URL monitor for {Url}", request.Url);
                return StatusCode(500, "An error occurred while creating the URL monitor");
            }
        }

        // PUT: api/url/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMonitor(int id, [FromBody] UpdateUrlMonitorRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var monitors = await _urlMonitoringService.GetAllMonitorsAsync();
                var monitor = monitors.FirstOrDefault(m => m.Id == id);

                if (monitor == null)
                {
                    return NotFound($"URL monitor with ID {id} not found");
                }

                // Update properties
                if (!string.IsNullOrWhiteSpace(request.Name))
                    monitor.Name = request.Name;

                if (!string.IsNullOrWhiteSpace(request.Description))
                    monitor.Description = request.Description;

                if (request.IsActive.HasValue)
                    monitor.IsActive = request.IsActive.Value;

                if (request.TimeoutSeconds.HasValue && request.TimeoutSeconds.Value > 0)
                    monitor.TimeoutSeconds = request.TimeoutSeconds.Value;

                if (request.CheckIntervalMinutes.HasValue && request.CheckIntervalMinutes.Value > 0)
                    monitor.CheckIntervalMinutes = request.CheckIntervalMinutes.Value;

                // Validate URL if provided
                if (!string.IsNullOrWhiteSpace(request.Url))
                {
                    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        return BadRequest("Invalid URL format. URL must be a valid HTTP or HTTPS URL");
                    }
                    monitor.Url = request.Url;
                }

                await _urlMonitoringService.UpdateMonitorAsync(monitor);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating URL monitor {Id}", id);
                return StatusCode(500, "An error occurred while updating the URL monitor");
            }
        }

        // DELETE: api/url/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMonitor(int id)
        {
            try
            {
                await _urlMonitoringService.DeleteMonitorAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting URL monitor {Id}", id);
                return StatusCode(500, "An error occurred while deleting the URL monitor");
            }
        }

        // POST: api/url/{id}/check
        [HttpPost("{id}/check")]
        public async Task<ActionResult<UrlMonitor>> CheckMonitor(int id)
        {
            try
            {
                var monitors = await _urlMonitoringService.GetAllMonitorsAsync();
                var monitor = monitors.FirstOrDefault(m => m.Id == id);

                if (monitor == null)
                {
                    return NotFound($"URL monitor with ID {id} not found");
                }

                var updatedMonitor = await _urlMonitoringService.CheckUrlAsync(monitor);
                return Ok(updatedMonitor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking URL monitor {Id}", id);
                return StatusCode(500, "An error occurred while checking the URL monitor");
            }
        }

        // POST: api/url/check-all
        [HttpPost("check-all")]
        public async Task<IActionResult> CheckAllMonitors()
        {
            try
            {
                await _urlMonitoringService.CheckAllActiveUrlsAsync();
                return Ok(new { message = "All active URL monitors have been checked" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking all URL monitors");
                return StatusCode(500, "An error occurred while checking all URL monitors");
            }
        }

        // GET: api/url/status
        [HttpGet("status")]
        public async Task<ActionResult> GetOverallStatus()
        {
            try
            {
                var monitors = await _urlMonitoringService.GetAllMonitorsAsync();
                var activeMonitors = monitors.Where(m => m.IsActive).ToList();

                var status = new
                {
                    TotalMonitors = monitors.Count,
                    ActiveMonitors = activeMonitors.Count,
                    UpCount = activeMonitors.Count(m => m.Status == UrlStatus.Up),
                    DownCount = activeMonitors.Count(m => m.Status == UrlStatus.Down),
                    ErrorCount = activeMonitors.Count(m => m.Status == UrlStatus.Error),
                    TimeoutCount = activeMonitors.Count(m => m.Status == UrlStatus.Timeout),
                    UnknownCount = activeMonitors.Count(m => m.Status == UrlStatus.Unknown),
                    CertificatesExpiringSoon = monitors.Count(m => m.Certificate?.IsExpiringSoon == true),
                    CertificatesExpired = monitors.Count(m => m.Certificate?.IsExpired == true)
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting URL monitoring status");
                return StatusCode(500, "An error occurred while getting the status");
            }
        }
    }

    // Request DTOs
    public class CreateUrlMonitorRequest
    {
        public string Url { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateUrlMonitorRequest
    {
        public string? Url { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public int? TimeoutSeconds { get; set; }
        public int? CheckIntervalMinutes { get; set; }
    }
}