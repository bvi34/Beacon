using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Beacon.Models;
using Beacon.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Sockets;
using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Services
{
    public interface IUrlMonitoringService
    {
        Task<UrlMonitorResult> CheckUrlAsync(UrlMonitor urlMonitor);
        Task<CertificateResult> CheckSslCertificateAsync(string url);
        Task RunMonitoringCycleAsync();
        Task<MonitoringStats> GetMonitoringStatsAsync();
        Task<List<UrlMonitor>> GetAllMonitorsAsync();
        Task<string> TestSimpleUrlAsync(string url); // Added for debugging
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

            // Configure HttpClient with a longer timeout than individual requests
            _httpClient.Timeout = TimeSpan.FromMinutes(2); // Longer than any individual monitor timeout
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Beacon-Monitor/1.0");
        }


        public async Task<UrlMonitorResult> CheckUrlAsync(UrlMonitor urlMonitor)
        {
            _logger.LogInformation($"=== STARTING URL CHECK === Monitor ID: {urlMonitor.Id}, URL: {urlMonitor.Url}");

            var stopwatch = Stopwatch.StartNew();
            var result = new UrlMonitorResult { UrlMonitorId = urlMonitor.Id };

            try
            {
                _logger.LogInformation($"Creating timeout for {urlMonitor.TimeoutSeconds} seconds");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(urlMonitor.TimeoutSeconds));

                _logger.LogInformation($"Making HTTP request to {urlMonitor.Url}");
                using var response = await _httpClient.GetAsync(urlMonitor.Url, cts.Token);

                stopwatch.Stop();

                result.Success = response.IsSuccessStatusCode;
                result.StatusCode = (int)response.StatusCode;
                result.ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                result.Status = response.IsSuccessStatusCode ? UrlStatus.Up : UrlStatus.Down;
                result.CheckedAt = DateTime.UtcNow;

                _logger.LogInformation($"HTTP Request SUCCESS: {urlMonitor.Url} -> Status: {result.StatusCode}, Time: {result.ResponseTimeMs:F2}ms");

                // Check SSL certificate if it's HTTPS and monitoring is enabled
                if (urlMonitor.IsHttps && urlMonitor.MonitorSsl)
                {
                    _logger.LogInformation($"Starting SSL check for {urlMonitor.Url}");
                    result.CertificateResult = await CheckSslCertificateAsync(urlMonitor.Url);
                    _logger.LogInformation($"SSL check completed: Success={result.CertificateResult.Success}");
                }

                _logger.LogInformation($"=== URL CHECK COMPLETED === {urlMonitor.Url}: {result.Status} ({result.StatusCode}) in {result.ResponseTimeMs:F2}ms");
            }
            catch (TaskCanceledException ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Status = UrlStatus.Timeout;
                result.Error = $"Request timed out after {urlMonitor.TimeoutSeconds}s";
                result.ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                result.CheckedAt = DateTime.UtcNow;

                _logger.LogWarning($"=== TIMEOUT === {urlMonitor.Url} timed out after {urlMonitor.TimeoutSeconds}s. Actual time: {result.ResponseTimeMs:F2}ms. InnerException: {ex.InnerException?.Message}");
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Status = UrlStatus.Error;
                result.Error = $"HTTP Error: {ex.Message}";
                result.ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                result.CheckedAt = DateTime.UtcNow;

                _logger.LogError(ex, $"=== HTTP ERROR === {urlMonitor.Url}: {ex.Message}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Status = UrlStatus.Error;
                result.Error = ex.Message;
                result.ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                result.CheckedAt = DateTime.UtcNow;

                _logger.LogError(ex, $"=== GENERAL ERROR === {urlMonitor.Url}: {ex.GetType().Name} - {ex.Message}");
            }

            return result;
        }

        public async Task<CertificateResult> CheckSslCertificateAsync(string url)
        {
            var result = new CertificateResult();

            try
            {
                var uri = new Uri(url);
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 443;

                _logger.LogInformation($"Connecting to {host}:{port} for SSL check");

                using var tcpClient = new TcpClient();

                // Add timeout for TCP connection
                var connectTask = tcpClient.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));

                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    throw new TimeoutException($"TCP connection to {host}:{port} timed out");
                }

                using var sslStream = new SslStream(tcpClient.GetStream(), false, ValidateCertificate);
                await sslStream.AuthenticateAsClientAsync(host);

                if (sslStream.RemoteCertificate is X509Certificate rawCert)
                {
                    var cert = new X509Certificate2(rawCert);
                    result.Success = true;
                    result.CommonName = cert.Subject;
                    result.Issuer = cert.Issuer;
                    result.IssuedDate = cert.NotBefore;
                    result.ExpiryDate = cert.NotAfter;
                    result.Thumbprint = cert.Thumbprint;
                    result.Algorithm = cert.SignatureAlgorithm.FriendlyName ?? cert.SignatureAlgorithm.Value ?? "Unknown";

                    // Modern, non-obsolete method for key size
                    if (cert.GetRSAPublicKey() is RSA rsaKey)
                    {
                        result.KeySize = rsaKey.KeySize;
                    }

                    result.IsExpired = DateTime.UtcNow > cert.NotAfter;
                    result.IsExpiringSoon = DateTime.UtcNow.AddDays(30) > cert.NotAfter && !result.IsExpired;
                    result.DaysUntilExpiry = (int)(cert.NotAfter - DateTime.UtcNow).TotalDays;

                    result.Status = result.IsExpired
                        ? CertificateStatus.Expired
                        : result.IsExpiringSoon
                            ? CertificateStatus.ExpiringSoon
                            : CertificateStatus.Valid;

                    _logger.LogInformation($"SSL certificate retrieved successfully for {url}: {result.CommonName}, Expires: {result.ExpiryDate:yyyy-MM-dd}");
                }
                else
                {
                    result.Success = false;
                    result.Error = "No certificate found in SSL stream.";
                    result.Status = CertificateStatus.Invalid;
                    _logger.LogWarning($"No certificate found for {url}");
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

        private bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // For monitoring purposes, we want to capture certificate info even if there are errors
            // but we should log the errors
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                _logger.LogWarning($"SSL Policy Errors: {sslPolicyErrors}");
            }
            return true; // Accept for monitoring, but log issues
        }
		private async Task<Certificate> UpsertCertificateAsync(ApplicationDbContext dbContext, CertificateResult certResult)
		{
			// Try to find an existing cert by thumbprint
			var existing = await dbContext.Certificates.FirstOrDefaultAsync(c => c.Thumbprint == certResult.Thumbprint);

			if (existing != null)
			{
				existing.Status = certResult.Status;
				existing.LastChecked = DateTime.UtcNow;
				existing.UpdatedAt = DateTime.UtcNow;
				dbContext.Entry(existing).State = EntityState.Modified;
				return existing;
			}

			var newCert = new Certificate
			{
				CommonName = certResult.CommonName ?? string.Empty,
				Thumbprint = certResult.Thumbprint ?? string.Empty,
				IssuedDate = certResult.IssuedDate,
				ExpiryDate = certResult.ExpiryDate,
				Issuer = certResult.Issuer ?? string.Empty,
				Algorithm = certResult.Algorithm ?? string.Empty,
				KeySize = certResult.KeySize,
				Status = certResult.Status,
				LastChecked = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};

			dbContext.Certificates.Add(newCert);
			return newCert;
		}
		public async Task RunMonitoringCycleAsync()
        {
            _logger.LogInformation("=== STARTING MONITORING CYCLE ===");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

			var now = DateTime.UtcNow;
			var activeMonitors = await dbContext.UrlMonitors
				.Where(m => m.IsActive)
				.Include(m => m.Certificate)
				.ToListAsync();

			var dueMonitors = activeMonitors
				.Where(m => !m.LastChecked.HasValue ||
							(now - m.LastChecked.Value).TotalMinutes >= m.CheckIntervalMinutes)
				.ToList();


			_logger.LogInformation($"Found {activeMonitors.Count} active monitors to check");

            var tasks = activeMonitors.Select(async monitor =>
            {
                try
                {
                    _logger.LogInformation($"Processing monitor {monitor.Id} ({monitor.Name})");
                    var result = await CheckUrlAsync(monitor);
                    await UpdateMonitorAsync(dbContext, monitor, result);
                    _logger.LogInformation($"Successfully updated monitor {monitor.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"=== FAILED TO PROCESS MONITOR === {monitor.Id}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);

            try
            {
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("=== MONITORING CYCLE COMPLETED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"=== FAILED TO SAVE CHANGES === {ex.Message}");
                throw;
            }
        }

		private async Task UpdateMonitorAsync(ApplicationDbContext dbContext, UrlMonitor monitor, UrlMonitorResult result)
		{
			_logger.LogInformation($"Updating monitor {monitor.Id} with result: Success={result.Success}, Status={result.Status}");

			try
			{
				monitor.Status = result.Status;
				monitor.LastResponseCode = result.StatusCode;
				monitor.LastResponseTimeMs = result.ResponseTimeMs;
				monitor.LastChecked = result.CheckedAt;
				monitor.LastError = result.Error ?? string.Empty;
				monitor.UpdatedAt = DateTime.UtcNow;

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

				monitor.UptimePercentage = monitor.TotalChecks > 0
					? (double)monitor.SuccessfulChecks / monitor.TotalChecks * 100
					: 0;

				dbContext.Entry(monitor).State = EntityState.Modified;

				if (result.CertificateResult != null && result.CertificateResult.Success)
				{
					var certEntity = await UpsertCertificateAsync(dbContext, result.CertificateResult);
					monitor.Certificate = certEntity;
				}

				_logger.LogInformation($"Monitor {monitor.Id} update prepared successfully");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error updating monitor {monitor.Id}: {ex.Message}");
				throw;
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
            var devices = await dbContext.Devices.ToListAsync();
            // Get monitors with response times for average calculation
            var monitorsWithResponseTimes = monitors.Where(m => m.LastResponseTimeMs.HasValue).ToList();

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
				OnlineDevices = devices.Count(d => d.Status == DeviceStatus.Online),
				OfflineDevices = devices.Count(d => d.Status == DeviceStatus.Offline),
				DevicesWithIssues = devices.Count(d => d.Status == DeviceStatus.Warning || d.Status == DeviceStatus.Error || d.Status == DeviceStatus.Unknown),
				TotalDevices = devices.Count(),
				// Safe average calculation - returns 0 if no elements
				AverageResponseTime = monitorsWithResponseTimes.Any()
                    ? monitorsWithResponseTimes.Average(m => m.LastResponseTimeMs.Value)
                    : 0,

                // Safe uptime percentage calculation
                OverallUptimePercentage = monitors.Any()
                    ? monitors.Average(m => m.UptimePercentage)
                    : 0
            };
        }

        // Added for debugging purposes
        public async Task<string> TestSimpleUrlAsync(string url)
        {
            try
            {
                _logger.LogInformation($"Testing simple request to {url}");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.GetAsync(url, cts.Token);

                var result = $"Status: {response.StatusCode}, Success: {response.IsSuccessStatusCode}";
                _logger.LogInformation($"Simple test result: {result}");

                return result;
            }
            catch (Exception ex)
            {
                var error = $"Error: {ex.GetType().Name} - {ex.Message}";
                _logger.LogError(ex, $"Simple test failed: {error}");
                return error;
            }
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
        public int? TotalDevices { get; set; }
		public int? OnlineDevices { get; internal set; }
		public int? OfflineDevices { get; internal set; }
		public int? DevicesWithIssues { get; internal set; }
	}
}