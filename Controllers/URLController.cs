using Microsoft.AspNetCore.Mvc;
using Beacon.Services;
using Beacon.Models;
using Microsoft.EntityFrameworkCore;
using Beacon.Data;

namespace Beacon.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MonitoringController : ControllerBase
    {
        private readonly IUrlMonitoringService _monitoringService;
        private readonly ApplicationDbContext _dbContext; // Replace with your actual DbContext
        private readonly ILogger<MonitoringController> _logger;

        public MonitoringController(IUrlMonitoringService monitoringService, ApplicationDbContext dbContext, ILogger<MonitoringController> logger)
        {
            _monitoringService = monitoringService;
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("stats")]
        public async Task<ActionResult<MonitoringStats>> GetStats()
        {
            try
            {
                var stats = await _monitoringService.GetMonitoringStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving monitoring stats");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("monitors")]
        public async Task<ActionResult<IEnumerable<UrlMonitor>>> GetAllMonitors()
        {
            try
            {
                var monitors = await _dbContext.UrlMonitors
                    .Include(m => m.Certificate)
                    .OrderBy(m => m.Name)
                    .ToListAsync();

                return Ok(monitors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving monitors");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("monitors/{id}")]
        public async Task<ActionResult<UrlMonitor>> GetMonitor(int id)
        {
            try
            {
                var monitor = await _dbContext.UrlMonitors
                    .Include(m => m.Certificate)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (monitor == null)
                    return NotFound();

                return Ok(monitor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving monitor {MonitorId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("monitors/{id}/check")]
        public async Task<ActionResult<UrlMonitorResult>> CheckMonitor(int id)
        {
            try
            {
                var monitor = await _dbContext.UrlMonitors.FindAsync(id);
                if (monitor == null)
                    return NotFound();

                var result = await _monitoringService.CheckUrlAsync(monitor);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking monitor {MonitorId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("certificates")]
        public async Task<ActionResult<IEnumerable<Certificate>>> GetCertificates()
        {
            try
            {
                var certificates = await _dbContext.Certificates
                    .Include(c => c.UrlMonitors)
                    .OrderBy(c => c.ExpiryDate)
                    .ToListAsync();

                return Ok(certificates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving certificates");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("certificates/expiring")]
        public async Task<ActionResult<IEnumerable<Certificate>>> GetExpiringCertificates(int days = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(days);
                var certificates = await _dbContext.Certificates
                    .Where(c => c.ExpiryDate <= cutoffDate && c.ExpiryDate > DateTime.UtcNow)
                    .Include(c => c.UrlMonitors)
                    .OrderBy(c => c.ExpiryDate)
                    .ToListAsync();

                return Ok(certificates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expiring certificates");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("run-cycle")]
        public async Task<ActionResult> RunMonitoringCycle()
        {
            try
            {
                await _monitoringService.RunMonitoringCycleAsync();
                return Ok(new { message = "Monitoring cycle completed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running monitoring cycle");
                return StatusCode(500, "Internal server error");
            }
        }

		[HttpGet("dashboard-data")]
        public async Task<ActionResult<DashboardData>> GetDashboardData()
        {
            try
            {
                var stats = await _monitoringService.GetMonitoringStatsAsync();
                var monitors = await _dbContext.UrlMonitors
                    .Include(m => m.Certificate)
                    .OrderBy(m => m.Name)
                    .ToListAsync();
                var expiringCerts = await _dbContext.Certificates
                    .Where(c => c.ExpiryDate <= DateTime.UtcNow.AddDays(30) && c.ExpiryDate > DateTime.UtcNow)
                    .Include(c => c.UrlMonitors)
                    .OrderBy(c => c.ExpiryDate)
                    .Take(10)
                    .ToListAsync();

                var dashboardData = new DashboardData
                {
                    Stats = stats,
                    Monitors = monitors,
                    ExpiringCertificates = expiringCerts,
                    LastUpdated = DateTime.UtcNow
                };

                return Ok(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard data");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class DashboardData
    {
        public MonitoringStats Stats { get; set; } = new();
        public List<UrlMonitor> Monitors { get; set; } = new();
        public List<Certificate> ExpiringCertificates { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }
}

// Program.cs registration
