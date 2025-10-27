using Microsoft.AspNetCore.Mvc;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    public class EstadoPersonaController : Controller
    {
        private readonly IHttpClientFactory _http;
        public EstadoPersonaController(IHttpClientFactory http) => _http = http;

        private const string RUTA_ESTADO = "/api/EstadoPersona";      // <-- ajustá si tu API usa otro path
        private const string RUTA_PERSONA = "/api/persona";           // para chequear FK en eliminar

        private static readonly JsonSerializerOptions JsonOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // GET: /EstadoPersona
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync(RUTA_ESTADO);

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"GET {RUTA_ESTADO} -> {(int)resp.StatusCode} {resp.ReasonPhrase}";
                return View(Enumerable.Empty<Estado_Persona>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<IEnumerable<Estado_Persona>>(json, JsonOps)
                        ?? Enumerable.Empty<Estado_Persona>();

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(data);
        }

        // GET: /EstadoPersona/Crear
        [HttpGet]
        public IActionResult Crear() => View(new Estado_Persona());

        // POST: /EstadoPersona/Crear
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] string descripcion)
        {
            if (string.IsNullOrWhiteSpace(descripcion))
            {
                ModelState.AddModelError(nameof(Estado_Persona.descripcion), "La descripción es obligatoria.");
                return View(new Estado_Persona { descripcion = descripcion ?? string.Empty });
            }

            var model = new Estado_Persona { descripcion = descripcion.Trim() };
            var client = _http.CreateClient("Api");

            var content = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(RUTA_ESTADO, content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST {RUTA_ESTADO} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            // quedarse en pantalla para crear otro
            ViewBag.Ok = "Estado creado correctamente.";
            ModelState.Clear();
            return View(new Estado_Persona());
        }

        // GET: /EstadoPersona/Modificar/5
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{RUTA_ESTADO}/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El estado no existe.";
                return RedirectToAction(nameof(Index));
            }
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"GET {RUTA_ESTADO}/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var model = await resp.Content.ReadFromJsonAsync<Estado_Persona>(JsonOps);
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            return View(model);
        }

        // POST: /EstadoPersona/Modificar
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] int id_estadoPersona, [FromForm] string descripcion)
        {
            if (string.IsNullOrWhiteSpace(descripcion))
            {
                ModelState.AddModelError(nameof(Estado_Persona.descripcion), "La descripción es obligatoria.");
                return View(new Estado_Persona { id_estadoPersona = id_estadoPersona, descripcion = descripcion ?? string.Empty });
            }

            var model = new Estado_Persona { id_estadoPersona = id_estadoPersona, descripcion = descripcion.Trim() };
            var client = _http.CreateClient("Api");

            var content = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");
            var resp = await client.PutAsync($"{RUTA_ESTADO}/{id_estadoPersona}", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT {RUTA_ESTADO}/{id_estadoPersona} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            TempData["Ok"] = "Estado actualizado correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = id_estadoPersona });
        }

        // GET: /EstadoPersona/Eliminar/5
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");

            // Obtener el estado
            var r = await client.GetAsync($"{RUTA_ESTADO}/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = r.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "El estado no existe o ya fue eliminado."
                    : $"No se pudo obtener el estado (código {(int)r.StatusCode}).";
                return RedirectToAction(nameof(Index));
            }

            var model = await r.Content.ReadFromJsonAsync<Estado_Persona>(JsonOps);

            // Chequear si hay personas usando este estado
            bool enUso = false;
            try
            {
                int pagina = 1;
                const int pageSize = 50;

                while (true)
                {
                    var p = await client.GetAsync($"{RUTA_PERSONA}?pagina={pagina}&pageSize={pageSize}");
                    if (!p.IsSuccessStatusCode) break;

                    var personas = await p.Content.ReadFromJsonAsync<List<PersonaMin>>(JsonOps);
                    if (personas == null || personas.Count == 0) break;

                    if (personas.Any(x => x.id_estadoPersona == id))
                    {
                        enUso = true;
                        break;
                    }

                    if (personas.Count < pageSize) break;
                    pagina++;
                    if (pagina > 2000) break; // guardrail
                }
            }
            catch
            {
                enUso = false;
            }

            ViewBag.EnUso = enUso;
            return View(model!);
        }

        private class PersonaMin
        {
            public int id_persona { get; set; }
            public int id_estadoPersona { get; set; }
        }

        // POST: /EstadoPersona/Eliminar/5
        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"{RUTA_ESTADO}/{id}");
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Estado eliminado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            // Volver a cargar el modelo para mostrar el mismo eliminar con error
            var r = await client.GetAsync($"{RUTA_ESTADO}/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudo eliminar el estado. Intentalo nuevamente.";
                return RedirectToAction(nameof(Index));
            }
            var model = await r.Content.ReadFromJsonAsync<Estado_Persona>(JsonOps);

            bool esFk =
                resp.StatusCode == System.Net.HttpStatusCode.Conflict ||
                (int)resp.StatusCode == 422 ||
                ((int)resp.StatusCode == 500 && (
                    body?.Contains("547") == true ||
                    body?.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase) == true ||
                    body?.Contains("FK__", StringComparison.OrdinalIgnoreCase) == true));

            if (esFk)
            {
                ViewBag.EnUso = true;
                TempData["Error"] = "No se puede eliminar el estado porque está en uso por una o más personas.";
                return View("Eliminar", model!);
            }

            ViewBag.EnUso = false;
            TempData["Error"] = "No se pudo eliminar el estado. Intentalo nuevamente.";
            return View("Eliminar", model!);
        }
    }
}
