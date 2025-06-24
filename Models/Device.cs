using Beacon.Models;
using System.ComponentModel.DataAnnotations;

namespace Beacon.Models
{
    public class Device
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Hostname { get; set; } = string.Empty;

        [Required]
        [StringLength(45)] // IPv6 max length
        public string IpAddress { get; set; } = string.Empty;

        public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;
		public DateTime? LastSeen { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public List<MonitoredPort> MonitoredPorts { get; set; } = new();
        public List<Certificate> Certificates { get; set; } = new();
    }

    public enum DeviceStatus
    {
        Unknown = 0,
        Online = 1,
        Offline = 2,
        Warning = 3,
        Error = 4
    }
}