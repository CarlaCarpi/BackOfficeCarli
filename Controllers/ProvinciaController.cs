using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    public class ProvinciaController : Controller
    {
        private readonly IHttpClientFactory _http;

        public ProvinciaController(IHttpClientFactory http)
        {
            _http = http;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");

            // 👇 Cambiá esta URL si usás localhost en lugar del hosting
            var resp = await client.GetAsync("https://webapisantaramona.somee.com/api/Provincia");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al obtener provincias: {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}";
                return View(Enumerable.Empty<Provincia>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return View(provincias);
        }
    }
}