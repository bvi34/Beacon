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

        // Beacon entities
        public DbSet<Device> Devices { get; set; }
        public DbSet<MonitoredPort> MonitoredPorts { get; set; }
        public DbSet<Certificate> Certificates { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Device configuration
            builder.Entity<Device>(entity =>
            {
                entity.HasIndex(e => e.IpAddress).IsUnique();
                entity.HasIndex(e => e.Hostname);
            });

            // MonitoredPort configuration
            builder.Entity<MonitoredPort>(entity =>
            {
                entity.HasIndex(e => new { e.DeviceId, e.Port, e.Protocol }).IsUnique();
                entity.HasOne(d => d.Device)
                      .WithMany(p => p.MonitoredPorts)
                      .HasForeignKey(d => d.DeviceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Certificate configuration
            builder.Entity<Certificate>(entity =>
            {
                entity.HasIndex(e => e.Thumbprint);
                entity.HasOne(d => d.Device)
                      .WithMany(p => p.Certificates)
                      .HasForeignKey(d => d.DeviceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}