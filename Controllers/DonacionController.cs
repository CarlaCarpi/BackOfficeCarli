using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    [Authorize(Policy = "Activo")]
    public class DonacionController : Controller
    {
        private readonly IHttpClientFactory _http;
        public DonacionController(IHttpClientFactory http) => _http = http;

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/donacion");

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"Error API: {(int)resp.StatusCode} - {resp.ReasonPhrase}";
                ViewBag.Page = 1;
                ViewBag.PageSize = pageSize;
                ViewBag.HasMore = false;
                return View(new List<Donacion>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var donaciones = JsonSerializer.Deserialize<List<Donacion>>(json, opciones) ?? new List<Donacion>();

            // Ordenar (de más nueva a más vieja)
            donaciones = donaciones.OrderByDescending(d => d.id_donacion).ToList();

            var total = donaciones.Count;
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var pageItems = donaciones
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            bool hasMore = (page * pageSize) < total;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.HasMore = hasMore;

            return View("Index", pageItems);
        }

        [HttpGet]
        public async Task<IActionResult> Mas(int page = 2, int pageSize = 20)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/donacion");

            if (!resp.IsSuccessStatusCode)
                return Content("");

            var json = await resp.Content.ReadAsStringAsync();
            var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var donaciones = JsonSerializer.Deserialize<List<Donacion>>(json, opciones) ?? new List<Donacion>();

            donaciones = donaciones.OrderByDescending(d => d.id_donacion).ToList();

            var total = donaciones.Count;
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var pageItems = donaciones
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            bool hasMore = (page * pageSize) < total;
            Response.Headers["X-HasMore"] = hasMore ? "true" : "false";

            return PartialView("_DonacionRows", pageItems);
        }


        // ===================== CREAR =====================
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public IActionResult Crear()
        {
            if (TempData["Ok"] is string ok)
                ViewBag.MensajeExito = ok;

            return View(new Donacion());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear([FromForm] string tipo, [FromForm] string descripcion)
        {
            // Validaciones mínimas
            if (string.IsNullOrWhiteSpace(tipo) || !(tipo == "M" || tipo == "I" || tipo == "B" || tipo == "P"))
                ModelState.AddModelError("tipo", "Debe seleccionar un tipo válido (M = Medicamentos, I = Insumos, B = Banco, P = No Bancario).");

            if (string.IsNullOrWhiteSpace(descripcion))
                ModelState.AddModelError("descripcion", "La descripción es obligatoria.");

            if (!string.IsNullOrWhiteSpace(tipo) && !string.IsNullOrWhiteSpace(descripcion))
            {
                int max = (tipo == "M" || tipo == "I") ? 40 : 490;

                if (descripcion.Length > max)
                {
                    ModelState.AddModelError("descripcion",
                        $"La descripción no puede superar los {max} caracteres.");
                }
            }

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
        [Authorize(Policy = "AdminOrColab")]
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
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar([FromForm] int id_donacion, [FromForm] string tipo, [FromForm] string descripcion)
        {
            if (string.IsNullOrWhiteSpace(tipo) || !(tipo == "M" || tipo == "I" || tipo == "B" || tipo == "P"))
                ModelState.AddModelError("tipo", "Debe seleccionar un tipo válido (M = Medicamentos, I = Insumos, B = Banco, P = No Bancario).");

            if (string.IsNullOrWhiteSpace(descripcion))
                ModelState.AddModelError("descripcion", "La descripción es obligatoria.");

            if (!string.IsNullOrWhiteSpace(tipo) && !string.IsNullOrWhiteSpace(descripcion))
            {
                int max = (tipo == "M" || tipo == "I") ? 40 : 490;

                if (descripcion.Length > max)
                {
                    ModelState.AddModelError("descripcion",
                        $"La descripción no puede superar los {max} caracteres.");
                }
            }

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
        [Authorize(Roles = "Administrador")]
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
        [Authorize(Roles = "Administrador")]
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
