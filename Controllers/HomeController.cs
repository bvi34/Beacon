using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Beacon.Data;
using Beacon.Models;

namespace Beacon.Controllers
{
    public class HomeController : Controller
    {
		private readonly ApplicationDbContext _context;
        public HomeController(ApplicationDbContext context)
        {
			_context = context;
		}
		public async Task<IActionResult> Index()
		{
			var devices = await _context.Devices
				.Include(d => d.MonitoredPorts)
				.Include(d => d.Certificates)
				.ToListAsync();

			return View(devices);
		}

		public IActionResult Discovery()
        {
            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }

    }
}