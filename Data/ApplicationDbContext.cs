using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Beacon.Models;

namespace Beacon.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Device monitoring entities (existing)
        public DbSet<Device> Devices { get; set; }
        public DbSet<MonitoredPort> MonitoredPorts { get; set; }

        // URL Monitoring entities (new)
        public DbSet<UrlMonitor> UrlMonitors { get; set; }

        // Certificates - shared between devices and URLs
        public DbSet<Certificate> Certificates { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Device configuration (existing)
            builder.Entity<Device>(entity =>
            {
                entity.HasIndex(e => e.IpAddress).IsUnique();
                entity.HasIndex(e => e.Hostname);
            });

            // MonitoredPort configuration (existing)
            builder.Entity<MonitoredPort>(entity =>
            {
                entity.HasIndex(e => new { e.DeviceId, e.Port, e.Protocol }).IsUnique();
                entity.HasOne(d => d.Device)
                      .WithMany(p => p.MonitoredPorts)
                      .HasForeignKey(d => d.DeviceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // UrlMonitor configuration (new)
            builder.Entity<UrlMonitor>(entity =>
            {
                entity.HasIndex(e => e.Url);
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.IsActive);

                entity.HasOne(u => u.Certificate)
                      .WithMany(c => c.UrlMonitors)
                      .HasForeignKey(u => u.CertificateId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Certificate configuration - updated to support both devices and URLs
            builder.Entity<Certificate>(entity =>
            {
                entity.HasIndex(e => e.Thumbprint);
                entity.HasIndex(e => e.CommonName);
                entity.HasIndex(e => e.ExpiryDate);
                entity.HasIndex(e => e.Status);

                // Optional relationship to Device (for device certificates)
                entity.HasOne(c => c.Device)
                      .WithMany(d => d.Certificates)
                      .HasForeignKey(c => c.DeviceId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired(false); // Make DeviceId optional since URLs won't have devices
            });
        }
    }
};