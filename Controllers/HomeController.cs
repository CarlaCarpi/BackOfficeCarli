using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using santa_ramona_BackOffice.Models;
using SantaRamona.Backoffice.Models;
using System.Diagnostics;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    // Prefijo del panel
    [Route("admin/santa/back")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IHttpClientFactory _http;

        // Único constructor para inyección de dependencias
        public HomeController(IHttpClientFactory http, ILogger<HomeController> logger)
        {
            _http = http;
            _logger = logger;
        }

        //public HomeController(ILogger<HomeController> logger) => _logger = logger;

        // GET /admin/santa/back
        [HttpGet("")]
        public IActionResult Index() => View();


        // GET /admin/santa/back/privacy
        [HttpGet("privacy")]
        public IActionResult Privacy() => View();


        //public IActionResult Privacy()
        //{
        //    return View();
        //}

        public IActionResult IndexPublic()
        {
            return View();
        }

        // GET /admin/santa/back/error
        [HttpGet("error")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

      

        
        //public IActionResult Error()
        //    => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    }
}
