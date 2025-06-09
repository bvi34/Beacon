using System.ComponentModel.DataAnnotations;

namespace Beacon.Models
{
    public class DiscoveredDevice
    {
        [Required]
        [StringLength(45)] // IPv6 max length
        public string IpAddress { get; set; } = string.Empty;

        [StringLength(255)]
        public string Hostname { get; set; } = string.Empty;

        [StringLength(255)]
        public string MacAddress { get; set; } = string.Empty;

        [StringLength(100)]
        public string DeviceType { get; set; } = string.Empty;

        [StringLength(100)]
        public string Manufacturer { get; set; } = string.Empty;

        [StringLength(100)]
        public string OperatingSystem { get; set; } = string.Empty;

        public bool IsReachable { get; set; }

        public double ResponseTimeMs { get; set; }

        public List<int> OpenPorts { get; set; } = new();

        public List<DiscoveredService> Services { get; set; } = new();

        public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

        // This will be set by the controller when checking against existing devices
        public bool AlreadyExists { get; set; }
    }

    public class DiscoveredService
    {
        public int Port { get; set; }
        public string Protocol { get; set; } = "TCP";
        public string ServiceName { get; set; } = string.Empty;
        public string Banner { get; set; } = string.Empty;
        public bool IsSecure { get; set; }
    }
}