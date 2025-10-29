using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    public class PreguntaController : Controller
    {
        private readonly IHttpClientFactory _http;
        public PreguntaController(IHttpClientFactory http) => _http = http;

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");

            var resp = await client.GetAsync("/api/Pregunta");
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/Pregunta -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(Enumerable.Empty<Pregunta>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var preguntas = JsonSerializer.Deserialize<IEnumerable<Pregunta>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Pregunta>();

            // Diccionario id_tipoFormulario -> descripción (descripcion | tipo)
            ViewBag.TiposDict = await GetTiposDict();

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(preguntas);
        }

        // ===================== CREAR =====================
        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            ViewBag.Tipos = await GetTiposSelect();
            if (TempData["Ok"] is string ok) ViewBag.MensajeExito = ok;
            return View(new Pregunta());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] Pregunta model)
        {
            // Validaciones mínimas (como en Animal)
            if (model.id_tipoFormulario <= 0)
                ModelState.AddModelError(nameof(Pregunta.id_tipoFormulario), "Seleccione un tipo de formulario válido.");
            if (string.IsNullOrWhiteSpace(model.pregunta))
                ModelState.AddModelError(nameof(Pregunta.pregunta), "La pregunta es obligatoria.");
            if (model.pregunta?.Length > 1000)
                ModelState.AddModelError(nameof(Pregunta.pregunta), "La pregunta no puede superar los 1000 caracteres.");
            if (model.orden.HasValue && model.orden.Value < 0)
                ModelState.AddModelError(nameof(Pregunta.orden), "El orden debe ser 0 o mayor.");

            if (!ModelState.IsValid)
            {
                ViewBag.Tipos = await GetTiposSelect(model.id_tipoFormulario);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(new
            {
                id_tipoFormulario = model.id_tipoFormulario,
                pregunta = model.pregunta,
                orden = model.orden
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/Pregunta", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/Pregunta -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                ViewBag.Tipos = await GetTiposSelect(model.id_tipoFormulario);
                return View(model);
            }

            TempData["Ok"] = "Pregunta creada correctamente.";
            return RedirectToAction(nameof(Crear));
        }

        // ===================== MODIFICAR =====================
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Pregunta/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"No se pudo cargar la pregunta {id}.";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Pregunta>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model == null)
            {
                TempData["Error"] = "No se pudo leer la respuesta del servidor.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Tipos = await GetTiposSelect(model.id_tipoFormulario);
            if (TempData["Ok"] is string ok) ViewBag.MensajeExito = ok;

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] Pregunta model)
        {
            if (model.id_tipoFormulario <= 0)
                ModelState.AddModelError(nameof(Pregunta.id_tipoFormulario), "Seleccione un tipo de formulario válido.");
            if (string.IsNullOrWhiteSpace(model.pregunta))
                ModelState.AddModelError(nameof(Pregunta.pregunta), "La pregunta es obligatoria.");
            if (model.pregunta?.Length > 1000)
                ModelState.AddModelError(nameof(Pregunta.pregunta), "La pregunta no puede superar los 1000 caracteres.");
            if (model.orden.HasValue && model.orden.Value < 0)
                ModelState.AddModelError(nameof(Pregunta.orden), "El orden debe ser 0 o mayor.");

            if (!ModelState.IsValid)
            {
                ViewBag.Tipos = await GetTiposSelect(model.id_tipoFormulario);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(new
            {
                id_pregunta = model.id_pregunta,
                id_tipoFormulario = model.id_tipoFormulario,
                pregunta = model.pregunta,
                orden = model.orden
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/Pregunta/{model.id_pregunta}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/Pregunta/{model.id_pregunta} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                ViewBag.Tipos = await GetTiposSelect(model.id_tipoFormulario);
                return View(model);
            }

            TempData["Ok"] = "Pregunta actualizada correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = model.id_pregunta });
        }

        // ===================== DETALLE (opcional, parcial) =====================
        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Pregunta/{id}");
            if (!resp.IsSuccessStatusCode) return NotFound();

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Pregunta>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model is null) return NotFound();

            ViewBag.TiposDict = await GetTiposDict();
            return PartialView("DetallePregunta", model);
        }

        // ===================== ELIMINAR =====================
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var r = await client.GetAsync($"/api/Pregunta/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = r.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "La pregunta no existe o ya fue eliminada."
                    : $"No se pudo obtener la pregunta (código {(int)r.StatusCode}).";
                return RedirectToAction(nameof(Index));
            }

            var model = await r.Content.ReadFromJsonAsync<Pregunta>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            ViewBag.TiposDict = await GetTiposDict();
            return View(model!);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"/api/Pregunta/{id}");

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Pregunta eliminada correctamente.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "No se pudo eliminar la pregunta. Inténtelo nuevamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS (sin clases extra) =====================
        private async Task<Dictionary<int, string>> GetTiposDict()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/TipoFormulario");
            var result = new Dictionary<int, string>();

            if (!resp.IsSuccessStatusCode) return result;

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var id = el.GetProperty("id_tipoFormulario").GetInt32();
                // admite "descripcion" o "tipo"
                string txt = el.TryGetProperty("descripcion", out var d) ? d.GetString() ?? ""
                           : el.TryGetProperty("tipo", out var t) ? t.GetString() ?? ""
                           : $"#{id}";
                result[id] = txt;
            }
            return result;
        }

        private async Task<List<SelectListItem>> GetTiposSelect(int? selected = null)
        {
            var dict = await GetTiposDict();
            var items = new List<SelectListItem> { new SelectListItem { Text = "Seleccione...", Value = "" } };

            items.AddRange(dict.Select(kv => new SelectListItem
            {
                Value = kv.Key.ToString(),
                Text = kv.Value,
                Selected = selected.HasValue && kv.Key == selected.Value
            }));
            return items;
        }
        [HttpGet]
        public async Task<IActionResult> PlantillaFormulario()
        {
            var client = _http.CreateClient("Api");

            // Obtener tipos de formulario
            var rTipos = await client.GetAsync("/api/TipoFormulario");
            var rPregs = await client.GetAsync("/api/Pregunta");

            if (!rTipos.IsSuccessStatusCode || !rPregs.IsSuccessStatusCode)
            {
                ViewBag.ApiError = "No se pudieron obtener los datos de la API.";
                return View(new List<Tipo_Formulario>());
            }

            var tipos = JsonSerializer.Deserialize<List<Tipo_Formulario>>(
                await rTipos.Content.ReadAsStringAsync(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            var preguntas = JsonSerializer.Deserialize<List<Pregunta>>(
                await rPregs.Content.ReadAsStringAsync(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            // Relacionar preguntas con su tipo
            foreach (var tipo in tipos)
            {
                tipo.PreguntasAsociadas = preguntas
                    .Where(p => p.id_tipoFormulario == tipo.id_tipoFormulario)
                    .OrderBy(p => p.orden ?? int.MaxValue)
                    .ToList();
            }

            return View(tipos);
        }

    }
}
