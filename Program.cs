using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Beacon.Data;
using Beacon.Models;
using Beacon.Services;

var builder = WebApplication.CreateBuilder(args);

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=beacon.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Register your network discovery service (only once)
builder.Services.AddScoped<INetworkDiscoveryService, NetworkDiscoveryService>();

// Register monitoring services
builder.Services.AddHttpClient<IUrlMonitoringService, UrlMonitoringService>();
builder.Services.AddScoped<IUrlMonitoringService, UrlMonitoringService>();
builder.Services.AddHostedService<BackgroundMonitoringService>();


// Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// MVC and Blazor
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// BUILD THE APP - Do this AFTER all service registrations
var app = builder.Build();
app.UseStaticFiles(); // Add this if it's not present

// Ensure database is created and seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();

    // Seed test data in development
    if (app.Environment.IsDevelopment() && !context.Devices.Any())
    {
        context.Devices.Add(new Device
        {
            Hostname = "localhost",
            IpAddress = "127.0.0.1",
            Status = DeviceStatus.Online,
            LastSeen = DateTime.UtcNow
        });
        context.Devices.Add(new Device
        {
            Hostname = "google.com",
            IpAddress = "8.8.8.8",
            Status = DeviceStatus.Unknown,
            LastSeen = DateTime.UtcNow.AddMinutes(-5)
        });
        context.SaveChanges();
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapBlazorHub();

app.Run();