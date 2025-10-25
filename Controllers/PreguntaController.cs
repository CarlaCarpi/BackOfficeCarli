using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    public class PreguntaController : Controller
    {
        private readonly IHttpClientFactory _http;
        public PreguntaController(IHttpClientFactory http) => _http = http;

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");

            // Traer preguntas
            var resp = await client.GetAsync("/api/Pregunta");
            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"Error API: {(int)resp.StatusCode} - {resp.ReasonPhrase}";
                return View(new List<Pregunta>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var preguntas = JsonSerializer.Deserialize<List<Pregunta>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            // Diccionario de tipos para mostrar descripción en vez del ID (opcional pero útil)
            var respTipos = await client.GetAsync("/api/TipoFormulario");
            var tiposDict = new Dictionary<int, string>();
            if (respTipos.IsSuccessStatusCode)
            {
                var jsonTipos = await respTipos.Content.ReadAsStringAsync();
                var tipos = JsonSerializer.Deserialize<List<TipoFormularioThin>>(jsonTipos,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                tiposDict = tipos.GroupBy(t => t.id_tipoFormulario)
                                 .ToDictionary(g => g.Key, g => g.First().descripcion);
            }
            ViewBag.TiposDict = tiposDict;

            return View("Index", preguntas);
        }

        // ===================== CREAR =====================
        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            await CargarSelectTipos();
            if (TempData["Ok"] is string ok) ViewBag.MensajeExito = ok;
            return View(new Pregunta());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] string pregunta, [FromForm] int id_tipoFormulario, [FromForm] int? orden)
        {
            // Controles
            if (string.IsNullOrWhiteSpace(pregunta))
                ModelState.AddModelError(nameof(Pregunta.pregunta), "La pregunta es obligatoria.");

            if (pregunta?.Length > 300)
                ModelState.AddModelError(nameof(Pregunta.pregunta), "La pregunta no puede superar los 300 caracteres.");

            if (id_tipoFormulario <= 0)
                ModelState.AddModelError(nameof(Pregunta.id_tipoFormulario), "Seleccione un tipo de formulario válido.");

            if (orden.HasValue && orden.Value < 0)
                ModelState.AddModelError(nameof(Pregunta.orden), "El orden debe ser 0 o mayor.");

            var model = new Pregunta
            {
                pregunta = pregunta ?? string.Empty,
                id_tipoFormulario = id_tipoFormulario,
                orden = orden
            };

            if (!ModelState.IsValid)
            {
                await CargarSelectTipos(id_tipoFormulario);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/Pregunta", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/Pregunta -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                await CargarSelectTipos(id_tipoFormulario);
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
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/Pregunta/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
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

            if (TempData["Ok"] is string ok) ViewBag.MensajeExito = ok;
            await CargarSelectTipos(model.id_tipoFormulario);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] int id_pregunta, [FromForm] string pregunta, [FromForm] int id_tipoFormulario, [FromForm] int? orden)
        {
            // Controles
            if (string.IsNullOrWhiteSpace(pregunta))
                ModelState.AddModelError(nameof(Pregunta.pregunta), "La pregunta es obligatoria.");

            if (pregunta?.Length > 300)
                ModelState.AddModelError(nameof(Pregunta.pregunta), "La pregunta no puede superar los 300 caracteres.");

            if (id_tipoFormulario <= 0)
                ModelState.AddModelError(nameof(Pregunta.id_tipoFormulario), "Seleccione un tipo de formulario válido.");

            if (orden.HasValue && orden.Value < 0)
                ModelState.AddModelError(nameof(Pregunta.orden), "El orden debe ser 0 o mayor.");

            var model = new Pregunta
            {
                id_pregunta = id_pregunta,
                pregunta = pregunta ?? string.Empty,
                id_tipoFormulario = id_tipoFormulario,
                orden = orden
            };

            if (!ModelState.IsValid)
            {
                await CargarSelectTipos(id_tipoFormulario);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/Pregunta/{id_pregunta}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/Pregunta/{id_pregunta} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                await CargarSelectTipos(id_tipoFormulario);
                return View(model);
            }

            TempData["Ok"] = "Pregunta actualizada correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = id_pregunta });
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
            // Para la vista, cargamos diccionario de tipos (mostrar descripción)
            var tiposDict = await ObtenerTiposDict();
            ViewBag.TiposDict = tiposDict;

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

        // ===================== HELPERS =====================
        private async Task CargarSelectTipos(int? selected = null)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/TipoFormulario");

            var items = new List<SelectListItem> { new SelectListItem { Text = "Seleccione...", Value = "" } };

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var tipos = JsonSerializer.Deserialize<List<TipoFormularioThin>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

                items.AddRange(tipos.Select(t => new SelectListItem
                {
                    Value = t.id_tipoFormulario.ToString(),
                    Text = t.descripcion,
                    Selected = selected.HasValue && t.id_tipoFormulario == selected.Value
                }));
            }

            ViewBag.Tipos = items;
        }

        private async Task<Dictionary<int, string>> ObtenerTiposDict()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/TipoFormulario");

            if (!resp.IsSuccessStatusCode) return new();

            var json = await resp.Content.ReadAsStringAsync();
            var tipos = JsonSerializer.Deserialize<List<TipoFormularioThin>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            return tipos.GroupBy(t => t.id_tipoFormulario).ToDictionary(g => g.Key, g => g.First().descripcion);
        }
    }

    // ====== modelos simples usados para selects/diccionarios ======
    public class TipoFormularioThin
    {
        public int id_tipoFormulario { get; set; }
        public string descripcion { get; set; } = string.Empty;
    }
}
