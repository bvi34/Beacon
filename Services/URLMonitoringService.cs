using Beacon.Data;
using Beacon.Models;
using Beacon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Threading;
using System.Xml.Linq;
using System;
  
  namespace Beacon.Services
  {
      public interface IUrlMonitoringService
      {
          Task<List<UrlMonitor>> GetAllMonitorsAsync();
          Task<UrlMonitor> AddMonitorAsync(string url, string name, string description);
          Task UpdateMonitorAsync(UrlMonitor monitor);
          Task DeleteMonitorAsync(int id);
          Task<UrlMonitor> CheckUrlAsync(UrlMonitor monitor);
          Task CheckAllActiveUrlsAsync();
      }
  
      public class UrlMonitoringService : IUrlMonitoringService
      {
          private readonly ApplicationDbContext _context;
          private readonly ILogger<UrlMonitoringService> _logger;
          private readonly HttpClient _httpClient;
  
          public UrlMonitoringService(
              ApplicationDbContext context,
              ILogger<UrlMonitoringService> logger,
              HttpClient httpClient)
          {
              _context = context;
              _logger = logger;
              _httpClient = httpClient;
          }
  
          public async Task<List<UrlMonitor>> GetAllMonitorsAsync()
          {
                  return await _context.UrlMonitors
                      .Include(u => u.Certificate)
                      .ToListAsync();
              }
  
          public async Task<UrlMonitor> AddMonitorAsync(string url, string name, string description)
          {
      var monitor = new UrlMonitor
                  {
          Url = url,
  Name = name,
  Description = description,
  IsActive = true,
  CreatedAt = DateTime.UtcNow,
  UpdatedAt = DateTime.UtcNow
              };
      
      _context.UrlMonitors.Add(monitor);
      await _context.SaveChangesAsync();
                  return monitor;
              }
  
          public async Task UpdateMonitorAsync(UrlMonitor monitor)
          {
      monitor.UpdatedAt = DateTime.UtcNow;
      _context.UrlMonitors.Update(monitor);
      await _context.SaveChangesAsync();
              }
  
          public async Task DeleteMonitorAsync(int id)
          {
      var monitor = await _context.UrlMonitors.FindAsync(id);
                  if (monitor != null)
                      {
          _context.UrlMonitors.Remove(monitor);
          await _context.SaveChangesAsync();
                      }
              }
  
          public async Task<UrlMonitor> CheckUrlAsync(UrlMonitor monitor)
          {
                  try
              {
          var start = DateTime.UtcNow;
          var response = await _httpClient.GetAsync(monitor.Url);
          monitor.LastResponseCode = (int)response.StatusCode;
          monitor.LastResponseTimeMs = (DateTime.UtcNow - start).TotalMilliseconds;
          monitor.Status = response.IsSuccessStatusCode ? UrlStatus.Up : UrlStatus.Down;
          monitor.LastError = string.Empty;
                      }
                  catch (TaskCanceledException)
              {
          monitor.Status = UrlStatus.Timeout;
          monitor.LastError = "Request timed out";
                      }
                  catch (Exception ex)
              {
          monitor.Status = UrlStatus.Error;
          monitor.LastError = ex.Message;
          _logger.LogWarning(ex, "Error checking URL {Url}", monitor.Url);
                      }
      
      monitor.LastChecked = DateTime.UtcNow;
      monitor.UpdatedAt = DateTime.UtcNow;
      
      _context.UrlMonitors.Update(monitor);
      await _context.SaveChangesAsync();
                  return monitor;
              }
      
              public async Task CheckAllActiveUrlsAsync()
          {
          var monitors = await _context.UrlMonitors
                          .Where(m => m.IsActive)
                          .ToListAsync();
          
                      foreach (var monitor in monitors)
                          {
              await CheckUrlAsync(monitor);
                          }
                  }
          }
  }
