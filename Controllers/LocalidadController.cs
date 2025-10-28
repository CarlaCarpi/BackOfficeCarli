using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    public class LocalidadController : Controller
    {
        private readonly IHttpClientFactory _http;

        public LocalidadController(IHttpClientFactory http)
        {
            _http = http;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");

            // ✅ Usar ruta relativa con barra inicial (se suma a BaseAddress)
            var resp = await client.GetAsync("/api/Localidad");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al obtener localidades: {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}";
                return View(Enumerable.Empty<Localidad>());
            }

            var json = await resp.Content.ReadAsStringAsync();

            // ✅ Deserializar a Localidad (no Provincia)
            var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? Enumerable.Empty<Localidad>();

            return View(localidades);
        }
    }
}
