using Beacon.Models;
using System.ComponentModel.DataAnnotations;

namespace Beacon.Models
{
    public enum UrlStatus
    {
        Unknown,
        Up,
        Down,
        Timeout,
        Error
    }


}
    public class UrlMonitor
{
    public int Id { get; set; }
    [Required]
    [StringLength(500)]
    public string Url { get; set; } = string.Empty;
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    public int CheckIntervalMinutes { get; set; } = 5;
    public UrlStatus Status { get; set; } = UrlStatus.Unknown;
    public int? LastResponseCode { get; set; }
    public double? LastResponseTimeMs { get; set; }
    public DateTime? LastChecked { get; set; }
    public DateTime? LastUptime { get; set; }
    public DateTime? LastDowntime { get; set; }
    [StringLength(1000)]
    public string LastError { get; set; } = string.Empty;
    public int? CertificateId { get; set; }

    // Additional properties for enhanced monitoring
    public bool MonitorSsl { get; set; } = true; // Whether to check SSL certificate
    public int ConsecutiveFailures { get; set; } = 0; // Track consecutive failures
    public int TotalChecks { get; set; } = 0; // Total number of checks performed
    public int SuccessfulChecks { get; set; } = 0; // Number of successful checks
    public double UptimePercentage { get; set; } = 0.0; // Calculated uptime percentage

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Certificate? Certificate { get; set; }

    // Computed properties
    public bool IsUp => Status == UrlStatus.Up;
    public bool IsHttps => Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public TimeSpan? Uptime => LastUptime.HasValue && LastDowntime.HasValue
        ? (LastUptime > LastDowntime ? DateTime.UtcNow - LastUptime : null)
        : null;

    // New computed property for reliability
    public double ReliabilityScore => TotalChecks > 0 ? (double)SuccessfulChecks / TotalChecks * 100 : 0;
}