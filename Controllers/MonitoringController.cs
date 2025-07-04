using Microsoft.AspNetCore.Mvc;
using Beacon.Services;
using Beacon.Models;
using Microsoft.EntityFrameworkCore;
using Beacon.Data;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Beacon.Controllers
{
    [ApiController]
    [Route("api/monitoring")]
    public class UrlMonitorController : ControllerBase
    {
        private readonly IUrlMonitoringService _urlMonitoringService;
        private readonly ApplicationDbContext _dbContext; // Replace with your actual DbContext
        private readonly ILogger<UrlMonitorController> _logger;

        public UrlMonitorController(IUrlMonitoringService urlMonitoringService, ApplicationDbContext dbContext, ILogger<UrlMonitorController> logger)
        {
            _urlMonitoringService = urlMonitoringService;
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("stats")]
        public async Task<ActionResult<MonitoringStats>> GetStats()
        {
            try
            {
                var stats = await _urlMonitoringService.GetMonitoringStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving monitoring stats");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("monitors")]
        public async Task<ActionResult<IEnumerable<UrlMonitor>>> GetMonitors()
        {
            try
            {
                var monitors = await _urlMonitoringService.GetAllMonitorsAsync();
                return Ok(monitors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving monitors");
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpPost("monitors")]
        public async Task<IActionResult> AddMonitor([FromBody] UrlMonitorStatusDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Url))
                return BadRequest("URL is required.");

            var newMonitor = new UrlMonitor
            {
                Name = dto.Name,
                Url = dto.Url,
                IsActive = dto.IsActive,
                MonitorSsl = dto.MonitorSsl,
                TimeoutSeconds = dto.TimeoutSeconds,
                CheckIntervalMinutes = dto.CheckIntervalMinutes,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.UrlMonitors.Add(newMonitor);
            await _dbContext.SaveChangesAsync();

            return Ok(newMonitor);
        }


		[HttpPost("check-all")]
		public async Task<List<object>> CheckAllMonitors()
        {
			var monitors = await _dbContext.UrlMonitors
				.Where(m => m.IsActive)
				.ToListAsync();

			var results = new List<object>();

			foreach (var monitor in monitors)
			{
				monitor.TotalChecks++;
				monitor.UpdatedAt = DateTime.UtcNow;
				monitor.LastChecked = DateTime.UtcNow;

				var sw = Stopwatch.StartNew();
				try
				{
					using var handler = new HttpClientHandler
					{
						ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
					};
					using var client = new HttpClient(handler)
					{
						Timeout = TimeSpan.FromSeconds(monitor.TimeoutSeconds)
					};

					var response = await client.GetAsync(monitor.Url);
					sw.Stop();

					monitor.LastResponseCode = (int)response.StatusCode;
					monitor.LastResponseTimeMs = sw.Elapsed.TotalMilliseconds;
					monitor.LastError = "";

					if (response.IsSuccessStatusCode)
					{
						monitor.Status = UrlStatus.Up;
						monitor.LastUptime = DateTime.UtcNow;
						monitor.SuccessfulChecks++;
						monitor.ConsecutiveFailures = 0;
					}
					else
					{
						monitor.Status = UrlStatus.Down;
						monitor.LastDowntime = DateTime.UtcNow;
						monitor.ConsecutiveFailures++;
					}

					// Optional SSL check
					if (monitor.IsHttps && monitor.MonitorSsl)
					{
						var uri = new Uri(monitor.Url);
						using var tcpClient = new TcpClient();
						await tcpClient.ConnectAsync(uri.Host, 443);
						using var sslStream = new SslStream(tcpClient.GetStream(), false, (s, cert, chain, errors) => true);
						await sslStream.AuthenticateAsClientAsync(uri.Host);

						var cert = new X509Certificate2(sslStream.RemoteCertificate);
						monitor.Certificate = new Certificate
						{
							CommonName = cert.GetNameInfo(X509NameType.SimpleName, false),
							ExpiryDate = cert.NotAfter,
							Issuer = cert.Issuer,
							Thumbprint = cert.SerialNumber,
							CreatedAt = DateTime.UtcNow
						};
					}
				}
				catch (Exception ex)
				{
					sw.Stop();
					monitor.Status = UrlStatus.Error;
					monitor.LastResponseCode = null;
					monitor.LastResponseTimeMs = null;
					monitor.LastDowntime = DateTime.UtcNow;
					monitor.LastError = ex.Message;
					monitor.ConsecutiveFailures++;
				}

				monitor.UptimePercentage = monitor.TotalChecks > 0
					? (double)monitor.SuccessfulChecks / monitor.TotalChecks * 100
					: 0;

				results.Add(new
				{
					monitor.Id,
					monitor.Name,
					monitor.Url,
					monitor.Status,
					monitor.LastResponseCode,
					monitor.LastResponseTimeMs,
					monitor.LastError,
					monitor.UptimePercentage,
					monitor.ConsecutiveFailures
				});
			}

			await _dbContext.SaveChangesAsync();
			return results;
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

                var result = await _urlMonitoringService.CheckUrlAsync(monitor);
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
                await _urlMonitoringService.RunMonitoringCycleAsync();
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
				var stats = await _urlMonitoringService.GetMonitoringStatsAsync();

				var monitors = await _dbContext.UrlMonitors
					.Include(m => m.Certificate)
					.OrderBy(m => m.Name)
					.ToListAsync();

				var monitorDtos = monitors.Select(m => new UrlMonitorStatusDto
				{
					Name = m.Name,
					Url = m.Url,
					IsActive = m.IsActive,
					MonitorSsl = m.MonitorSsl,
					TimeoutSeconds = m.TimeoutSeconds,
					CheckIntervalMinutes = m.CheckIntervalMinutes,
					Description = m.Description,
					UrlStatus = m.Status.ToString(),
					LastChecked = m.LastChecked,
					Certificate = m.Certificate == null ? null : new CertificateDto
					{
						ExpiryDate = m.Certificate.ExpiryDate,
					}
				}).ToList();

				var expiringCerts = await _dbContext.Certificates
					.Where(c => c.ExpiryDate <= DateTime.UtcNow.AddDays(30) && c.ExpiryDate > DateTime.UtcNow)
					.Include(c => c.UrlMonitors)
					.OrderBy(c => c.ExpiryDate)
					.Take(10)
					.ToListAsync();

				var dashboardData = new DashboardData
				{
					Stats = stats, // Use the stats directly from the service
					Monitors = monitorDtos,
					ExpiringCertificates = expiringCerts,
					LastUpdated = DateTime.UtcNow,
				};

				return Ok(dashboardData);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving dashboard data: {Message}", ex.Message);
				return StatusCode(500, new { error = ex.Message, details = ex.ToString() }); // This will show the real error
			}
		}

	}
	public class DashboardData
	{
		public MonitoringStats Stats { get; set; }
		public List<UrlMonitorStatusDto> Monitors { get; set; } // <-- Changed from List<UrlMonitor>
		public List<Certificate> ExpiringCertificates { get; set; }
		public DateTime LastUpdated { get; set; }
        public List<Device>? Devices { get; set; } // Assuming you want to include devices as well
	}

	public class UrlMonitorStatusDto
	{
		// Configuration
		public string Name { get; set; }
		public string Url { get; set; }
		public bool IsActive { get; set; }
		public bool MonitorSsl { get; set; }
		public int TimeoutSeconds { get; set; }
		public int CheckIntervalMinutes { get; set; }
		public string? Description { get; set; }

		// Live / Persisted Status
		public string UrlStatus { get; set; } // Status from DB
		public DateTime? LastChecked { get; set; }

		public CertificateDto? Certificate { get; set; }
	}

	public class CertificateDto
	{
		public int Id { get; set; }
		public DateTime? ExpiryDate { get; set; }
		public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value <= DateTime.UtcNow;

		public int? DaysUntilExpiry => ExpiryDate.HasValue
			? (int?)(ExpiryDate.Value - DateTime.UtcNow).TotalDays
			: null;
	}

	public class DashboardDataDto
	{
		public MonitoringStats? Stats { get; set; }
		public List<UrlMonitorStatusDto>? Monitors { get; set; }
		public List<CertificateDto>? ExpiringCertificates { get; set; }
		public DateTime LastUpdated { get; set; }
	}
}