using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Beacon.Data;
using Beacon.Models;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.ComponentModel.DataAnnotations;

namespace Beacon.Controllers
{
    [Route("[controller]")]
    public class DevicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DevicesController> _logger;

        public DevicesController(ApplicationDbContext context, ILogger<DevicesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region MVC Actions (Views)

        // GET: Devices
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var devices = await _context.Devices
                .Include(d => d.MonitoredPorts)
                .Include(d => d.Certificates)
                .OrderByDescending(d => d.LastSeen)
                .ToListAsync();
            return View(devices);
        }

        // GET: Devices/Details/5
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var device = await _context.Devices
                .Include(d => d.MonitoredPorts)
                .Include(d => d.Certificates)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (device == null)
            {
                return NotFound();
            }

            return View(device);
        }

        // GET: Devices/Delete/5
        [HttpGet("Delete/{id}")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var device = await _context.Devices
                .FirstOrDefaultAsync(m => m.Id == id);

            if (device == null)
            {
                return NotFound();
            }

            return View(device);
        }

        // POST: Devices/Delete/5
        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var device = await _context.Devices.FindAsync(id);
            if (device != null)
            {
                _context.Devices.Remove(device);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region API Endpoints

        // API: Get device count
        [HttpGet("api/count")]
        public async Task<IActionResult> GetDeviceCount()
        {
            var count = await _context.Devices.CountAsync();
            return Json(new { count });
        }

        // API: Get device status summary
        [HttpGet("api/status-summary")]
        public async Task<IActionResult> GetStatusSummary()
        {
            var summary = await _context.Devices
                .GroupBy(d => d.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();
            return Json(summary);
        }

        // API: Add device
        [HttpPost("api/add")]
        public async Task<ActionResult<AddDeviceResponse>> AddDevice([FromBody] AddDeviceRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Check if device already exists
                var existingDevice = await _context.Devices
                    .FirstOrDefaultAsync(d => d.IpAddress == request.IpAddress);

                if (existingDevice != null)
                {
                    return Conflict(new { error = "Device with this IP address already exists" });
                }

                // Validate IP address format
                if (!IPAddress.TryParse(request.IpAddress, out _))
                {
                    return BadRequest(new { error = "Invalid IP address format" });
                }

                // Create new device
                var device = new Device
                {
                    Hostname = request.Hostname ?? request.IpAddress,
                    IpAddress = request.IpAddress,
                    Status = DeviceStatus.Unknown,
                    LastSeen = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Perform initial connectivity check
                var pingResult = await PingDevice(request.IpAddress);
                if (pingResult.IsReachable)
                {
                    device.Status = DeviceStatus.Online;
                    device.LastSeen = DateTime.UtcNow;

                    // Try to resolve hostname if not provided
                    if (string.IsNullOrEmpty(request.Hostname))
                    {
                        try
                        {
                            var hostEntry = await Dns.GetHostEntryAsync(request.IpAddress);
                            device.Hostname = hostEntry.HostName;
                        }
                        catch
                        {
                            // Keep IP as hostname if resolution fails
                        }
                    }
                }
                else
                {
                    device.Status = DeviceStatus.Offline;
                }

                _context.Devices.Add(device);
                await _context.SaveChangesAsync();

                // Add default ports to monitor if specified
                if (request.MonitoredPorts?.Any() == true)
                {
                    foreach (var port in request.MonitoredPorts)
                    {
                        var monitoredPort = new MonitoredPort
                        {
                            DeviceId = device.Id,
                            Port = port.Port,
                            Protocol = port.Protocol ?? "TCP",
                            ServiceName = port.ServiceName ?? "",
                            IsEnabled = true,
                            Status = PortStatus.Unknown,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.MonitoredPorts.Add(monitoredPort);
                    }
                    await _context.SaveChangesAsync();
                }

                // Start background port scanning if device is online
                if (device.Status == DeviceStatus.Online)
                {
                    _ = Task.Run(async () => await ScanDevicePorts(device.Id));
                }

                return Ok(new AddDeviceResponse
                {
                    Success = true,
                    DeviceId = device.Id,
                    Message = $"Device {device.Hostname} added successfully",
                    InitialStatus = device.Status.ToString(),
                    PingResponse = pingResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding device {IpAddress}", request.IpAddress);
                return StatusCode(500, new { error = "Internal server error while adding device" });
            }
        }

        // API: Quick add device
        [HttpPost("api/quick-add")]
        public async Task<ActionResult<QuickAddResponse>> QuickAddDevice([FromBody] QuickAddRequest request)
        {
            try
            {
                // Validate IP address
                if (!IPAddress.TryParse(request.IpAddress, out _))
                {
                    return BadRequest(new { error = "Invalid IP address format" });
                }

                // Check if already exists
                var exists = await _context.Devices.AnyAsync(d => d.IpAddress == request.IpAddress);
                if (exists)
                {
                    return Conflict(new { error = "Device already exists" });
                }

                // Ping the device
                var pingResult = await PingDevice(request.IpAddress);

                if (!pingResult.IsReachable)
                {
                    return Ok(new QuickAddResponse
                    {
                        Success = false,
                        Message = "Device is not reachable",
                        PingResult = pingResult
                    });
                }

                // Try to resolve hostname
                string hostname = request.IpAddress;
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(request.IpAddress);
                    hostname = hostEntry.HostName;
                }
                catch { }

                // Scan common ports
                var commonPorts = new[] { 22, 23, 25, 53, 80, 110, 143, 443, 993, 995, 3389, 5985, 5986 };
                var openPorts = new List<int>();

                foreach (var port in commonPorts)
                {
                    if (await IsPortOpen(request.IpAddress, port, TimeSpan.FromMilliseconds(500)))
                    {
                        openPorts.Add(port);
                    }
                }

                // Create device
                var device = new Device
                {
                    Hostname = hostname,
                    IpAddress = request.IpAddress,
                    Status = DeviceStatus.Online,
                    LastSeen = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Devices.Add(device);
                await _context.SaveChangesAsync();

                // Add open ports for monitoring
                foreach (var port in openPorts)
                {
                    var monitoredPort = new MonitoredPort
                    {
                        DeviceId = device.Id,
                        Port = port,
                        Protocol = "TCP",
                        ServiceName = GetServiceName(port),
                        IsEnabled = true,
                        Status = PortStatus.Open,
                        LastChecked = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.MonitoredPorts.Add(monitoredPort);
                }

                await _context.SaveChangesAsync();

                return Ok(new QuickAddResponse
                {
                    Success = true,
                    DeviceId = device.Id,
                    Hostname = hostname,
                    Message = $"Device added with {openPorts.Count} monitored ports",
                    OpenPorts = openPorts,
                    PingResult = pingResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in quick add for {IpAddress}", request.IpAddress);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // API: Get all devices (for JSON responses)
        [HttpGet("api/devices")]
        public async Task<ActionResult<List<Device>>> GetAllDevices()
        {
            var devices = await _context.Devices
                .Include(d => d.MonitoredPorts)
                .Include(d => d.Certificates)
                .OrderByDescending(d => d.LastSeen)
                .ToListAsync();
            return Ok(devices);
        }

        // API: Get single device
        [HttpGet("api/devices/{id}")]
        public async Task<ActionResult<Device>> GetDevice(int id)
        {
            var device = await _context.Devices
                .Include(d => d.MonitoredPorts)
                .Include(d => d.Certificates)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (device == null)
            {
                return NotFound();
            }

            return Ok(device);
        }

        // API: Update device status manually
        [HttpPut("api/devices/{id}/status")]
        public async Task<IActionResult> UpdateDeviceStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            var device = await _context.Devices.FindAsync(id);
            if (device == null)
            {
                return NotFound();
            }

            device.Status = request.Status;
            device.UpdatedAt = DateTime.UtcNow;

            if (request.Status == DeviceStatus.Online)
            {
                device.LastSeen = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Device status updated successfully" });
        }

        // API: Ping device on demand
        [HttpPost("api/devices/{id}/ping")]
        public async Task<ActionResult<PingResult>> PingDeviceById(int id)
        {
            var device = await _context.Devices.FindAsync(id);
            if (device == null)
            {
                return NotFound();
            }

            var pingResult = await PingDevice(device.IpAddress);

            // Update device status based on ping result
            device.Status = pingResult.IsReachable ? DeviceStatus.Online : DeviceStatus.Offline;
            device.UpdatedAt = DateTime.UtcNow;
            if (pingResult.IsReachable)
            {
                device.LastSeen = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(pingResult);
        }

        // API: Bulk import devices
        [HttpPost("api/bulk-import")]
        public async Task<ActionResult<BulkImportResponse>> BulkImportDevices([FromBody] BulkImportRequest request)
        {
            var results = new List<BulkImportResult>();
            var successCount = 0;

            foreach (var deviceData in request.Devices)
            {
                try
                {
                    // Validate IP
                    if (!IPAddress.TryParse(deviceData.IpAddress, out _))
                    {
                        results.Add(new BulkImportResult
                        {
                            IpAddress = deviceData.IpAddress,
                            Success = false,
                            Error = "Invalid IP address format"
                        });
                        continue;
                    }

                    // Check if exists
                    var exists = await _context.Devices.AnyAsync(d => d.IpAddress == deviceData.IpAddress);
                    if (exists)
                    {
                        results.Add(new BulkImportResult
                        {
                            IpAddress = deviceData.IpAddress,
                            Success = false,
                            Error = "Device already exists"
                        });
                        continue;
                    }

                    // Create device
                    var device = new Device
                    {
                        Hostname = deviceData.Hostname ?? deviceData.IpAddress,
                        IpAddress = deviceData.IpAddress,
                        Status = DeviceStatus.Unknown,
                        LastSeen = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Devices.Add(device);
                    await _context.SaveChangesAsync();

                    results.Add(new BulkImportResult
                    {
                        IpAddress = deviceData.IpAddress,
                        Success = true,
                        DeviceId = device.Id
                    });

                    successCount++;

                    // Start background ping for each device
                    _ = Task.Run(async () => await UpdateDeviceStatus(device.Id));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error importing device {IpAddress}", deviceData.IpAddress);
                    results.Add(new BulkImportResult
                    {
                        IpAddress = deviceData.IpAddress,
                        Success = false,
                        Error = "Internal error during import"
                    });
                }
            }

            return Ok(new BulkImportResponse
            {
                TotalRequested = request.Devices.Count,
                SuccessfullyAdded = successCount,
                Results = results
            });
        }

        #endregion

        #region Helper Methods

        private async Task<PingResult> PingDevice(string ipAddress)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ipAddress, 3000);

                return new PingResult
                {
                    IsReachable = reply.Status == IPStatus.Success,
                    ResponseTime = reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : 0,
                    Status = reply.Status.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ping failed for {IpAddress}", ipAddress);
                return new PingResult
                {
                    IsReachable = false,
                    ResponseTime = 0,
                    Status = "Failed"
                };
            }
        }

        private async Task<bool> IsPortOpen(string host, int port, TimeSpan timeout)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(timeout);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == connectTask && client.Connected)
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private string GetServiceName(int port)
        {
            return port switch
            {
                22 => "SSH",
                23 => "Telnet",
                25 => "SMTP",
                53 => "DNS",
                80 => "HTTP",
                110 => "POP3",
                143 => "IMAP",
                443 => "HTTPS",
                993 => "IMAPS",
                995 => "POP3S",
                3389 => "RDP",
                5985 => "WinRM HTTP",
                5986 => "WinRM HTTPS",
                _ => "Unknown"
            };
        }

        private async Task ScanDevicePorts(int deviceId)
        {
            // Background port scanning logic
            var device = await _context.Devices.FindAsync(deviceId);
            if (device == null) return;

            var commonPorts = new[] { 22, 23, 25, 53, 80, 110, 143, 443, 993, 995, 3389, 5985, 5986 };

            foreach (var port in commonPorts)
            {
                var isOpen = await IsPortOpen(device.IpAddress, port, TimeSpan.FromSeconds(2));
                if (isOpen)
                {
                    var existingPort = await _context.MonitoredPorts
                        .FirstOrDefaultAsync(p => p.DeviceId == deviceId && p.Port == port);

                    if (existingPort == null)
                    {
                        _context.MonitoredPorts.Add(new MonitoredPort
                        {
                            DeviceId = deviceId,
                            Port = port,
                            Protocol = "TCP",
                            ServiceName = GetServiceName(port),
                            IsEnabled = true,
                            Status = PortStatus.Open,
                            LastChecked = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task UpdateDeviceStatus(int deviceId)
        {
            var device = await _context.Devices.FindAsync(deviceId);
            if (device == null) return;

            var pingResult = await PingDevice(device.IpAddress);
            device.Status = pingResult.IsReachable ? DeviceStatus.Online : DeviceStatus.Offline;
            device.LastSeen = DateTime.UtcNow;
            device.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        #endregion
    }

    #region DTOs

    // Request/Response DTOs
    public class UpdateStatusRequest
    {
        [Required]
        public DeviceStatus Status { get; set; }
    }

    public class AddDeviceRequest
    {
        [Required]
        public string IpAddress { get; set; } = string.Empty;
        public string? Hostname { get; set; }
        public List<PortRequest>? MonitoredPorts { get; set; }
    }

    public class PortRequest
    {
        public int Port { get; set; }
        public string? Protocol { get; set; }
        public string? ServiceName { get; set; }
    }

    public class QuickAddRequest
    {
        [Required]
        public string IpAddress { get; set; } = string.Empty;
    }

    public class BulkImportRequest
    {
        public List<BulkDeviceData> Devices { get; set; } = new();
    }

    public class BulkDeviceData
    {
        public string IpAddress { get; set; } = string.Empty;
        public string? Hostname { get; set; }
    }

    public class AddDeviceResponse
    {
        public bool Success { get; set; }
        public int DeviceId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string InitialStatus { get; set; } = string.Empty;
        public PingResult PingResponse { get; set; } = new();
    }

    public class QuickAddResponse
    {
        public bool Success { get; set; }
        public int DeviceId { get; set; }
        public string Hostname { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<int> OpenPorts { get; set; } = new();
        public PingResult PingResult { get; set; } = new();
    }

    public class BulkImportResponse
    {
        public int TotalRequested { get; set; }
        public int SuccessfullyAdded { get; set; }
        public List<BulkImportResult> Results { get; set; } = new();
    }

    public class BulkImportResult
    {
        public string IpAddress { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int DeviceId { get; set; }
        public string? Error { get; set; }
    }

    public class PingResult
    {
        public bool IsReachable { get; set; }
        public int ResponseTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    #endregion
}