using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    public class DonacionController : Controller
    {
        private readonly IHttpClientFactory _http;
        public DonacionController(IHttpClientFactory http) => _http = http;

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/donacion");

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"Error API: {(int)resp.StatusCode} - {resp.ReasonPhrase}";
                return View(new List<Donacion>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var donaciones = JsonSerializer.Deserialize<List<Donacion>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return View("Index", donaciones ?? new List<Donacion>());
        }

        // ===================== CREAR =====================
        [HttpGet]
        public IActionResult Crear()
        {
            if (TempData["Ok"] is string ok)
                ViewBag.MensajeExito = ok;

            return View(new Donacion());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] string tipo, [FromForm] string descripcion)
        {
            // Validaciones mínimas
            if (string.IsNullOrWhiteSpace(tipo) || !(tipo == "M" || tipo == "I"))
                ModelState.AddModelError("tipo", "Debe seleccionar un tipo válido (M = Medicamentos, I = Insumos).");

            if (string.IsNullOrWhiteSpace(descripcion))
                ModelState.AddModelError("descripcion", "La descripción es obligatoria.");

            if (!ModelState.IsValid)
                return View(new Donacion { tipo = tipo ?? string.Empty, descripcion = descripcion ?? string.Empty });

            var model = new Donacion { tipo = tipo, descripcion = descripcion };

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/donacion", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/donacion -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            TempData["Ok"] = "Donación creada correctamente.";
            return RedirectToAction(nameof(Crear));
        }

        // ===================== MODIFICAR =====================
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/donacion/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/donacion/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Donacion>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (TempData["Ok"] is string ok)
                ViewBag.MensajeExito = ok;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] int id_donacion, [FromForm] string tipo, [FromForm] string descripcion)
        {
            if (string.IsNullOrWhiteSpace(tipo) || !(tipo == "M" || tipo == "I"))
                ModelState.AddModelError("tipo", "Debe seleccionar un tipo válido (M = Medicamentos, I = Insumos).");

            if (string.IsNullOrWhiteSpace(descripcion))
                ModelState.AddModelError("descripcion", "La descripción es obligatoria.");

            if (!ModelState.IsValid)
                return View(new Donacion { id_donacion = id_donacion, tipo = tipo ?? string.Empty, descripcion = descripcion ?? string.Empty });

            var model = new Donacion { id_donacion = id_donacion, tipo = tipo, descripcion = descripcion };

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/donacion/{id_donacion}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/donacion/{id_donacion} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            TempData["Ok"] = "Donación actualizada correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = id_donacion });
        }

        // ===================== ELIMINAR =====================
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var r = await client.GetAsync($"/api/donacion/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = r.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "La donación no existe o ya fue eliminada."
                    : $"No se pudo obtener la donación (código {(int)r.StatusCode}).";
                return RedirectToAction(nameof(Index));
            }

            var model = await r.Content.ReadFromJsonAsync<Donacion>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return View(model!);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"/api/donacion/{id}");

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Donación eliminada correctamente.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "No se pudo eliminar la donación. Intentalo nuevamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
