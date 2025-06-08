using Microsoft.AspNetCore.Mvc;
using Beacon.Services;
using Beacon.Models;
using Microsoft.EntityFrameworkCore;
using Beacon.Data;

namespace Beacon.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiscoveryController : ControllerBase
    {
        private readonly INetworkDiscoveryService _discoveryService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DiscoveryController> _logger;

        public DiscoveryController(
            INetworkDiscoveryService discoveryService,
            ApplicationDbContext context,
            ILogger<DiscoveryController> logger)
        {
            _discoveryService = discoveryService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Discover devices on the local network
        /// </summary>
        [HttpPost("scan-local")]
        public async Task<ActionResult<DiscoveryResult>> ScanLocalNetwork()
        {
            try
            {
                _logger.LogInformation("Starting local network discovery");

                var discoveredDevices = await _discoveryService.DiscoverLocalNetworkAsync();

                // Check which devices already exist in the database
                var existingIps = await _context.Devices
                    .Select(d => d.IpAddress)
                    .ToListAsync();

                foreach (var device in discoveredDevices)
                {
                    device.AlreadyExists = existingIps.Contains(device.IpAddress);
                }

                var result = new DiscoveryResult
                {
                    TotalDevicesFound = discoveredDevices.Count,
                    NewDevices = discoveredDevices.Where(d => !d.AlreadyExists).ToList(),
                    ExistingDevices = discoveredDevices.Where(d => d.AlreadyExists).ToList(),
                    ScanCompletedAt = DateTime.UtcNow
                };

                _logger.LogInformation($"Local network discovery completed. Found {result.TotalDevicesFound} devices ({result.NewDevices.Count} new)");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during local network discovery");
                return StatusCode(500, new { error = "Network discovery failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Discover devices in a specific network range
        /// </summary>
        [HttpPost("scan-range")]
        public async Task<ActionResult<DiscoveryResult>> ScanNetworkRange([FromBody] NetworkRangeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NetworkRange))
            {
                return BadRequest(new { error = "Network range is required" });
            }

            try
            {
                _logger.LogInformation($"Starting network discovery for range: {request.NetworkRange}");

                var discoveredDevices = await _discoveryService.DiscoverDevicesAsync(request.NetworkRange);

                // Check which devices already exist
                var existingIps = await _context.Devices
                    .Select(d => d.IpAddress)
                    .ToListAsync();

                foreach (var device in discoveredDevices)
                {
                    device.AlreadyExists = existingIps.Contains(device.IpAddress);
                }

                var result = new DiscoveryResult
                {
                    TotalDevicesFound = discoveredDevices.Count,
                    NewDevices = discoveredDevices.Where(d => !d.AlreadyExists).ToList(),
                    ExistingDevices = discoveredDevices.Where(d => d.AlreadyExists).ToList(),
                    ScanCompletedAt = DateTime.UtcNow,
                    NetworkRange = request.NetworkRange
                };

                _logger.LogInformation($"Network range discovery completed for {request.NetworkRange}. Found {result.TotalDevicesFound} devices");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during network range discovery for {request.NetworkRange}");
                return StatusCode(500, new { error = "Network discovery failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Probe a specific device
        /// </summary>
        [HttpPost("probe")]
        public async Task<ActionResult<DiscoveredDevice>> ProbeDevice([FromBody] ProbeDeviceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.IpAddress))
            {
                return BadRequest(new { error = "IP address is required" });
            }

            try
            {
                var device = await _discoveryService.ProbeDeviceAsync(request.IpAddress);

                if (device == null)
                {
                    return NotFound(new { error = $"Device at {request.IpAddress} is not responding" });
                }

                // Check if device already exists
                var existingDevice = await _context.Devices
                    .FirstOrDefaultAsync(d => d.IpAddress == request.IpAddress);

                device.AlreadyExists = existingDevice != null;

                return Ok(device);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error probing device {request.IpAddress}");
                return StatusCode(500, new { error = "Device probe failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Add discovered devices to monitoring
        /// </summary>
        [HttpPost("add-devices")]
        public async Task<ActionResult<AddDevicesResult>> AddDevices([FromBody] AddDevicesRequest request)
        {
            if (request.DeviceIpAddresses == null || !request.DeviceIpAddresses.Any())
            {
                return BadRequest(new { error = "At least one device IP address is required" });
            }

            var results = new List<AddDeviceResult>();

            foreach (var ipAddress in request.DeviceIpAddresses)
            {
                try
                {
                    // First probe the device to get current information
                    var discoveredDevice = await _discoveryService.ProbeDeviceAsync(ipAddress);

                    if (discoveredDevice == null)
                    {
                        results.Add(new AddDeviceResult
                        {
                            IpAddress = ipAddress,
                            Success = false,
                            Error = "Device is not responding"
                        });
                        continue;
                    }

                    var success = await _discoveryService.AddDiscoveredDeviceAsync(discoveredDevice);

                    results.Add(new AddDeviceResult
                    {
                        IpAddress = ipAddress,
                        Hostname = discoveredDevice.Hostname,
                        Success = success,
                        Error = success ? null : "Failed to add device to database"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error adding device {ipAddress}");
                    results.Add(new AddDeviceResult
                    {
                        IpAddress = ipAddress,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            var result = new AddDevicesResult
            {
                TotalRequested = request.DeviceIpAddresses.Count,
                SuccessfullyAdded = results.Count(r => r.Success),
                Failed = results.Count(r => !r.Success),
                Results = results
            };

            return Ok(result);
        }

        /// <summary>
        /// Get discovery history or status
        /// </summary>
        [HttpGet("status")]
        public async Task<ActionResult<DiscoveryStatus>> GetDiscoveryStatus()
        {
            try
            {
                var totalDevices = await _context.Devices.CountAsync();
                var onlineDevices = await _context.Devices
                    .CountAsync(d => d.Status == DeviceStatus.Online);
                var offlineDevices = await _context.Devices
                    .CountAsync(d => d.Status == DeviceStatus.Offline);

                var recentlyAdded = await _context.Devices
                    .Where(d => d.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                    .CountAsync();

                var status = new DiscoveryStatus
                {
                    TotalManagedDevices = totalDevices,
                    OnlineDevices = onlineDevices,
                    OfflineDevices = offlineDevices,
                    RecentlyAddedDevices = recentlyAdded,
                    LastDiscoveryRun = DateTime.UtcNow // This would be stored separately in a real implementation
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discovery status");
                return StatusCode(500, new { error = "Failed to get discovery status" });
            }
        }
    }

    // Request/Response DTOs
    public class NetworkRangeRequest
    {
        public string NetworkRange { get; set; } = string.Empty;
    }

    public class ProbeDeviceRequest
    {
        public string IpAddress { get; set; } = string.Empty;
    }

    public class AddDevicesRequest
    {
        public List<string> DeviceIpAddresses { get; set; } = new();
    }

    public class DiscoveryResult
    {
        public int TotalDevicesFound { get; set; }
        public List<DiscoveredDevice> NewDevices { get; set; } = new();
        public List<DiscoveredDevice> ExistingDevices { get; set; } = new();
        public DateTime ScanCompletedAt { get; set; }
        public string? NetworkRange { get; set; }
    }

    public class AddDevicesResult
    {
        public int TotalRequested { get; set; }
        public int SuccessfullyAdded { get; set; }
        public int Failed { get; set; }
        public List<AddDeviceResult> Results { get; set; } = new();
    }

    public class AddDeviceResult
    {
        public string IpAddress { get; set; } = string.Empty;
        public string? Hostname { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    public class DiscoveryStatus
    {
        public int TotalManagedDevices { get; set; }
        public int OnlineDevices { get; set; }
        public int OfflineDevices { get; set; }
        public int RecentlyAddedDevices { get; set; }
        public DateTime LastDiscoveryRun { get; set; }
    }
}