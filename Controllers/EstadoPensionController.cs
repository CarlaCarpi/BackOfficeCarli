using Microsoft.AspNetCore.Mvc;
using santa_ramona_BackOffice.Models;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]")]
    public class EstadoPensionController : Controller
    {
        private readonly IHttpClientFactory _http;
        public EstadoPensionController(IHttpClientFactory http) => _http = http;

        private const string RUTA_ESTADO = "/api/EstadoPension";  // <-- ajustá si tu API usa otro path/casing
        private const string RUTA_PENSION = "/api/Pension";        // para chequear FK en eliminar

        private static readonly JsonSerializerOptions JsonOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // GET: /EstadoPension
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync(RUTA_ESTADO);

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"GET {RUTA_ESTADO} -> {(int)resp.StatusCode} {resp.ReasonPhrase}";
                return View(Enumerable.Empty<Estado_Pension>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<IEnumerable<Estado_Pension>>(json, JsonOps)
                        ?? Enumerable.Empty<Estado_Pension>();

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(data);
        }

        // GET: /EstadoPension/Crear
        [HttpGet]
        public IActionResult Crear() => View(new Estado_Pension());

        // POST: /EstadoPension/Crear
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] string descripcion)
        {
            if (string.IsNullOrWhiteSpace(descripcion))
            {
                ModelState.AddModelError(nameof(Estado_Pension.descripcion), "La descripción es obligatoria.");
                return View(new Estado_Pension { descripcion = descripcion ?? string.Empty });
            }

            var model = new Estado_Pension { descripcion = descripcion.Trim() };
            var client = _http.CreateClient("Api");

            var content = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(RUTA_ESTADO, content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST {RUTA_ESTADO} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            TempData["Ok"] = "Estado de pensión creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /EstadoPension/Modificar/5
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{RUTA_ESTADO}/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El estado de pensión no existe.";
                return RedirectToAction(nameof(Index));
            }
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"GET {RUTA_ESTADO}/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var model = await resp.Content.ReadFromJsonAsync<Estado_Pension>(JsonOps);
            return View(model);
        }

        // POST: /EstadoPension/Modificar
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] int id_estadoPension, [FromForm] string descripcion)
        {
            if (string.IsNullOrWhiteSpace(descripcion))
            {
                ModelState.AddModelError(nameof(Estado_Pension.descripcion), "La descripción es obligatoria.");
                return View(new Estado_Pension { id_estadoPension = id_estadoPension, descripcion = descripcion ?? string.Empty });
            }

            var model = new Estado_Pension { id_estadoPension = id_estadoPension, descripcion = descripcion.Trim() };
            var client = _http.CreateClient("Api");

            var content = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");
            var resp = await client.PutAsync($"{RUTA_ESTADO}/{id_estadoPension}", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT {RUTA_ESTADO}/{id_estadoPension} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            TempData["Ok"] = "Estado de pensión actualizado correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = id_estadoPension });
        }

        // GET: /EstadoPension/Eliminar/5
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");

            // Traer estado
            var r = await client.GetAsync($"{RUTA_ESTADO}/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = r.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "El estado de pensión no existe o ya fue eliminado."
                    : $"No se pudo obtener el estado (código {(int)r.StatusCode}).";
                return RedirectToAction(nameof(Index));
            }

            var model = await r.Content.ReadFromJsonAsync<Estado_Pension>(JsonOps);

            // Chequear si hay pensiones usando este estado (FK)
            bool enUso = false;
            try
            {
                int pagina = 1;
                const int pageSize = 50;

                while (true)
                {
                    var p = await client.GetAsync($"{RUTA_PENSION}?pagina={pagina}&pageSize={pageSize}");
                    if (!p.IsSuccessStatusCode) break;

                    var pensiones = await p.Content.ReadFromJsonAsync<List<PensionMin>>(JsonOps);
                    if (pensiones == null || pensiones.Count == 0) break;

                    if (pensiones.Any(x => x.id_estadoPension == id))
                    {
                        enUso = true;
                        break;
                    }

                    if (pensiones.Count < pageSize) break;
                    pagina++;
                    if (pagina > 2000) break; // guardrail
                }
            }
            catch
            {
                enUso = false; // si falla el chequeo, no bloquear
            }

            ViewBag.EnUso = enUso;
            return View(model!);
        }

        private class PensionMin
        {
            public int id_pension { get; set; }
            public int id_estadoPension { get; set; }
        }

        // POST: /EstadoPension/Eliminar/5
        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"{RUTA_ESTADO}/{id}");
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Estado de pensión eliminado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            // Reobtener modelo para re-mostrar la vista Eliminar con error
            var r = await client.GetAsync($"{RUTA_ESTADO}/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudo eliminar el estado. Intentá nuevamente.";
                TempData["ApiDetail"] = $"DELETE {RUTA_ESTADO}/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}";
                return RedirectToAction(nameof(Index));
            }
            var model = await r.Content.ReadFromJsonAsync<Estado_Pension>(JsonOps);

            // Heurística para FK/relación en uso
            bool esFk =
                resp.StatusCode == System.Net.HttpStatusCode.Conflict ||
                (int)resp.StatusCode == 422 ||
                ((int)resp.StatusCode == 500 && (
                    body?.Contains("547") == true ||
                    body?.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase) == true ||
                    body?.Contains("FK__", StringComparison.OrdinalIgnoreCase) == true));

            ViewBag.EnUso = esFk;
            TempData["Error"] = esFk
                ? "No se puede eliminar el estado porque está en uso por una o más pensiones."
                : "No se pudo eliminar el estado. Intentá nuevamente.";
            TempData["ApiDetail"] = $"DELETE {RUTA_ESTADO}/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}";

            return View("Eliminar", model!);
        }
    }
}
