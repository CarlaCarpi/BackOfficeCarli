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
    public class RespuestaController : Controller
    {
        private readonly IHttpClientFactory _http;
        public RespuestaController(IHttpClientFactory http) => _http = http;

        private static readonly JsonSerializerOptions JsonOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

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
            var respuestas = JsonSerializer.Deserialize<List<Respuesta>>(json, JsonOps) ?? new();

            var tForms = client.GetAsync("/api/Formulario");
            var tPregs = client.GetAsync("/api/Pregunta");
            await Task.WhenAll(tForms, tPregs);

            ViewBag.FormulariosDict = await ToDict<Formulario>(tForms.Result, f => (int?)f.id_formulario, f => $"#{f.id_formulario}");
            ViewBag.PreguntasDict = await ToDict<Pregunta>(tPregs.Result, p => (int?)p.id_pregunta, p => p.pregunta ?? $"Pregunta #{p.id_pregunta}");

            return View("Index", respuestas);
        }

        // ===================== CREAR =====================
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear()
        {
            var client = _http.CreateClient("Api");

            // ===== Tipos de Formulario =====
            var tipos = new List<(int id, string desc)>();
            var tHttp = await client.GetAsync("/api/TipoFormulario");
            if (tHttp.IsSuccessStatusCode)
            {
                var str = await tHttp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(str);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.TryGetProperty("id_tipoFormulario", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                        {
                            var id = idProp.GetInt32();
                            var desc = el.TryGetProperty("tipo", out var tp) ? (tp.GetString() ?? "") : "";
                            if (id > 0) tipos.Add((id, desc));
                        }
                    }
                }
            }

            ViewBag.Tipos = new SelectList(
                tipos.Select(x => new SelectListItem
                {
                    Value = x.id.ToString(),
                    Text = string.IsNullOrWhiteSpace(x.desc) ? $"Tipo {x.id}" : x.desc
                }),
                "Value", "Text"
            );

            ViewBag.Formularios = new SelectList(Enumerable.Empty<SelectListItem>());
            ViewBag.Preguntas = new SelectList(Enumerable.Empty<SelectListItem>());

            return View(new Respuesta());
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear([FromForm] Respuesta model)
        {
            if (model.id_formulario <= 0)
                ModelState.AddModelError(nameof(Respuesta.id_formulario), "Seleccione un formulario válido.");
            if (model.id_pregunta <= 0)
                ModelState.AddModelError(nameof(Respuesta.id_pregunta), "Seleccione una pregunta válida.");
            if (string.IsNullOrWhiteSpace(model.respuesta))
                ModelState.AddModelError(nameof(Respuesta.respuesta), "La respuesta es obligatoria.");

            var client = _http.CreateClient("Api");

            if (!ModelState.IsValid)
            {
                await CargarFormulariosSelect(model.id_formulario);
                await CargarPreguntasDeFormularioSelect(model.id_formulario, model.id_pregunta);
                return View(model);
            }

            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await client.PostAsync("/api/Respuesta", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/Respuesta -> {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}";
                await CargarFormulariosSelect(model.id_formulario);
                await CargarPreguntasDeFormularioSelect(model.id_formulario, model.id_pregunta);
                return View(model);
            }

            TempData["Ok"] = "Respuesta creada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===================== MODIFICAR (GET) =====================
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");

            // Traer la respuesta
            var resp = await client.GetAsync($"/api/Respuesta/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = resp.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "La respuesta no existe."
                    : $"No se pudo obtener la respuesta (código {(int)resp.StatusCode}).";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Respuesta>(json, JsonOps);
            if (model is null)
            {
                TempData["Error"] = "No se pudo leer la respuesta del servidor.";
                return RedirectToAction(nameof(Index));
            }

            // Cargar selects con la selección actual
            await CargarFormulariosSelect(model.id_formulario);
            await CargarPreguntasDeFormularioSelect(model.id_formulario, model.id_pregunta);

            if (TempData["Ok"] is string ok) ViewBag.MensajeExito = ok;
            return View(model);
        }

        // ===================== MODIFICAR (POST) =====================
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar(
            [FromForm] int id_respuesta,
            [FromForm] string? respuesta,
            [FromForm] int id_formulario,
            [FromForm] int id_pregunta)
        {
            // === Validaciones ===
            if (id_formulario <= 0)
                ModelState.AddModelError(nameof(Respuesta.id_formulario), "Seleccione un formulario válido.");
            if (id_pregunta <= 0)
                ModelState.AddModelError(nameof(Respuesta.id_pregunta), "Seleccione una pregunta válida.");
            if (string.IsNullOrWhiteSpace(respuesta))
                ModelState.AddModelError(nameof(Respuesta.respuesta), "La respuesta es obligatoria.");

            var model = new Respuesta
            {
                id_respuesta = id_respuesta,
                id_formulario = id_formulario,
                id_pregunta = id_pregunta,
                respuesta = respuesta ?? string.Empty
            };

            if (!ModelState.IsValid)
            {
                await CargarFormulariosSelect(id_formulario);
                await CargarPreguntasDeFormularioSelect(id_formulario, id_pregunta);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/Respuesta/{id_respuesta}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/Respuesta/{id_respuesta} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}";
                await CargarFormulariosSelect(id_formulario);
                await CargarPreguntasDeFormularioSelect(id_formulario, id_pregunta);
                return View(model);
            }

            TempData["Ok"] = "Respuesta actualizada correctamente.";

            // === NUEVO: si vino el id de persona, redirigimos al detalle ===
            var returnToPersonaId = Request.Form["returnToPersonaId"].ToString();
            if (!string.IsNullOrWhiteSpace(returnToPersonaId))
                return RedirectToAction("Detalle", "Persona", new { id = returnToPersonaId });

            // Si no vino, vuelve al mismo Modificar
            return RedirectToAction(nameof(Modificar), new { id = id_respuesta });
        }

        // ===================== LISTAR TIPOS =====================
        [HttpGet]
        public async Task<IActionResult> TiposJson()
        {
            var client = _http.CreateClient("Api");
            var r = await client.GetAsync("/api/TipoFormulario");
            if (!r.IsSuccessStatusCode) return Json(Array.Empty<object>());

            var list = new List<object>();
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("id_tipoFormulario", out var idP) && idP.ValueKind == JsonValueKind.Number)
                    {
                        var id = idP.GetInt32();
                        var desc = el.TryGetProperty("tipo", out var tp) ? (tp.GetString() ?? "") : $"Tipo {id}";
                        list.Add(new { value = id, text = string.IsNullOrWhiteSpace(desc) ? $"Tipo {id}" : desc });
                    }
                }
            }
            return Json(list);
        }

        // ===================== FORMULARIOS POR TIPO =====================
        [HttpGet]
        public async Task<IActionResult> FormulariosPorTipo(int id_tipoFormulario)
        {
            var client = _http.CreateClient("Api");
            var fHttp = await client.GetAsync($"/api/Formulario?id_tipoFormulario={id_tipoFormulario}");
            if (!fHttp.IsSuccessStatusCode)
                fHttp = await client.GetAsync("/api/Formulario");

            var list = new List<object>();
            if (fHttp.IsSuccessStatusCode)
            {
                var json = await fHttp.Content.ReadAsStringAsync();
                var formularios = JsonSerializer.Deserialize<List<Formulario>>(json, JsonOps) ?? new();
                var filtrados = formularios.Where(f => f.id_tipoFormulario == id_tipoFormulario);
                foreach (var f in filtrados)
                    list.Add(new { value = f.id_formulario, text = $"Formulario #{f.id_formulario}" });
            }
            return Json(list);
        }

        // ===================== PREGUNTAS POR FORMULARIO =====================
        [HttpGet]
        public async Task<IActionResult> PreguntasDeFormulario(int id_formulario)
        {
            var client = _http.CreateClient("Api");

            var fHttp = await client.GetAsync($"/api/Formulario/{id_formulario}");
            if (!fHttp.IsSuccessStatusCode)
                return BadRequest("Formulario no encontrado.");

            int idTipo = 0;
            using (var fdoc = JsonDocument.Parse(await fHttp.Content.ReadAsStringAsync()))
            {
                if (fdoc.RootElement.TryGetProperty("id_tipoFormulario", out var prop) &&
                    prop.ValueKind == JsonValueKind.Number)
                    idTipo = prop.GetInt32();
            }

            if (idTipo <= 0)
                return BadRequest("Tipo inválido.");

            var pHttp = await client.GetAsync($"/api/Pregunta?id_tipoFormulario={idTipo}");
            if (!pHttp.IsSuccessStatusCode)
                pHttp = await client.GetAsync($"/api/Pregunta?tipoFormularioId={idTipo}");

            if (!pHttp.IsSuccessStatusCode)
                return Json(Array.Empty<object>());

            var preguntas = JsonSerializer.Deserialize<List<Pregunta>>(await pHttp.Content.ReadAsStringAsync(), JsonOps) ?? new();
            return Json(preguntas.Select(p => new { value = p.id_pregunta, text = p.pregunta ?? $"Pregunta #{p.id_pregunta}" }));
        }

        // ===================== HELPERS =====================
        private async Task CargarFormulariosSelect(int? seleccionado = null)
        {
            var client = _http.CreateClient("Api");

            // Formularios
            var fStr = await (await client.GetAsync("/api/Formulario")).Content.ReadAsStringAsync();
            var formularios = JsonSerializer.Deserialize<List<Formulario>>(fStr, JsonOps) ?? new();

            // Tipos: id -> descripcion
            var tiposDict = new Dictionary<int, string>();
            var tHttp = await client.GetAsync("/api/TipoFormulario");
            if (tHttp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await tHttp.Content.ReadAsStringAsync());
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.TryGetProperty("id_tipoFormulario", out var idP) && idP.ValueKind == JsonValueKind.Number)
                        {
                            var id = idP.GetInt32();
                            var desc = el.TryGetProperty("tipo", out var tp) ? (tp.GetString() ?? "") : $"Tipo {id}";
                            tiposDict[id] = string.IsNullOrWhiteSpace(desc) ? $"Tipo {id}" : desc;
                        }
                    }
                }
            }

            ViewBag.Formularios = new SelectList(
                formularios.Select(f => new SelectListItem
                {
                    Value = f.id_formulario.ToString(),
                    Text = tiposDict.TryGetValue(f.id_tipoFormulario, out var d) ? d : $"Tipo {f.id_tipoFormulario}",
                    Selected = seleccionado.HasValue && f.id_formulario == seleccionado.Value
                }),
                "Value", "Text", seleccionado
            );
        }


        private async Task CargarPreguntasDeFormularioSelect(int? id_formulario, int? seleccionada = null)
        {
            var client = _http.CreateClient("Api");
            var items = new List<SelectListItem> { new SelectListItem { Text = "Seleccione...", Value = "" } };

            if (!id_formulario.HasValue || id_formulario.Value <= 0)
            {
                ViewBag.Preguntas = new SelectList(items, "Value", "Text");
                return;
            }

            int idTipo = 0;
            var fHttp = await client.GetAsync($"/api/Formulario/{id_formulario.Value}");
            if (fHttp.IsSuccessStatusCode)
            {
                using var fdoc = JsonDocument.Parse(await fHttp.Content.ReadAsStringAsync());
                if (fdoc.RootElement.TryGetProperty("id_tipoFormulario", out var tprop) && tprop.ValueKind == JsonValueKind.Number)
                    idTipo = tprop.GetInt32();
            }

            if (idTipo > 0)
            {
                var pHttp = await client.GetAsync($"/api/Pregunta?id_tipoFormulario={idTipo}");
                if (!pHttp.IsSuccessStatusCode)
                    pHttp = await client.GetAsync($"/api/Pregunta?tipoFormularioId={idTipo}");

                if (pHttp.IsSuccessStatusCode)
                {
                    var pStr = await pHttp.Content.ReadAsStringAsync();
                    var preguntas = JsonSerializer.Deserialize<List<Pregunta>>(pStr, JsonOps) ?? new();
                    items.AddRange(preguntas.Select(p => new SelectListItem
                    {
                        Value = p.id_pregunta.ToString(),
                        Text = p.pregunta ?? $"Pregunta #{p.id_pregunta}",
                        Selected = seleccionada.HasValue && p.id_pregunta == seleccionada.Value
                    }));
                }
            }

            ViewBag.Preguntas = new SelectList(items, "Value", "Text", seleccionada);
        }

        private static async Task<Dictionary<int?, string>> ToDict<T>(
            HttpResponseMessage resp,
            Func<T, int?> keySel,
            Func<T, string> valSel)
        {
            if (resp is null || !resp.IsSuccessStatusCode)
                return new Dictionary<int?, string>();

            var json = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<IEnumerable<T>>(json, JsonOps) ?? Enumerable.Empty<T>();

            var dict = new Dictionary<int?, string>();
            foreach (var item in list)
            {
                var k = keySel(item);
                if (k.HasValue && !dict.ContainsKey(k))
                    dict[k] = valSel(item);
            }
            return dict;
        }
    }
}
