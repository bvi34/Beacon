using System.ComponentModel.DataAnnotations;

namespace Beacon.Models
{
    public class MonitoredPort
    {
        public int Id { get; set; }

        [Required]
        public int DeviceId { get; set; }

        [Required]
        [Range(1, 65535)]
        public int Port { get; set; }

        [Required]
        [StringLength(10)]
        public string Protocol { get; set; } = "TCP";

        [StringLength(100)]
        public string ServiceName { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public PortStatus Status { get; set; } = PortStatus.Unknown;

        public DateTime LastChecked { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public Device Device { get; set; } = null!;
    }

    public enum PortStatus
    {
        Unknown,
        Open,
        Closed,
        Filtered,
        Timeout
    }
}