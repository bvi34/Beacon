using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Beacon.Models;
using Beacon.Data;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Services
{
    public interface IUrlMonitoringService
    {
        Task<UrlMonitorResult> CheckUrlAsync(UrlMonitor urlMonitor);
        Task<CertificateResult> CheckSslCertificateAsync(string url);
        Task RunMonitoringCycleAsync();
        Task<MonitoringStats> GetMonitoringStatsAsync();
        Task<List<UrlMonitor>> GetAllMonitorsAsync();
    }

    public class UrlMonitoringService : IUrlMonitoringService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UrlMonitoringService> _logger;

        public UrlMonitoringService(HttpClient httpClient, IServiceProvider serviceProvider, ILogger<UrlMonitoringService> logger)
        {
            _httpClient = httpClient;
            _serviceProvider = serviceProvider;
            _logger = logger;

            // Configure HttpClient
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Beacon-Monitor/1.0");
        }

        public async Task<UrlMonitorResult> CheckUrlAsync(UrlMonitor urlMonitor)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new UrlMonitorResult { UrlMonitorId = urlMonitor.Id };

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(urlMonitor.TimeoutSeconds));
                using var response = await _httpClient.GetAsync(urlMonitor.Url, cts.Token);

                stopwatch.Stop();

                result.Success = response.IsSuccessStatusCode;
                result.StatusCode = (int)response.StatusCode;
                result.ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                result.Status = response.IsSuccessStatusCode ? UrlStatus.Up : UrlStatus.Down;
                result.CheckedAt = DateTime.UtcNow;

                // Check SSL certificate if it's HTTPS and monitoring is enabled
                if (urlMonitor.IsHttps && urlMonitor.MonitorSsl)
                {
                    result.CertificateResult = await CheckSslCertificateAsync(urlMonitor.Url);
                }

                _logger.LogInformation($"URL check completed for {urlMonitor.Url}: {result.Status} ({result.StatusCode}) in {result.ResponseTimeMs:F2}ms");
            }
            catch (TaskCanceledException)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Status = UrlStatus.Timeout;
                result.Error = "Request timed out";
                result.ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                result.CheckedAt = DateTime.UtcNow;

                _logger.LogWarning($"URL check timed out for {urlMonitor.Url} after {urlMonitor.TimeoutSeconds} seconds");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Status = UrlStatus.Error;
                result.Error = ex.Message;
                result.ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                result.CheckedAt = DateTime.UtcNow;

                _logger.LogError(ex, $"Error checking URL {urlMonitor.Url}");
            }

            return result;
        }

        public async Task<CertificateResult> CheckSslCertificateAsync(string url)
        {
            var result = new CertificateResult();

            try
            {
                var uri = new Uri(url);
                var request = WebRequest.Create($"https://{uri.Host}:{(uri.Port == -1 ? 443 : uri.Port)}");

                if (request is HttpWebRequest httpRequest)
                {
                    httpRequest.Method = "HEAD";
                    httpRequest.Timeout = 10000; // 10 second timeout
                    httpRequest.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        if (certificate is X509Certificate2 cert)
                        {
                            result.Success = true;
                            result.CommonName = cert.Subject;
                            result.Issuer = cert.Issuer;
                            result.IssuedDate = cert.NotBefore;
                            result.ExpiryDate = cert.NotAfter;
                            result.Thumbprint = cert.Thumbprint;
                            result.Algorithm = cert.SignatureAlgorithm.FriendlyName ?? cert.SignatureAlgorithm.Value ?? "Unknown";
                            result.KeySize = cert.PublicKey.Key.KeySize;
                            result.IsExpired = DateTime.UtcNow > cert.NotAfter;
                            result.IsExpiringSoon = DateTime.UtcNow.AddDays(30) > cert.NotAfter && !result.IsExpired;
                            result.DaysUntilExpiry = (int)(cert.NotAfter - DateTime.UtcNow).TotalDays;

                            // Determine status
                            if (result.IsExpired)
                                result.Status = CertificateStatus.Expired;
                            else if (result.IsExpiringSoon)
                                result.Status = CertificateStatus.ExpiringSoon;
                            else if (sslPolicyErrors == SslPolicyErrors.None)
                                result.Status = CertificateStatus.Valid;
                            else
                                result.Status = CertificateStatus.Invalid;
                        }

                        return true; // Always return true to avoid throwing, we handle validation ourselves
                    };

                    using var response = await httpRequest.GetResponseAsync();
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                result.Status = CertificateStatus.Invalid;
                _logger.LogError(ex, $"Error checking SSL certificate for {url}");
            }

            return result;
        }

        public async Task RunMonitoringCycleAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); // Replace with your actual DbContext

            var activeMonitors = await dbContext.UrlMonitors
                .Where(m => m.IsActive)
                .Include(m => m.Certificate)
                .ToListAsync();

            var tasks = activeMonitors.Select(async monitor =>
            {
                try
                {
                    var result = await CheckUrlAsync(monitor);
                    await UpdateMonitorAsync(dbContext, monitor, result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to process monitor {monitor.Id}");
                }
            });

            await Task.WhenAll(tasks);
            await dbContext.SaveChangesAsync();
        }

        private async Task UpdateMonitorAsync(ApplicationDbContext dbContext, UrlMonitor monitor, UrlMonitorResult result)
        {
            // Update monitor status
            monitor.Status = result.Status;
            monitor.LastResponseCode = result.StatusCode;
            monitor.LastResponseTimeMs = result.ResponseTimeMs;
            monitor.LastChecked = result.CheckedAt;
            monitor.LastError = result.Error ?? string.Empty;
            monitor.UpdatedAt = DateTime.UtcNow;

            // Update statistics
            monitor.TotalChecks++;
            if (result.Success)
            {
                monitor.SuccessfulChecks++;
                monitor.LastUptime = result.CheckedAt;
                monitor.ConsecutiveFailures = 0;
            }
            else
            {
                monitor.LastDowntime = result.CheckedAt;
                monitor.ConsecutiveFailures++;
            }

            // Calculate uptime percentage
            monitor.UptimePercentage = monitor.TotalChecks > 0
                ? (double)monitor.SuccessfulChecks / monitor.TotalChecks * 100
                : 0;

            // Update or create certificate record if SSL check was performed
            if (result.CertificateResult != null && result.CertificateResult.Success)
            {
                var existingCert = monitor.Certificate;
                if (existingCert == null || existingCert.Thumbprint != result.CertificateResult.Thumbprint)
                {
                    // Create new certificate record
                    var newCert = new Certificate
                    {
                        CommonName = result.CertificateResult.CommonName ?? string.Empty,
                        Thumbprint = result.CertificateResult.Thumbprint ?? string.Empty,
                        IssuedDate = result.CertificateResult.IssuedDate,
                        ExpiryDate = result.CertificateResult.ExpiryDate,
                        Issuer = result.CertificateResult.Issuer ?? string.Empty,
                        Algorithm = result.CertificateResult.Algorithm ?? string.Empty,
                        KeySize = result.CertificateResult.KeySize,
                        Status = result.CertificateResult.Status,
                        LastChecked = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    dbContext.Certificates.Add(newCert);
                    monitor.Certificate = newCert;
                }
                else
                {
                    // Update existing certificate
                    existingCert.Status = result.CertificateResult.Status;
                    existingCert.LastChecked = DateTime.UtcNow;
                    existingCert.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        public async Task<List<UrlMonitor>> GetAllMonitorsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await dbContext.UrlMonitors
                .Include(m => m.Certificate)
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<MonitoringStats> GetMonitoringStatsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var monitors = await dbContext.UrlMonitors.ToListAsync();
            var certificates = await dbContext.Certificates.ToListAsync();

            return new MonitoringStats
            {
                TotalMonitors = monitors.Count,
                ActiveMonitors = monitors.Count(m => m.IsActive),
                UpMonitors = monitors.Count(m => m.Status == UrlStatus.Up),
                DownMonitors = monitors.Count(m => m.Status == UrlStatus.Down),
                UnknownMonitors = monitors.Count(m => m.Status == UrlStatus.Unknown),
                TotalCertificates = certificates.Count,
                ValidCertificates = certificates.Count(c => c.Status == CertificateStatus.Valid),
                ExpiredCertificates = certificates.Count(c => c.Status == CertificateStatus.Expired),
                ExpiringSoonCertificates = certificates.Count(c => c.Status == CertificateStatus.ExpiringSoon),
                AverageResponseTime = monitors.Where(m => m.LastResponseTimeMs.HasValue)
                                            .Average(m => m.LastResponseTimeMs.Value),
                OverallUptimePercentage = monitors.Any() ? monitors.Average(m => m.UptimePercentage) : 0
            };
        }
    }

    // Result classes
    public class UrlMonitorResult
    {
        public int UrlMonitorId { get; set; }
        public bool Success { get; set; }
        public UrlStatus Status { get; set; }
        public int? StatusCode { get; set; }
        public double ResponseTimeMs { get; set; }
        public string? Error { get; set; }
        public DateTime CheckedAt { get; set; }
        public CertificateResult? CertificateResult { get; set; }
    }

    public class CertificateResult
    {
        public bool Success { get; set; }
        public string? CommonName { get; set; }
        public string? Issuer { get; set; }
        public DateTime IssuedDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string? Thumbprint { get; set; }
        public string? Algorithm { get; set; }
        public int KeySize { get; set; }
        public bool IsExpired { get; set; }
        public bool IsExpiringSoon { get; set; }
        public int DaysUntilExpiry { get; set; }
        public CertificateStatus Status { get; set; }
        public string? Error { get; set; }
    }

    public class MonitoringStats
    {
        public int TotalMonitors { get; set; }
        public int ActiveMonitors { get; set; }
        public int UpMonitors { get; set; }
        public int DownMonitors { get; set; }
        public int UnknownMonitors { get; set; }
        public int TotalCertificates { get; set; }
        public int ValidCertificates { get; set; }
        public int ExpiredCertificates { get; set; }
        public int ExpiringSoonCertificates { get; set; }
        public double AverageResponseTime { get; set; }
        public double OverallUptimePercentage { get; set; }
    }
}