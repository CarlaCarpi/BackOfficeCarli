using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using santa_ramona_BackOffice.Models;
using SantaRamona.Backoffice.Models;
using System.Diagnostics;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
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

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult IndexPublic()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

      
    }
}
