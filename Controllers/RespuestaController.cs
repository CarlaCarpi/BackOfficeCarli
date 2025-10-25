using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    public class RespuestaController : Controller
    {
        private readonly IHttpClientFactory _http;
        public RespuestaController(IHttpClientFactory http) => _http = http;

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");

            var resp = await client.GetAsync("/api/Respuesta");
            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"Error API: {(int)resp.StatusCode} - {resp.ReasonPhrase}";
                return View(new List<Respuesta>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var respuestas = JsonSerializer.Deserialize<List<Respuesta>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            // Diccionarios para mostrar textos legibles
            var tForms = client.GetAsync("/api/Formulario");
            var tPregs = client.GetAsync("/api/Pregunta");
            await Task.WhenAll(tForms, tPregs);

            ViewBag.FormulariosDict = await ToDict<FormularioThin>(tForms.Result, f => f.id_formulario, f => $"Formulario #{f.id_formulario}");
            ViewBag.PreguntasDict = await ToDict<PreguntaThin>(tPregs.Result, p => p.id_pregunta, p => p.pregunta);

            return View("Index", respuestas);
        }

        // ===================== CREAR =====================
        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            await CargarSelects();
            if (TempData["Ok"] is string ok) ViewBag.MensajeExito = ok;
            return View(new Respuesta());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] string respuesta, [FromForm] int id_formulario, [FromForm] int id_pregunta)
        {
            // Controles
            if (string.IsNullOrWhiteSpace(respuesta))
                ModelState.AddModelError(nameof(Respuesta.respuesta), "La respuesta es obligatoria.");

            if (id_formulario <= 0)
                ModelState.AddModelError(nameof(Respuesta.id_formulario), "Seleccione un formulario válido.");

            if (id_pregunta <= 0)
                ModelState.AddModelError(nameof(Respuesta.id_pregunta), "Seleccione una pregunta válida.");

            var model = new Respuesta
            {
                respuesta = respuesta ?? string.Empty,
                id_formulario = id_formulario,
                id_pregunta = id_pregunta
            };

            if (!ModelState.IsValid)
            {
                await CargarSelects(id_formulario, id_pregunta);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/Respuesta", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/Respuesta -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                await CargarSelects(id_formulario, id_pregunta);
                return View(model);
            }

            TempData["Ok"] = "Respuesta creada correctamente.";
            return RedirectToAction(nameof(Crear));
        }

        // ===================== MODIFICAR =====================
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Respuesta/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/Respuesta/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Respuesta>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model == null)
            {
                TempData["Error"] = "No se pudo leer la respuesta del servidor.";
                return RedirectToAction(nameof(Index));
            }

            if (TempData["Ok"] is string ok) ViewBag.MensajeExito = ok;
            await CargarSelects(model.id_formulario, model.id_pregunta);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] int id_respuesta, [FromForm] string respuesta, [FromForm] int id_formulario, [FromForm] int id_pregunta)
        {
            // Controles
            if (string.IsNullOrWhiteSpace(respuesta))
                ModelState.AddModelError(nameof(Respuesta.respuesta), "La respuesta es obligatoria.");

            if (id_formulario <= 0)
                ModelState.AddModelError(nameof(Respuesta.id_formulario), "Seleccione un formulario válido.");

            if (id_pregunta <= 0)
                ModelState.AddModelError(nameof(Respuesta.id_pregunta), "Seleccione una pregunta válida.");

            var model = new Respuesta
            {
                id_respuesta = id_respuesta,
                respuesta = respuesta ?? string.Empty,
                id_formulario = id_formulario,
                id_pregunta = id_pregunta
            };

            if (!ModelState.IsValid)
            {
                await CargarSelects(id_formulario, id_pregunta);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/Respuesta/{id_respuesta}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/Respuesta/{id_respuesta} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                await CargarSelects(id_formulario, id_pregunta);
                return View(model);
            }

            TempData["Ok"] = "Respuesta actualizada correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = id_respuesta });
        }

        // ===================== ELIMINAR =====================
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var r = await client.GetAsync($"/api/Respuesta/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = r.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "La respuesta no existe o ya fue eliminada."
                    : $"No se pudo obtener la respuesta (código {(int)r.StatusCode}).";
                return RedirectToAction(nameof(Index));
            }

            var model = await r.Content.ReadFromJsonAsync<Respuesta>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Diccionarios para vista (mostrar textos)
            var tForms = client.GetAsync("/api/Formulario");
            var tPregs = client.GetAsync("/api/Pregunta");
            await Task.WhenAll(tForms, tPregs);

            ViewBag.FormulariosDict = await ToDict<FormularioThin>(tForms.Result, f => f.id_formulario, f => $"Formulario #{f.id_formulario}");
            ViewBag.PreguntasDict = await ToDict<PreguntaThin>(tPregs.Result, p => p.id_pregunta, p => p.pregunta);

            return View(model!);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"/api/Respuesta/{id}");

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Respuesta eliminada correctamente.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "No se pudo eliminar la respuesta. Inténtelo nuevamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS =====================
        private async Task CargarSelects(int? formSel = null, int? pregSel = null)
        {
            var client = _http.CreateClient("Api");
            var tForms = client.GetAsync("/api/Formulario");
            var tPregs = client.GetAsync("/api/Pregunta");
            await Task.WhenAll(tForms, tPregs);

            ViewBag.Formularios = await ToSelectList<FormularioThin>(tForms.Result, x => x.id_formulario, x => $"Formulario #{x.id_formulario}", formSel);
            ViewBag.Preguntas = await ToSelectList<PreguntaThin>(tPregs.Result, x => x.id_pregunta, x => x.pregunta, pregSel);
        }

        private static async Task<List<SelectListItem>> ToSelectList<T>(
            HttpResponseMessage resp,
            Func<T, int> keySel,
            Func<T, string> textSel,
            int? selected = null)
        {
            var items = new List<SelectListItem> { new SelectListItem { Text = "Seleccione...", Value = "" } };

            if (resp is null || !resp.IsSuccessStatusCode) return items;

            var json = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<IEnumerable<T>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<T>();

            items.AddRange(list.Select(x => new SelectListItem
            {
                Value = keySel(x).ToString(),
                Text = textSel(x),
                Selected = selected.HasValue && keySel(x) == selected.Value
            }));

            return items;
        }

        private static async Task<Dictionary<int, string>> ToDict<T>(
            HttpResponseMessage resp,
            Func<T, int> keySel,
            Func<T, string> valSel)
        {
            if (resp is null || !resp.IsSuccessStatusCode)
                return new Dictionary<int, string>();

            var json = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<IEnumerable<T>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<T>();

            return list.GroupBy(keySel).ToDictionary(g => g.Key, g => valSel(g.First()));
        }
    }

    // ====== modelos simples para selects/diccionarios ======
    public class FormularioThin
    {
        public int id_formulario { get; set; }
    }
    public class PreguntaThin
    {
        public int id_pregunta { get; set; }
        public string pregunta { get; set; } = string.Empty;
    }
}
