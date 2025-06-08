using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Beacon.Data;
using Beacon.Models;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Services
{
    // FIXED: Interface should be named INetworkDiscoveryService (with I prefix)
    public interface INetworkDiscoveryService
    {
        Task<List<DiscoveredDevice>> DiscoverDevicesAsync(string networkRange);
        Task<List<DiscoveredDevice>> DiscoverLocalNetworkAsync();
        Task<DiscoveredDevice?> ProbeDeviceAsync(string ipAddress);
        Task<bool> AddDiscoveredDeviceAsync(DiscoveredDevice discoveredDevice);
    }

    // FIXED: Class should be named NetworkDiscoveryService (without I prefix) and implement INetworkDiscoveryService
    public class NetworkDiscoveryService : INetworkDiscoveryService
    {
        private readonly ApplicationDbContext _context;
        // FIXED: Logger should be for NetworkDiscoveryService, not INetworkDiscoveryService
        private readonly ILogger<NetworkDiscoveryService> _logger;

        // Common ports to check for service identification
        private readonly Dictionary<int, string> _commonPorts = new()
        {
            { 22, "SSH" },
            { 23, "Telnet" },
            { 25, "SMTP" },
            { 53, "DNS" },
            { 80, "HTTP" },
            { 110, "POP3" },
            { 143, "IMAP" },
            { 443, "HTTPS" },
            { 993, "IMAPS" },
            { 995, "POP3S" },
            { 3389, "RDP" },
            { 5432, "PostgreSQL" },
            { 3306, "MySQL" },
            { 1433, "SQL Server" },
            { 21, "FTP" },
            { 161, "SNMP" }
        };

        // FIXED: Constructor parameter should be ILogger<NetworkDiscoveryService>
        public NetworkDiscoveryService(ApplicationDbContext context, ILogger<NetworkDiscoveryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<DiscoveredDevice>> DiscoverLocalNetworkAsync()
        {
            var localIp = GetLocalIPAddress();
            if (localIp == null)
            {
                _logger.LogWarning("Could not determine local IP address");
                return new List<DiscoveredDevice>();
            }

            var networkRange = GetNetworkRange(localIp);
            return await DiscoverDevicesAsync(networkRange);
        }

        public async Task<List<DiscoveredDevice>> DiscoverDevicesAsync(string networkRange)
        {
            var discoveredDevices = new List<DiscoveredDevice>();
            var ipAddresses = ParseNetworkRange(networkRange);

            _logger.LogInformation($"Starting network discovery for {ipAddresses.Count} addresses in range {networkRange}");

            // Use parallel processing but limit concurrency to avoid overwhelming the network
            var semaphore = new SemaphoreSlim(20); // Max 20 concurrent scans
            var tasks = ipAddresses.Select(async ip =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var device = await ProbeDeviceAsync(ip);
                    if (device != null)
                    {
                        lock (discoveredDevices)
                        {
                            discoveredDevices.Add(device);
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation($"Discovery completed. Found {discoveredDevices.Count} devices");
            return discoveredDevices.OrderBy(d => IPAddress.Parse(d.IpAddress).GetAddressBytes()[3]).ToList();
        }

        public async Task<DiscoveredDevice?> ProbeDeviceAsync(string ipAddress)
        {
            try
            {
                // First, try to ping the device
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ipAddress, 3000);

                if (reply.Status != IPStatus.Success)
                {
                    return null;
                }

                var device = new DiscoveredDevice
                {
                    IpAddress = ipAddress,
                    IsResponding = true,
                    ResponseTime = reply.RoundtripTime,
                    DiscoveredAt = DateTime.UtcNow
                };

                // Try to resolve hostname
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                    device.Hostname = hostEntry.HostName;
                }
                catch
                {
                    device.Hostname = $"Unknown-{ipAddress}";
                }

                // Probe common ports to identify services
                device.OpenPorts = await ProbePortsAsync(ipAddress);
                device.DeviceType = DetermineDeviceType(device.OpenPorts, device.Hostname);

                return device;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Error probing device {ipAddress}: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> AddDiscoveredDeviceAsync(DiscoveredDevice discoveredDevice)
        {
            try
            {
                // Check if device already exists
                var existingDevice = await _context.Devices
                    .FirstOrDefaultAsync(d => d.IpAddress == discoveredDevice.IpAddress);

                if (existingDevice != null)
                {
                    // Update existing device
                    existingDevice.Hostname = discoveredDevice.Hostname;
                    existingDevice.Status = DeviceStatus.Online;
                    existingDevice.LastSeen = DateTime.UtcNow;
                    existingDevice.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Add new device
                    var newDevice = new Device
                    {
                        Hostname = discoveredDevice.Hostname,
                        IpAddress = discoveredDevice.IpAddress,
                        Status = DeviceStatus.Online,
                        LastSeen = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Devices.Add(newDevice);
                    await _context.SaveChangesAsync();

                    // Add monitored ports for discovered open ports
                    foreach (var port in discoveredDevice.OpenPorts)
                    {
                        var monitoredPort = new MonitoredPort
                        {
                            DeviceId = newDevice.Id,
                            Port = port.Port,
                            Protocol = port.Protocol,
                            ServiceName = port.Service,
                            IsEnabled = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.MonitoredPorts.Add(monitoredPort);
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding discovered device {discoveredDevice.IpAddress}");
                return false;
            }
        }

        private async Task<List<DiscoveredPort>> ProbePortsAsync(string ipAddress)
        {
            var openPorts = new List<DiscoveredPort>();
            var semaphore = new SemaphoreSlim(10); // Limit concurrent port scans

            var tasks = _commonPorts.Select(async kvp =>
            {
                await semaphore.WaitAsync();
                try
                {
                    if (await IsPortOpenAsync(ipAddress, kvp.Key))
                    {
                        lock (openPorts)
                        {
                            openPorts.Add(new DiscoveredPort
                            {
                                Port = kvp.Key,
                                Service = kvp.Value,
                                Protocol = "TCP"
                            });
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return openPorts;
        }

        private async Task<bool> IsPortOpenAsync(string host, int port, int timeout = 2000)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(timeout);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                return completedTask == connectTask && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        private string DetermineDeviceType(List<DiscoveredPort> openPorts, string hostname)
        {
            var portNumbers = openPorts.Select(p => p.Port).ToHashSet();

            // Check hostname patterns
            var hostnameLower = hostname.ToLower();
            if (hostnameLower.Contains("router") || hostnameLower.Contains("gateway"))
                return "Router/Gateway";
            if (hostnameLower.Contains("switch"))
                return "Network Switch";
            if (hostnameLower.Contains("server") || hostnameLower.Contains("srv"))
                return "Server";
            if (hostnameLower.Contains("printer"))
                return "Printer";

            // Check based on open ports
            if (portNumbers.Contains(80) || portNumbers.Contains(443))
            {
                if (portNumbers.Contains(22) || portNumbers.Contains(3389))
                    return "Web Server";
                return "Web Service";
            }

            if (portNumbers.Contains(3306) || portNumbers.Contains(5432) || portNumbers.Contains(1433))
                return "Database Server";

            if (portNumbers.Contains(22) || portNumbers.Contains(3389))
                return "Server";

            if (portNumbers.Contains(161)) // SNMP
                return "Network Device";

            if (portNumbers.Contains(25) || portNumbers.Contains(110) || portNumbers.Contains(143))
                return "Mail Server";

            return openPorts.Any() ? "Network Device" : "Unknown Device";
        }

        private IPAddress? GetLocalIPAddress()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address;
            }
            catch
            {
                return null;
            }
        }

        private string GetNetworkRange(IPAddress localIp)
        {
            var bytes = localIp.GetAddressBytes();
            return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.1-254";
        }

        private List<string> ParseNetworkRange(string networkRange)
        {
            var ipAddresses = new List<string>();

            try
            {
                if (networkRange.Contains("-"))
                {
                    // Handle range like "192.168.1.1-254"
                    var parts = networkRange.Split('-');
                    var baseParts = parts[0].Split('.');
                    var startIp = int.Parse(baseParts[3]);
                    var endIp = int.Parse(parts[1]);

                    var baseNetwork = $"{baseParts[0]}.{baseParts[1]}.{baseParts[2]}";

                    for (int i = startIp; i <= endIp; i++)
                    {
                        ipAddresses.Add($"{baseNetwork}.{i}");
                    }
                }
                else if (networkRange.Contains("/"))
                {
                    // Handle CIDR notation like "192.168.1.0/24"
                    var parts = networkRange.Split('/');
                    var baseIp = IPAddress.Parse(parts[0]);
                    var prefixLength = int.Parse(parts[1]);

                    // Simple /24 implementation for now
                    if (prefixLength == 24)
                    {
                        var bytes = baseIp.GetAddressBytes();
                        var baseNetwork = $"{bytes[0]}.{bytes[1]}.{bytes[2]}";
                        for (int i = 1; i <= 254; i++)
                        {
                            ipAddresses.Add($"{baseNetwork}.{i}");
                        }
                    }
                }
                else
                {
                    // Single IP
                    ipAddresses.Add(networkRange);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing network range: {networkRange}");
            }

            return ipAddresses;
        }
    }

    // DTOs for discovery results
    public class DiscoveredDevice
    {
        public string IpAddress { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public bool IsResponding { get; set; }
        public long ResponseTime { get; set; }
        public List<DiscoveredPort> OpenPorts { get; set; } = new();
        public string DeviceType { get; set; } = "Unknown";
        public DateTime DiscoveredAt { get; set; }
        public bool AlreadyExists { get; set; }
    }

    public class DiscoveredPort
    {
        public int Port { get; set; }
        public string Protocol { get; set; } = "TCP";
        public string Service { get; set; } = string.Empty;
    }
}