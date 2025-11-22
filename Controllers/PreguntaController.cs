using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    [Authorize(Policy = "Activo")]
    public class PreguntaController : Controller
    {
        private readonly IHttpClientFactory _http;
        public PreguntaController(IHttpClientFactory http) => _http = http;

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index(
            [FromQuery] string? q,
            int page = 1,
            int pageSize = 20)
        {
            var client = _http.CreateClient("Api");

            // 👉 Ahora la API NO pagina: traemos TODO y paginamos acá
            var resp = await client.GetAsync("/api/Pregunta");
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/Pregunta -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.HasMore = false;
                return View(Enumerable.Empty<Pregunta>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var preguntas = JsonSerializer.Deserialize<List<Pregunta>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Pregunta>();

            // Diccionario de tipos para mostrar y para filtrar
            var tiposDict = await GetTiposDict();
            ViewBag.TiposDict = tiposDict;

            // para rellenar el input de búsqueda
            ViewBag.Query = q ?? string.Empty;

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            // 🔍 Filtro por texto: pregunta + tipo + estado (en memoria)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var qNorm = q.Trim().ToLower();

                preguntas = preguntas
                    .Where(p =>
                    {
                        // texto de la pregunta
                        var texto = p.pregunta ?? "";
                        bool matchPregunta = texto.Contains(qNorm, StringComparison.OrdinalIgnoreCase);

                        // texto del tipo de formulario
                        bool matchTipo = false;
                        if (tiposDict.TryGetValue(p.id_tipoFormulario, out var tipoTxt) && tipoTxt != null)
                        {
                            matchTipo = tipoTxt.Contains(qNorm, StringComparison.OrdinalIgnoreCase);
                        }

                        // estado como texto ("activo" / "inactivo")
                        var estadoTxt = p.activo ? "activo" : "inactivo";
                        bool matchEstado = estadoTxt.Contains(qNorm, StringComparison.OrdinalIgnoreCase);

                        return matchPregunta || matchTipo || matchEstado;
                    })
                    .ToList();
            }

            // ⭐ Ordenar por la más nueva (id más grande primero)
            preguntas = preguntas
                .OrderByDescending(p => p.id_pregunta)
                .ToList();

            // 🔢 Paginación en memoria
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var total = preguntas.Count;
            var pagePreguntas = preguntas
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var hasMore = (page * pageSize) < total;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.HasMore = hasMore;

            return View(pagePreguntas);
        }

        // ===================== VER MÁS =====================
        [HttpGet]
        public async Task<IActionResult> Mas(
            [FromQuery] string? q,
            int page = 2,
            int pageSize = 20)
        {
            var client = _http.CreateClient("Api");

            // 👉 Igual que Index: traemos TODO y paginamos acá
            var resp = await client.GetAsync("/api/Pregunta");
            if (!resp.IsSuccessStatusCode)
                return Content("");

            var json = await resp.Content.ReadAsStringAsync();
            var preguntas = JsonSerializer.Deserialize<List<Pregunta>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Pregunta>();

            var tiposDict = await GetTiposDict();
            ViewBag.TiposDict = tiposDict;

            // 🔍 mismo filtro que en Index
            if (!string.IsNullOrWhiteSpace(q))
            {
                var qNorm = q.Trim().ToLower();

                preguntas = preguntas
                    .Where(p =>
                    {
                        var texto = p.pregunta ?? "";
                        bool matchPregunta = texto.Contains(qNorm, StringComparison.OrdinalIgnoreCase);

                        bool matchTipo = false;
                        if (tiposDict.TryGetValue(p.id_tipoFormulario, out var tipoTxt) && tipoTxt != null)
                        {
                            matchTipo = tipoTxt.Contains(qNorm, StringComparison.OrdinalIgnoreCase);
                        }

                        var estadoTxt = p.activo ? "activo" : "inactivo";
                        bool matchEstado = estadoTxt.Contains(qNorm, StringComparison.OrdinalIgnoreCase);

                        return matchPregunta || matchTipo || matchEstado;
                    })
                    .ToList();
            }

            // Orden por más nueva
            preguntas = preguntas
                .OrderByDescending(p => p.id_pregunta)
                .ToList();

            // 🔢 Paginación en memoria para esta "page"
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var total = preguntas.Count;
            var pagePreguntas = preguntas
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var hasMore = (page * pageSize) < total;

            // Header para el JS del "Ver más"
            Response.Headers["X-HasMore"] = hasMore ? "true" : "false";

            return PartialView("_PreguntaRows", pagePreguntas);
        }

        // ===================== CREAR =====================
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear()
        {
            ViewBag.Tipos = await GetTiposSelect();
            if (TempData["Ok"] is string ok) ViewBag.MensajeExito = ok;
            return View(new Pregunta());
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
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
                orden = model.orden,
                activo = model.activo
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
        [Authorize(Policy = "AdminOrColab")]
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
        [Authorize(Policy = "AdminOrColab")]
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
                orden = model.orden,
                activo = model.activo
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
        [Authorize(Roles = "Administrador")]
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
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"/api/Pregunta/{id}");
            var body = await resp.Content.ReadAsStringAsync(); // leemos siempre la respuesta

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Pregunta eliminada correctamente.";
                return RedirectToAction(nameof(Index));
            }

            // 👉 Caso típico: error al guardar por restricción (FK / respuestas asociadas)
            if (!string.IsNullOrWhiteSpace(body) &&
                body.Contains("An error occurred while saving the entity changes", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "La pregunta posee respuestas asociadas, no se puede eliminar. Modifique la pregunta a desactivado para que no aparezca en el formulario.";

                // 👇 Importante: NO guardamos ApiDetail, así no se muestra el FATAL ni el stack trace
                // TempData["ApiDetail"] = body;

                return RedirectToAction(nameof(Index));
            }

            // 👉 Cualquier otro error: si querés, acá sí podés seguir viendo el detalle técnico
            TempData["Error"] = "No se pudo eliminar la pregunta. Inténtelo nuevamente.";
            TempData["ApiDetail"] = body;

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
