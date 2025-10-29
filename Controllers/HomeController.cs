using Microsoft.AspNetCore.Mvc;
using santa_ramona_BackOffice.Models;
using SantaRamona.Backoffice.Models;
using System.Diagnostics;

namespace SantaRamona.Backoffice.Controllers
{
    // Prefijo del panel
    [Route("admin/santa/back")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        public HomeController(ILogger<HomeController> logger) => _logger = logger;

        // GET /admin/santa/back
        [HttpGet("")]
        public IActionResult Index() => View();

        // GET /admin/santa/back/privacy
        [HttpGet("privacy")]
        public IActionResult Privacy() => View();

        // GET /admin/santa/back/error
        [HttpGet("error")]
        public IActionResult Error()
            => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
