using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]")]
    public class FormularioController : Controller
    {
        private readonly IHttpClientFactory _http;
        public FormularioController(IHttpClientFactory http) => _http = http;

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");

            var resp = await client.GetAsync("/api/Formulario");
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/Formulario -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(Enumerable.Empty<Formulario>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<IEnumerable<Formulario>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Formulario>();

            // Cargar diccionarios
            var tPer = client.GetAsync("/api/Persona");
            var tTip = client.GetAsync("/api/TipoFormulario");
            await Task.WhenAll(tPer, tTip);

            ViewBag.PersonasDict = await ToDict<PersonaForm>(tPer.Result, x => x.id_persona, x => x.nombreCompleto);
            ViewBag.TiposDict = await ToDict<TipoFormulario>(tTip.Result, x => x.id_tipoFormulario, x => x.descripcion);

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(list);
        }

        // ===================== CREAR =====================
        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            await CargarSelects();
            return View(new Formulario { estado = "Pendiente" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] Formulario model)
        {
            if (model.id_persona <= 0)
                ModelState.AddModelError(nameof(Formulario.id_persona), "Seleccione una persona válida.");
            if (model.id_tipoFormulario <= 0)
                ModelState.AddModelError(nameof(Formulario.id_tipoFormulario), "Seleccione un tipo válido.");
            if (string.IsNullOrWhiteSpace(model.estado))
                model.estado = "Pendiente";

            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_persona, model.id_tipoFormulario);
                return View(model);
            }

            var client = _http.CreateClient("Api");

            var payload = new
            {
                id_persona = model.id_persona,
                id_tipoFormulario = model.id_tipoFormulario,
                estado = model.estado
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/Formulario", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"POST /api/Formulario -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                await CargarSelects(model.id_persona, model.id_tipoFormulario);
                return View(model);
            }

            ViewBag.Ok = "Formulario creado correctamente.";
            ModelState.Clear();
            await CargarSelects();
            return View(new Formulario { estado = "Pendiente" });
        }

        // ===================== MODIFICAR =====================
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Formulario/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"No se pudo cargar el formulario {id}.";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Formulario>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model == null)
            {
                TempData["Error"] = "No se pudo analizar el formulario.";
                return RedirectToAction(nameof(Index));
            }

            await CargarSelects(model.id_persona, model.id_tipoFormulario);
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] Formulario model, string? estado)
        {
            if (!string.IsNullOrWhiteSpace(estado))
                model.estado = estado;

            if (model.id_persona <= 0)
                ModelState.AddModelError(nameof(Formulario.id_persona), "Seleccione una persona válida.");
            if (model.id_tipoFormulario <= 0)
                ModelState.AddModelError(nameof(Formulario.id_tipoFormulario), "Seleccione un tipo válido.");

            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_persona, model.id_tipoFormulario);
                return View(model);
            }

            var client = _http.CreateClient("Api");

            var payload = new
            {
                id_formulario = model.id_formulario,
                id_persona = model.id_persona,
                id_tipoFormulario = model.id_tipoFormulario,
                estado = model.estado
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/Formulario/{model.id_formulario}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/Formulario/{model.id_formulario} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                await CargarSelects(model.id_persona, model.id_tipoFormulario);
                return View(model);
            }

            TempData["Ok"] = "Formulario actualizado correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = model.id_formulario });
        }

        // ===================== DETALLE (parcial) =====================
        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Formulario/{id}");
            if (!resp.IsSuccessStatusCode) return NotFound();

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Formulario>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model is null) return NotFound();

            await CargarDiccionariosBasicos();
            return PartialView("DetalleFormulario", model);
        }

        // ===================== ELIMINAR =====================
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");

            var resp = await client.GetAsync($"/api/Formulario/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"No se pudo cargar el formulario {id}.";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Formulario>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (model == null)
            {
                TempData["Error"] = "No se pudo leer la respuesta del servidor.";
                return RedirectToAction(nameof(Index));
            }

            // Diccionarios para la vista
            var tPer = client.GetAsync("/api/Persona");
            var tTip = client.GetAsync("/api/TipoFormulario");
            await Task.WhenAll(tPer, tTip);

            ViewBag.PersonasDict = await ToDict<PersonaForm>(tPer.Result, x => x.id_persona, x => x.nombreCompleto);
            ViewBag.TiposDict = await ToDict<TipoFormulario>(tTip.Result, x => x.id_tipoFormulario, x => x.descripcion);

            return View(model);
        }

        [HttpPost, ActionName("Eliminar"), ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"/api/Formulario/{id}");

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Formulario eliminado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            if ((int)resp.StatusCode == 409)
            {
                TempData["Error"] = "No se puede eliminar: el formulario está en uso.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "No se pudo eliminar el formulario.";
            var body = await resp.Content.ReadAsStringAsync();
            ViewBag.ApiError = $"DELETE /api/Formulario/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS =====================
        private async Task CargarSelects(int? personaSel = null, int? tipoSel = null)
        {
            var client = _http.CreateClient("Api");

            var tPer = client.GetAsync("/api/Persona");
            var tTip = client.GetAsync("/api/TipoFormulario");
            await Task.WhenAll(tPer, tTip);

            ViewBag.Personas = await ToSelectList<PersonaForm>(tPer.Result, x => x.id_persona, x => x.nombreCompleto, personaSel);
            ViewBag.Tipos = await ToSelectList<TipoFormulario>(tTip.Result, x => x.id_tipoFormulario, x => x.descripcion, tipoSel);
        }

        private async Task CargarDiccionariosBasicos()
        {
            var client = _http.CreateClient("Api");

            var tPer = client.GetAsync("/api/Persona");
            var tTip = client.GetAsync("/api/TipoFormulario");
            await Task.WhenAll(tPer, tTip);

            ViewBag.Personas = await ToDict<PersonaForm>(tPer.Result, x => x.id_persona, x => x.nombreCompleto);
            ViewBag.Tipos = await ToDict<TipoFormulario>(tTip.Result, x => x.id_tipoFormulario, x => x.descripcion);
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

    // ====== modelos simples para selects (ajustá nombres/props a tu API real) ======
    public class PersonaForm
    {
        public int id_persona { get; set; }
        public string nombreCompleto { get; set; } = string.Empty;
    }
    public class TipoFormulario
    {
        public int id_tipoFormulario { get; set; }
        public string descripcion { get; set; } = string.Empty;
    }
}
