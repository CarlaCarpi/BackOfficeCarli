using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]")]
    public class TipoFormularioController : Controller
    {
        private readonly IHttpClientFactory _http;
        public TipoFormularioController(IHttpClientFactory http) => _http = http;

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");

            var resp = await client.GetAsync("/api/TipoFormulario");
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/TipoFormulario -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                TempData["Error"] = "No se pudo cargar la lista de tipos de formulario.";
                return View(Enumerable.Empty<Tipo_Formulario>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<IEnumerable<Tipo_Formulario>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Tipo_Formulario>();

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(lista);
        }

        // ===================== CREAR =====================
        [HttpGet]
        public IActionResult Crear()
        {
            return View(new Tipo_Formulario { Estado = "Activo" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] Tipo_Formulario model)
        {
            if (string.IsNullOrWhiteSpace(model.tipo))
                ModelState.AddModelError(nameof(Tipo_Formulario.tipo), "El tipo es obligatorio.");

            if (string.IsNullOrWhiteSpace(model.Estado))
                model.Estado = "Activo";
            else if (!EsEstadoValido(model.Estado))
                ModelState.AddModelError(nameof(Tipo_Formulario.Estado), "Estado inválido (use Activo o Inactivo).");

            if (!ModelState.IsValid)
                return View(model);

            var client = _http.CreateClient("Api");

            var payload = new
            {
                tipo = model.tipo,
                Estado = model.Estado
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/TipoFormulario", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"POST /api/TipoFormulario -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                TempData["Error"] = "No se pudo crear el tipo de formulario.";
                return View(model);
            }

            ViewBag.Ok = "Tipo de formulario creado correctamente.";
            ModelState.Clear();
            return View(new Tipo_Formulario { Estado = "Activo" });
        }

        // ===================== MODIFICAR =====================
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/TipoFormulario/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"No se pudo cargar el tipo de formulario {id}.";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Tipo_Formulario>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model == null)
            {
                TempData["Error"] = "No se pudo leer la respuesta del servidor.";
                return RedirectToAction(nameof(Index));
            }

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            return View(model);
        }

        // Soporta botones: name="Estado" value="Activo|Inactivo"
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] Tipo_Formulario model, string? Estado)
        {
            if (!string.IsNullOrWhiteSpace(Estado))
                model.Estado = Estado;

            if (model.id_tipoFormulario <= 0)
                ModelState.AddModelError(nameof(Tipo_Formulario.id_tipoFormulario), "Identificador inválido.");
            if (string.IsNullOrWhiteSpace(model.tipo))
                ModelState.AddModelError(nameof(Tipo_Formulario.tipo), "El tipo es obligatorio.");
            if (!EsEstadoValido(model.Estado))
                ModelState.AddModelError(nameof(Tipo_Formulario.Estado), "Estado inválido (use Activo o Inactivo).");

            if (!ModelState.IsValid)
                return View(model);

            var client = _http.CreateClient("Api");

            var payload = new
            {
                id_tipoFormulario = model.id_tipoFormulario,
                tipo = model.tipo,
                Estado = model.Estado
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/TipoFormulario/{model.id_tipoFormulario}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/TipoFormulario/{model.id_tipoFormulario} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                TempData["Error"] = "No se pudo actualizar el tipo de formulario.";
                return View(model);
            }

            TempData["Ok"] = "Tipo de formulario actualizado correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = model.id_tipoFormulario });
        }

        // ===================== DETALLE (parcial) =====================
        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/TipoFormulario/{id}");
            if (!resp.IsSuccessStatusCode) return NotFound();

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Tipo_Formulario>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model is null) return NotFound();

            return PartialView("DetalleTipoFormulario", model);
        }

        // ===================== ELIMINAR =====================
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/TipoFormulario/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"No se pudo cargar el tipo de formulario {id}.";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Tipo_Formulario>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (model == null)
            {
                TempData["Error"] = "No se pudo leer la respuesta del servidor.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost, ActionName("Eliminar"), ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"/api/TipoFormulario/{id}");

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Tipo de formulario eliminado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            if ((int)resp.StatusCode == 409)
            {
                TempData["Error"] = "No se puede eliminar: el tipo está en uso.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "No se pudo eliminar el tipo de formulario.";
            var body = await resp.Content.ReadAsStringAsync();
            ViewBag.ApiError = $"DELETE /api/TipoFormulario/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS =====================
        private static bool EsEstadoValido(string? estado)
            => string.Equals(estado, "Activo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(estado, "Inactivo", StringComparison.OrdinalIgnoreCase);
    }
}
