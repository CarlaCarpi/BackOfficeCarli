using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SantaRamona.Backoffice.Models;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    [Authorize(Policy = "Activo")]
    public class PersonaController : Controller
    {
        private readonly IHttpClientFactory _http;
        public PersonaController(IHttpClientFactory http) => _http = http;

        // ====== Rutas API ======
        private const string RUTA_PERSONA = "/api/Persona";
        private const string RUTA_ESTADO_PERSONA = "/api/EstadoPersona";
        private const string RUTA_PROVINCIA = "/api/Provincia";
        private const string RUTA_LOCALIDAD = "/api/Localidad";

        private static readonly JsonSerializerOptions JsonOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ============================================================
        // ===================== MÉTODOS AUXILIARES ===================
        // ============================================================

        private async Task<SelectList> CargarEstadosSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_ESTADO_PERSONA);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());

            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<IEnumerable<Estado_Persona>>(json, JsonOps) ?? Enumerable.Empty<Estado_Persona>();

            var dict = lista.ToDictionary(e => e.id_estadoPersona, e => e.descripcion);
            return new SelectList(dict, "Key", "Value", seleccionado);
        }

        private async Task<Dictionary<int, string>> CargarEstadosDictAsync(HttpClient client)
        {
            var resp = await client.GetAsync(RUTA_ESTADO_PERSONA);
            if (!resp.IsSuccessStatusCode) return new Dictionary<int, string>();

            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<IEnumerable<Estado_Persona>>(json, JsonOps) ?? Enumerable.Empty<Estado_Persona>();
            return lista.ToDictionary(e => e.id_estadoPersona, e => e.descripcion);
        }

        private async Task<SelectList> CargarProvinciasSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_PROVINCIA);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());

            var json = await resp.Content.ReadAsStringAsync();
            var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(json, JsonOps) ?? Enumerable.Empty<Provincia>();

            return new SelectList(provincias.Select(p => new { p.id_provincia, p.nombre }),
                                  "id_provincia", "nombre", seleccionado);
        }

        private async Task<SelectList> CargarLocalidadesSelectAsync(HttpClient client, int? idProvincia, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_LOCALIDAD);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());

            var json = await resp.Content.ReadAsStringAsync();
            var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(json, JsonOps) ?? Enumerable.Empty<Localidad>();

            // Filtrar si hay provincia seleccionada
            if (idProvincia is not null && idProvincia > 0)
                localidades = localidades.Where(l => l.id_provincia == idProvincia);

            return new SelectList(localidades.Select(l => new { l.id_localidad, l.nombre }),
                                  "id_localidad", "nombre", seleccionado);
        }

        // ============================================================
        // ===================== INDEX ===============================
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20, string? q = null)
        {
            var client = _http.CreateClient("Api");

            // Normalizar y guardar búsqueda
            q = (q ?? "").Trim();
            ViewBag.Query = q;

            // ============================
            // 1) PEDIDO A LA API (SIN paginación)
            // ============================
            var resp = await client.GetAsync(RUTA_PERSONA);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al obtener personas: {body}";
                ViewBag.Estados = new Dictionary<int, string>();
                ViewBag.Provincia = new Dictionary<int, string>();
                ViewBag.Localidad = new Dictionary<int, string>();
                ViewBag.FormTiposByPersona = new Dictionary<int, List<string>>();

                ViewBag.Page = 1;
                ViewBag.PageSize = pageSize;
                ViewBag.HasMore = false;

                return View(Enumerable.Empty<Persona>());
            }

            // Parseo inicial: TODAS las personas
            var json = await resp.Content.ReadAsStringAsync();
            var personas = JsonSerializer.Deserialize<IEnumerable<Persona>>(json, JsonOps)
                           ?? Enumerable.Empty<Persona>();

            // =============================
            // 2) CARGAR ESTADOS (lo usamos en el filtro)
            // =============================
            var estadosDict = await CargarEstadosDictAsync(client);
            ViewBag.Estados = estadosDict;

            // =============================
            // 3) TIPOS DE FORMULARIO POR PERSONA
            // =============================
            Dictionary<int, List<string>> formTiposByPersona = new();

            try
            {
                var tiposDict = new Dictionary<int, string>();
                var tHttp = await client.GetAsync("/api/TipoFormulario");
                if (tHttp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await tHttp.Content.ReadAsStringAsync());
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.TryGetProperty("id_tipoFormulario", out var idProp) &&
                            idProp.ValueKind == JsonValueKind.Number)
                        {
                            var idTipo = idProp.GetInt32();
                            var nombre = el.TryGetProperty("tipo", out var pTipo) ? pTipo.GetString()
                                      : el.TryGetProperty("nombre", out var pNom) ? pNom.GetString()
                                      : $"Tipo {idTipo}";
                            tiposDict[idTipo] = string.IsNullOrWhiteSpace(nombre) ? $"Tipo {idTipo}" : nombre!;
                        }
                    }
                }

                var fHttp = await client.GetAsync("/api/Formulario");
                if (fHttp.IsSuccessStatusCode)
                {
                    var fJson = await fHttp.Content.ReadAsStringAsync();
                    var formularios = JsonSerializer.Deserialize<IEnumerable<FormularioMin>>(fJson, JsonOps)
                                      ?? Enumerable.Empty<FormularioMin>();

                    foreach (var g in formularios.Where(f => f.id_persona > 0).GroupBy(f => f.id_persona))
                    {
                        var lst = g.Select(f => tiposDict.TryGetValue(f.id_tipoFormulario, out var nom) ? nom : $"Tipo {f.id_tipoFormulario}")
                                   .Distinct(StringComparer.OrdinalIgnoreCase)
                                   .OrderBy(s => s)
                                   .ToList();

                        formTiposByPersona[g.Key] = lst;
                    }
                }
            }
            catch
            {
                // Si algo falla, dejamos diccionario vacío
                formTiposByPersona = new Dictionary<int, List<string>>();
            }

            ViewBag.FormTiposByPersona = formTiposByPersona;

            // =============================
            // 4) FILTRO LOCAL SI HAY q
            //    (Nombre, Apellido, EstadoPersona y Tipo de formulario)
            // =============================
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();

                personas = personas.Where(p =>
                {
                    // Nombre
                    bool porNombre = !string.IsNullOrWhiteSpace(p.nombre) &&
                                     p.nombre.Contains(term, StringComparison.OrdinalIgnoreCase);

                    // Apellido
                    bool porApellido = !string.IsNullOrWhiteSpace(p.apellido) &&
                                       p.apellido.Contains(term, StringComparison.OrdinalIgnoreCase);

                    // Estado (descripcion)
                    string estadoTxt = "";
                    if (p.id_estadoPersona is int idEst &&
                        estadosDict.TryGetValue(idEst, out var descEstado) &&
                        !string.IsNullOrWhiteSpace(descEstado))
                    {
                        estadoTxt = descEstado;
                    }

                    bool porEstado = !string.IsNullOrWhiteSpace(estadoTxt) &&
                                     estadoTxt.Contains(term, StringComparison.OrdinalIgnoreCase);

                    // 👉 NUEVO: Tipo de formulario (columna "Formulario")
                    bool porTipoFormulario = false;
                    if (formTiposByPersona.TryGetValue(p.id_persona, out var listaTipos) && listaTipos != null && listaTipos.Any())
                    {
                        porTipoFormulario = listaTipos.Any(t =>
                            t.Contains(term, StringComparison.OrdinalIgnoreCase));
                    }

                    return porNombre || porApellido || porEstado || porTipoFormulario;
                });
            }

            // =============================
            // 5) CARGAR DICCIONARIOS RESTO
            // =============================

            // Provincias
            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var jsonProv = await respProv.Content.ReadAsStringAsync();
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(jsonProv, JsonOps)
                                 ?? Enumerable.Empty<Provincia>();
                ViewBag.Provincia = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }
            else ViewBag.Provincia = new Dictionary<int, string>();

            // Localidades
            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var jsonLoc = await respLoc.Content.ReadAsStringAsync();
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(jsonLoc, JsonOps)
                                  ?? Enumerable.Empty<Localidad>();
                ViewBag.Localidad = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }
            else ViewBag.Localidad = new Dictionary<int, string>();

            // =============================
            // 6) MENSAJES TEMP
            // =============================
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            // =============================
            // 7) PAGINACIÓN EN MEMORIA
            // =============================
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var lista = personas
                .OrderByDescending(p => p.id_persona)
                .ToList();

            var total = lista.Count;
            var pagePersonas = lista
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var hasMore = (page * pageSize) < total;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.HasMore = hasMore;

            // =============================
            // 8) DEVOLVER SOLO LA PÁGINA
            // =============================
            return View(pagePersonas);
        }

        // ============================================================
        // ===================== MAS (VER MÁS) ========================
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Mas(int page = 2, int pageSize = 20, string? q = null)
        {
            var client = _http.CreateClient("Api");

            // Normalizar búsqueda
            q = (q ?? "").Trim();

            // 1) Traer TODAS las personas (igual que en Index)
            var resp = await client.GetAsync(RUTA_PERSONA);
            if (!resp.IsSuccessStatusCode)
            {
                // Si falla, devolvemos el código para que el front pueda manejarlo
                var body = await resp.Content.ReadAsStringAsync();
                return StatusCode((int)resp.StatusCode, body);
            }

            var json = await resp.Content.ReadAsStringAsync();
            var personas = JsonSerializer.Deserialize<IEnumerable<Persona>>(json, JsonOps)
                           ?? Enumerable.Empty<Persona>();

            // 2) Cargar ESTADOS (lo usamos en el filtro y en la vista)
            var estadosDict = await CargarEstadosDictAsync(client);
            ViewBag.Estados = estadosDict;

            // 3) TIPOS DE FORMULARIO POR PERSONA (para la columna "Formulario" y el filtro por q)
            Dictionary<int, List<string>> formTiposByPersona = new();

            try
            {
                var tiposDict = new Dictionary<int, string>();
                var tHttp = await client.GetAsync("/api/TipoFormulario");
                if (tHttp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await tHttp.Content.ReadAsStringAsync());
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            if (el.TryGetProperty("id_tipoFormulario", out var idProp) &&
                                idProp.ValueKind == JsonValueKind.Number)
                            {
                                var idTipo = idProp.GetInt32();
                                var nombre = el.TryGetProperty("tipo", out var pTipo) ? (pTipo.GetString() ?? "")
                                          : el.TryGetProperty("nombre", out var pNom) ? (pNom.GetString() ?? "")
                                          : $"Tipo {idTipo}";
                                tiposDict[idTipo] = string.IsNullOrWhiteSpace(nombre) ? $"Tipo {idTipo}" : nombre!;
                            }
                        }
                    }
                }

                var fHttp = await client.GetAsync("/api/Formulario");
                if (fHttp.IsSuccessStatusCode)
                {
                    var fJson = await fHttp.Content.ReadAsStringAsync();
                    var formularios = JsonSerializer.Deserialize<IEnumerable<FormularioMin>>(fJson, JsonOps)
                                      ?? Enumerable.Empty<FormularioMin>();

                    foreach (var g in formularios.Where(f => f.id_persona > 0).GroupBy(f => f.id_persona))
                    {
                        var lst = g.Select(f => tiposDict.TryGetValue(f.id_tipoFormulario, out var nom) ? nom : $"Tipo {f.id_tipoFormulario}")
                                   .Distinct(StringComparer.OrdinalIgnoreCase)
                                   .OrderBy(s => s)
                                   .ToList();

                        formTiposByPersona[g.Key] = lst;
                    }
                }
            }
            catch
            {
                formTiposByPersona = new Dictionary<int, List<string>>();
            }

            ViewBag.FormTiposByPersona = formTiposByPersona;

            // 4) FILTRO LOCAL si hay q (mismo criterio que en Index)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();

                personas = personas.Where(p =>
                {
                    // Nombre
                    bool porNombre = !string.IsNullOrWhiteSpace(p.nombre) &&
                                     p.nombre.Contains(term, StringComparison.OrdinalIgnoreCase);

                    // Apellido
                    bool porApellido = !string.IsNullOrWhiteSpace(p.apellido) &&
                                       p.apellido.Contains(term, StringComparison.OrdinalIgnoreCase);

                    // Estado (descripcion)
                    string estadoTxt = "";
                    if (p.id_estadoPersona is int idEst &&
                        estadosDict.TryGetValue(idEst, out var descEstado) &&
                        !string.IsNullOrWhiteSpace(descEstado))
                    {
                        estadoTxt = descEstado;
                    }

                    bool porEstado = !string.IsNullOrWhiteSpace(estadoTxt) &&
                                     estadoTxt.Contains(term, StringComparison.OrdinalIgnoreCase);

                    // Tipo de formulario
                    bool porTipoFormulario = false;
                    if (formTiposByPersona.TryGetValue(p.id_persona, out var listaTipos) &&
                        listaTipos != null && listaTipos.Any())
                    {
                        porTipoFormulario = listaTipos.Any(t =>
                            t.Contains(term, StringComparison.OrdinalIgnoreCase));
                    }

                    return porNombre || porApellido || porEstado || porTipoFormulario;
                });
            }

            // 5) Provincias y Localidades (la vista parcial también los usa)
            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var jsonProv = await respProv.Content.ReadAsStringAsync();
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(jsonProv, JsonOps)
                                 ?? Enumerable.Empty<Provincia>();
                ViewBag.Provincia = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }
            else ViewBag.Provincia = new Dictionary<int, string>();

            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var jsonLoc = await respLoc.Content.ReadAsStringAsync();
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(jsonLoc, JsonOps)
                                  ?? Enumerable.Empty<Localidad>();
                ViewBag.Localidad = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }
            else ViewBag.Localidad = new Dictionary<int, string>();

            // 6) PAGINACIÓN EN MEMORIA (igual que Index, pero solo devolvemos esta página)
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var lista = personas
                .OrderByDescending(p => p.id_persona)
                .ToList();

            var total = lista.Count;

            var pagePersonas = lista
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var hasMore = (page * pageSize) < total;
            Response.Headers["X-HasMore"] = hasMore ? "true" : "false";

            // Si no hay más filas, devolvemos 204 para que el front oculte el botón
            if (!pagePersonas.Any())
            {
                return StatusCode(204);
            }

            // 7) Devolver SOLO las filas <tr> usando el partial _PersonaRows
            return PartialView("_PersonaRows", pagePersonas);
        }

        // ===================== DETALLE ==============================

        [HttpGet]

        public async Task<IActionResult> DetalleFull(int id)
        {
            var client = _http.CreateClient("Api");

            // ===== Persona =====
            var resp = await client.GetAsync($"{RUTA_PERSONA}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"Error al obtener persona #{id}.";
                return RedirectToAction(nameof(Index));
            }
            var persona = JsonSerializer.Deserialize<Persona>(
                await resp.Content.ReadAsStringAsync(),
                JsonOps);

            // ===== Diccionarios existentes (Estados, Provincia, Localidad) =====
            ViewBag.Estados = await CargarEstadosDictAsync(client);

            // Provincias
            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(
                    await respProv.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Provincia>();
                ViewBag.Provincia = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }
            else ViewBag.Provincia = new Dictionary<int, string>();

            // Localidades
            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(
                    await respLoc.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Localidad>();
                ViewBag.Localidad = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }
            else ViewBag.Localidad = new Dictionary<int, string>();

            // ✅ NUEVO: Usuarios (para mostrar nombre en lugar de id_usuario)
            var usuariosDict = new Dictionary<int, string>();
            var respUsu = await client.GetAsync("/api/Usuario");
            if (respUsu.IsSuccessStatusCode)
            {
                var jsonUsu = await respUsu.Content.ReadAsStringAsync();
                var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(jsonUsu, JsonOps)
                               ?? Enumerable.Empty<Usuario>();

                usuariosDict = usuarios.ToDictionary(
                    u => u.id_usuario,
                    u =>
                    {
                        var nomComp = $"{u.nombre} {u.apellido}".Trim();
                        return string.IsNullOrWhiteSpace(nomComp) ? u.nombre : nomComp;
                    });
            }
            ViewBag.Usuarios = usuariosDict;

            // ===== Formularios de esta persona =====
            var formsHttp = await client.GetAsync("/api/Formulario");
            var formularios = new List<Formulario>();
            if (formsHttp.IsSuccessStatusCode)
            {
                var json = await formsHttp.Content.ReadAsStringAsync();
                var all = JsonSerializer.Deserialize<List<Formulario>>(json, JsonOps) ?? new();
                formularios = all.Where(f => f.id_persona == id).OrderByDescending(f => f.fechaEnvio).ToList();
            }
            ViewBag.Formularios = formularios;

            // ===== Tipos de Formulario (id -> texto) =====
            var tipoDict = new Dictionary<int, string>();
            var tHttp = await client.GetAsync("/api/TipoFormulario");
            if (tHttp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await tHttp.Content.ReadAsStringAsync());
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.TryGetProperty("id_tipoFormulario", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                        {
                            var idTipo = idProp.GetInt32();
                            var desc = el.TryGetProperty("tipo", out var tp) ? (tp.GetString() ?? "")
                                     : el.TryGetProperty("nombre", out var nom) ? (nom.GetString() ?? "")
                                     : $"Tipo {idTipo}";
                            if (idTipo > 0) tipoDict[idTipo] = desc;
                        }
                    }
                }
            }
            ViewBag.TipoDict = tipoDict;

            // ===== Preguntas por Tipo =====
            var preguntasByTipo = new Dictionary<int, List<Pregunta>>();

            foreach (var idTipo in formularios.Select(f => f.id_tipoFormulario).Distinct())
            {
                var pHttp = await client.GetAsync($"/api/Pregunta?id_tipoFormulario={idTipo}");
                if (!pHttp.IsSuccessStatusCode)
                    pHttp = await client.GetAsync($"/api/Pregunta?tipoFormularioId={idTipo}");

                var list = new List<Pregunta>();
                if (pHttp.IsSuccessStatusCode)
                {
                    var pJson = await pHttp.Content.ReadAsStringAsync();
                    var todas = JsonSerializer.Deserialize<List<Pregunta>>(pJson, JsonOps) ?? new();

                    // 🔒 Filtro defensivo por tipo de formulario
                    list = todas
                        .Where(p => p.id_tipoFormulario == idTipo)
                        .OrderBy(p => p.orden)
                        .ThenBy(p => p.id_pregunta)
                        .ToList();
                }

                preguntasByTipo[idTipo] = list;
            }

            ViewBag.PreguntasByTipo = preguntasByTipo;

            // ===== Respuestas por Formulario =====
            var respuestasByForm = new Dictionary<int, List<Respuesta>>();

            // Helper local: GET sin caché
            HttpRequestMessage NoCacheGet(string url)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MustRevalidate = true
                };
                req.Headers.Pragma.ParseAdd("no-cache");
                req.Headers.IfModifiedSince = DateTimeOffset.UtcNow;
                return req;
            }

            bool endpointNoFiltra = false;

            foreach (var f in formularios)
            {
                List<Respuesta> rListFinal = new();

                // 1) Primer intento: ?formularioId=
                var rHttp = await client.SendAsync(NoCacheGet($"/api/Respuesta?formularioId={f.id_formulario}"));
                if (rHttp.IsSuccessStatusCode)
                {
                    var payload = await rHttp.Content.ReadAsStringAsync();
                    var lista = JsonSerializer.Deserialize<List<Respuesta>>(payload, JsonOps) ?? new();

                    // Filtro defensivo local por id_formulario
                    var filtradas = lista.Where(r => r.id_formulario == f.id_formulario).ToList();

                    if (lista.Count > 0 && filtradas.Count == 0)
                    {
                        endpointNoFiltra = true;
                    }
                    else
                    {
                        rListFinal = filtradas;
                    }
                }

                // 2) Segundo intento: ?id_formulario= (si el primero no dejó resultados)
                if (!endpointNoFiltra && rListFinal.Count == 0)
                {
                    var rHttp2 = await client.SendAsync(NoCacheGet($"/api/Respuesta?id_formulario={f.id_formulario}"));
                    if (rHttp2.IsSuccessStatusCode)
                    {
                        var payload2 = await rHttp2.Content.ReadAsStringAsync();
                        var lista2 = JsonSerializer.Deserialize<List<Respuesta>>(payload2, JsonOps) ?? new();

                        var filtradas2 = lista2.Where(r => r.id_formulario == f.id_formulario).ToList();
                        if (lista2.Count > 0 && filtradas2.Count == 0)
                        {
                            endpointNoFiltra = true;
                        }
                        else
                        {
                            rListFinal = filtradas2;
                        }
                    }
                }

                if (endpointNoFiltra)
                    break;

                respuestasByForm[f.id_formulario] = rListFinal;
            }

            // Fallback: el endpoint no filtra → traemos todo y agrupamos, filtrando localmente
            if (endpointNoFiltra)
            {
                var rAllHttp = await client.SendAsync(NoCacheGet("/api/Respuesta"));
                if (rAllHttp.IsSuccessStatusCode)
                {
                    var allJson = await rAllHttp.Content.ReadAsStringAsync();
                    var all = JsonSerializer.Deserialize<List<Respuesta>>(allJson, JsonOps) ?? new();

                    respuestasByForm = all
                        .GroupBy(r => r.id_formulario)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var f in formularios)
                        if (!respuestasByForm.ContainsKey(f.id_formulario))
                            respuestasByForm[f.id_formulario] = new List<Respuesta>();
                }
            }

            ViewBag.RespuestasByForm = respuestasByForm;

            // Renderizamos en el modal como Partial
            return PartialView("_DetallePersonaFull", persona);
        }

        // ===================== NUEVO: DETALLE PDF ===================

        [HttpGet]
        public async Task<IActionResult> DetallePdf(int id)
        {
            var client = _http.CreateClient("Api");

            // ===== Persona =====
            var resp = await client.GetAsync($"{RUTA_PERSONA}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"Error al obtener persona #{id} para PDF.";
                return RedirectToAction(nameof(Index));
            }

            var persona = JsonSerializer.Deserialize<Persona>(
                await resp.Content.ReadAsStringAsync(),
                JsonOps
            );

            if (persona == null)
            {
                TempData["Error"] = $"No se encontró la persona #{id} para PDF.";
                return RedirectToAction(nameof(Index));
            }

            // ===== Diccionarios (Estados, Provincia, Localidad) =====
            var estadosDict = await CargarEstadosDictAsync(client);

            var provinciasDict = new Dictionary<int, string>();
            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(
                    await respProv.Content.ReadAsStringAsync(), JsonOps
                ) ?? Enumerable.Empty<Provincia>();
                provinciasDict = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }

            var localidadesDict = new Dictionary<int, string>();
            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(
                    await respLoc.Content.ReadAsStringAsync(), JsonOps
                ) ?? Enumerable.Empty<Localidad>();
                localidadesDict = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }

            // ===== Formularios de esta persona =====
            var formsHttp = await client.GetAsync("/api/Formulario");
            var formularios = new List<Formulario>();
            if (formsHttp.IsSuccessStatusCode)
            {
                var json = await formsHttp.Content.ReadAsStringAsync();
                var all = JsonSerializer.Deserialize<List<Formulario>>(json, JsonOps) ?? new();
                formularios = all.Where(f => f.id_persona == id)
                                 .OrderByDescending(f => f.fechaEnvio)
                                 .ToList();
            }

            // ===== Tipos de Formulario (id -> texto) =====
            var tipoDict = new Dictionary<int, string>();
            var tHttp = await client.GetAsync("/api/TipoFormulario");
            if (tHttp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await tHttp.Content.ReadAsStringAsync());
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.TryGetProperty("id_tipoFormulario", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                        {
                            var idTipo = idProp.GetInt32();
                            var desc = el.TryGetProperty("tipo", out var tp) ? (tp.GetString() ?? "")
                                     : el.TryGetProperty("nombre", out var nom) ? (nom.GetString() ?? "")
                                     : $"Tipo {idTipo}";
                            if (idTipo > 0)
                                tipoDict[idTipo] = string.IsNullOrWhiteSpace(desc) ? $"Tipo {idTipo}" : desc;
                        }
                    }
                }
            }

            // ===== Preguntas por Tipo (con filtro defensivo) =====
            var preguntasByTipo = new Dictionary<int, List<Pregunta>>();
            foreach (var idTipo in formularios.Select(f => f.id_tipoFormulario).Distinct())
            {
                var pHttp = await client.GetAsync($"/api/Pregunta?id_tipoFormulario={idTipo}");
                if (!pHttp.IsSuccessStatusCode)
                    pHttp = await client.GetAsync($"/api/Pregunta?tipoFormularioId={idTipo}");

                var list = new List<Pregunta>();
                if (pHttp.IsSuccessStatusCode)
                {
                    var pJson = await pHttp.Content.ReadAsStringAsync();
                    var todas = JsonSerializer.Deserialize<List<Pregunta>>(pJson, JsonOps) ?? new();

                    list = todas
                        .Where(p => p.id_tipoFormulario == idTipo)
                        .OrderBy(p => p.orden)
                        .ThenBy(p => p.id_pregunta)
                        .ToList();
                }

                preguntasByTipo[idTipo] = list;
            }

            // ===== Respuestas por Formulario (simple, filtrando por id_formulario) =====
            var respuestasByForm = new Dictionary<int, List<Respuesta>>();

            foreach (var f in formularios)
            {
                var listaFinal = new List<Respuesta>();

                var rHttp = await client.GetAsync($"/api/Respuesta?formularioId={f.id_formulario}");
                if (!rHttp.IsSuccessStatusCode)
                    rHttp = await client.GetAsync($"/api/Respuesta?id_formulario={f.id_formulario}");

                if (rHttp.IsSuccessStatusCode)
                {
                    var payload = await rHttp.Content.ReadAsStringAsync();
                    var lista = JsonSerializer.Deserialize<List<Respuesta>>(payload, JsonOps) ?? new();
                    listaFinal = lista.Where(r => r.id_formulario == f.id_formulario).ToList();
                }

                respuestasByForm[f.id_formulario] = listaFinal;
            }

            // ===== Helpers de texto =====
            string Str(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s;
            string IntTxt(int? n) => n.HasValue ? n.Value.ToString() : "—";
            string DateTxt(DateTime? d) => d.HasValue ? d.Value.ToString("dd/MM/yyyy") : "—";
            string Txt(Dictionary<int, string> dict, int? pid)
                => pid.HasValue && dict.TryGetValue(pid.Value, out var v) ? v : (pid.HasValue ? $"#{pid}" : "—");

            // ===== Documento PDF con QuestPDF (sin cadenas raras) =====
            // ===== Documento PDF con estilo similar a la vista =====
            var docPdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(t => t.FontSize(8));

                    // ===== HEADER =====
                    page.Header()
                        .Background(Color.FromHex("2FA8A2"))
                        .Padding(6)   // altura del banner
                        .Text("Ficha persona")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(Colors.White);

                    // ===== CONTENIDO =====
                    page.Content().Column(col =>
                    {
                       
                        // Tarjeta de datos de persona
                        col.Item()
                            .Border(1)
                            .BorderColor(Color.FromHex("2FA8A2"))
                            .Padding(10)
                            .Column(card =>
                            {
                                card.Spacing(3);

                                card.Item().Text(text =>
                                {
                                    text.Span("Nombre: ").SemiBold();
                                    text.Span(Str(persona.nombre));
                                });

                                card.Item().Text(text =>
                                {
                                    text.Span("Apellido: ").SemiBold();
                                    text.Span(Str(persona.apellido));
                                });

                                card.Item().Text(text =>
                                {
                                    text.Span("DNI: ").SemiBold();
                                    text.Span(IntTxt(persona.dni));
                                });

                                card.Item().Text(text =>
                                {
                                    text.Span("Email: ").SemiBold();
                                    text.Span(Str(persona.email));
                                });

                                card.Item().Text(text =>
                                {
                                    text.Span("Dirección: ").SemiBold();
                                    text.Span($"{Str(persona.calle)} {IntTxt(persona.altura)} {Str(persona.departamento)}");
                                });

                                card.Item().Text(text =>
                                {
                                    text.Span("Provincia: ").SemiBold();
                                    text.Span(Txt(provinciasDict, persona.id_provincia));
                                    text.Span("   |   Localidad: ").SemiBold();
                                    text.Span(Txt(localidadesDict, persona.id_localidad));
                                });

                                card.Item().Text(text =>
                                {
                                    text.Span("Redes sociales: ").SemiBold();
                                    text.Span(Str(persona.redesSociales));
                                });

                                card.Item().Text(text =>
                                {
                                    text.Span("Fecha de alta: ").SemiBold();
                                    text.Span(DateTxt(persona.fechaIngreso));
                                });

                                card.Item().Text("Observaciones:")
                                    .SemiBold();

                                card.Item().Text(Str(persona.motivoEgreso));

                                card.Item().Text(text =>
                                {
                                    text.Span("Estado: ").SemiBold();
                                    text.Span(Txt(estadosDict, persona.id_estadoPersona));
                                });
                            });

                        // Separador entre persona y formularios
                        col.Item().Text(" ");
                        col.Item().Text(" ");

                        if (formularios.Any())
                        {
                            foreach (var f in formularios)
                            {
                                var tipoTxt = tipoDict.TryGetValue(f.id_tipoFormulario, out var t) ? t : $"Tipo {f.id_tipoFormulario}";
                                var preguntas = preguntasByTipo.TryGetValue(f.id_tipoFormulario, out var ql) ? ql : new List<Pregunta>();
                                var respuestas = respuestasByForm.TryGetValue(f.id_formulario, out var rl) ? rl : new List<Respuesta>();

                                // Tarjeta de cada formulario
                                col.Item()
                                    .Border(1)
                                    .BorderColor(Color.FromHex("2FA8A2"))
                                    .Column(card =>
                                    {
                                        // Encabezado del formulario (barra verde igual a Ficha persona)
                                        card.Item()
                                            .Background(Color.FromHex("2FA8A2"))
                                            .Padding(6)
                                            .Text($"Formulario respondido: {tipoTxt}")
                                                .FontSize(12)
                                                .SemiBold()
                                                .FontColor(Colors.White);

                                        // Contenido del formulario con padding interno
                                        card.Item()
                                            .Padding(10)
                                            .Column(fcol =>
                                            {
                                                fcol.Spacing(3);

                                                if (preguntas.Any())
                                                {
                                                    int i = 1;

                                                    foreach (var q in preguntas)
                                                    {
                                                        var r = respuestas.FirstOrDefault(x => x.id_pregunta == q.id_pregunta);
                                                        var valor = string.IsNullOrWhiteSpace(r?.respuesta) ? "—" : r!.respuesta;

                                                        fcol.Item().Column(block =>
                                                        {
                                                            block.Item().Text($"{i}. {q.pregunta ?? $"Pregunta #{q.id_pregunta}"}")
                                                                .SemiBold().FontSize(8);

                                                            block.Item().Text(valor)
                                                                .FontSize(8);

                                                            block.Item().Text(" ");
                                                        });

                                                        i++;
                                                    }
                                                }
                                                else
                                                {
                                                    fcol.Item().Text("No hay preguntas configuradas para este tipo.")
                                                        .FontSize(9);
                                                }
                                            });
                                    });


                                // Espacio entre formularios
                                col.Item().Text(" ");
                            }
                        }
                        else
                        {
                            col.Item().Text("Esta persona no tiene formularios registrados.")
                                .FontSize(10);
                        }
                    });

                    /// FOOTER
                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.CurrentPageNumber();
                        txt.Span(" / ");
                        txt.TotalPages();
                    });
                });
            });

            var pdfBytes = docPdf.GeneratePdf();
            var fileName = $"persona_{persona.id_persona}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }


        // ===================== CREAR ================================
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear()
        {
            var client = _http.CreateClient("Api");
            ViewBag.Estados = await CargarEstadosSelectAsync(client);
            ViewBag.Provincia = await CargarProvinciasSelectAsync(client);
            ViewBag.Localidad = new SelectList(Enumerable.Empty<SelectListItem>());

            // Fecha de alta por defecto = hoy
            return View(new Persona { fechaIngreso = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear([FromForm] Persona persona)
        {
            // 👉 Ligar la persona al usuario logueado (igual que en Pensión)
            persona.id_usuario = GetCurrentUserId();

            // 👉 MUY IMPORTANTE (SOLUCIONA TU ERROR)
            ModelState.Remove(nameof(persona.fechaNacimiento));

            // Muy importante: quitar id_usuario del ModelState para que no se quede el 0 del binding
            ModelState.Remove(nameof(persona.id_usuario));

            // Normalizar teléfonos
            persona.telefono1 = persona.telefono1.Trim();
            if (!string.IsNullOrWhiteSpace(persona.telefono2))
                persona.telefono2 = persona.telefono2!.Trim();

            // Si no vino fecha de ingreso, usar hoy
            if (persona.fechaIngreso == default(DateTime))
                persona.fechaIngreso = DateTime.Today;

            if (!ModelState.IsValid)
            {
                var clientErr = _http.CreateClient("Api");
                ViewBag.Estados = await CargarEstadosSelectAsync(clientErr, persona.id_estadoPersona);
                ViewBag.Provincia = await CargarProvinciasSelectAsync(clientErr, persona.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(clientErr, persona.id_provincia, persona.id_localidad);
                return View(persona);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(persona);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync(RUTA_PERSONA, content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al guardar persona: {body}";

                ViewBag.Estados = await CargarEstadosSelectAsync(client, persona.id_estadoPersona);
                ViewBag.Provincia = await CargarProvinciasSelectAsync(client, persona.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(client, persona.id_provincia, persona.id_localidad);
                return View(persona);
            }

            TempData["Ok"] = "Persona creada correctamente.";
            return RedirectToAction(nameof(Index));
        }
        private int GetCurrentUserId()
        {
            string? raw =
                User.FindFirstValue("IdUsuario") ??                // por si tu claim se llama así
                User.FindFirstValue("id_usuario") ??               // claim típico
                User.FindFirstValue("Id") ??                       // otro nombre posible
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??  // estándar
                User.FindFirstValue("sub") ??                      // JWT
                "0";

            return int.TryParse(raw, out var id) ? id : 0;
        }



        // ===================== MODIFICAR ============================

        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{RUTA_PERSONA}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"No se encontró la persona #{id}.";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Persona>(await resp.Content.ReadAsStringAsync(), JsonOps);

            ViewBag.Estados = await CargarEstadosSelectAsync(client, model?.id_estadoPersona);
            ViewBag.Provincia = await CargarProvinciasSelectAsync(client, model?.id_provincia);
            ViewBag.Localidad = await CargarLocalidadesSelectAsync(client, model?.id_provincia, model?.id_localidad);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar([FromForm] Persona persona)
        {
            // ✅ Ignoramos fechaIngreso en el ModelState (por si viene vacía o rara)
            ModelState.Remove(nameof(persona.fechaIngreso));

            persona.telefono1 = persona.telefono1?.Trim();
            if (!string.IsNullOrWhiteSpace(persona.telefono2)) persona.telefono2 = persona.telefono2!.Trim();

            // ⬇⬇ NUEVO: tomar el id del usuario logueado y guardarlo
            var claimIdUsuario = User.FindFirst("IdUsuario")
                                  ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (claimIdUsuario != null && int.TryParse(claimIdUsuario.Value, out var idUsu))
                persona.id_usuario = idUsu;

            persona.fechaEgreso = DateTime.Now;


            if (!ModelState.IsValid)
            {
                var clientErr = _http.CreateClient("Api");
                ViewBag.Estados = await CargarEstadosSelectAsync(clientErr, persona.id_estadoPersona);
                ViewBag.Provincia = await CargarProvinciasSelectAsync(clientErr, persona.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(clientErr, persona.id_provincia, persona.id_localidad);
                return View(persona);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(persona);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"{RUTA_PERSONA}/{persona.id_persona}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al actualizar persona: {body}";
                ViewBag.Estados = await CargarEstadosSelectAsync(client, persona.id_estadoPersona);
                ViewBag.Provincia = await CargarProvinciasSelectAsync(client, persona.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(client, persona.id_provincia, persona.id_localidad);
                return View(persona);
            }


            TempData["Ok"] = "Persona actualizada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===================== ELIMINAR =============================

        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{RUTA_PERSONA}/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "La persona no existe o ya fue eliminada.";
                return RedirectToAction(nameof(Index));
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"Error al obtener persona: {body}";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Persona>(
                await resp.Content.ReadAsStringAsync(),
                JsonOps
            );

            // 🔹 Cargar diccionarios de provincias, localidades y estado persona
            await CargarDiccionariosPersonaAsync(client);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");

            try
            {
                // 1️⃣ Obtener todos los formularios existentes (LO MISMO QUE YA TENÍAS)
                var fResp = await client.GetAsync("/api/Formulario");
                if (fResp.IsSuccessStatusCode)
                {
                    var fJson = await fResp.Content.ReadAsStringAsync();
                    var formularios = JsonSerializer.Deserialize<List<FormularioMin>>(fJson, JsonOps) ?? new();

                    var formulariosPersona = formularios
                        .Where(f => f.id_persona == id)
                        .ToList();

                    foreach (var form in formulariosPersona)
                    {
                        // 1.a) Obtener respuestas del formulario
                        var rResp = await client.GetAsync($"/api/Respuesta?formularioId={form.id_formulario}");
                        if (!rResp.IsSuccessStatusCode)
                            rResp = await client.GetAsync($"/api/Respuesta?id_formulario={form.id_formulario}");

                        if (rResp.IsSuccessStatusCode)
                        {
                            var rJson = await rResp.Content.ReadAsStringAsync();
                            var respuestas = JsonSerializer.Deserialize<List<RespuestaMin>>(rJson, JsonOps) ?? new();

                            var respuestasDelForm = respuestas
                                .Where(r => r.id_formulario == form.id_formulario)
                                .ToList();

                            // 1.b) Eliminar respuestas
                            foreach (var r in respuestasDelForm)
                            {
                                await client.DeleteAsync($"/api/Respuesta/{r.id_respuesta}");
                            }
                        }

                        // 1.c) Eliminar el formulario
                        await client.DeleteAsync($"/api/Formulario/{form.id_formulario}");
                    }
                }

                // 2️⃣ Eliminar persona (AHORA vía soft delete con DTO)
                var idUsuarioActual = GetCurrentUserId(); // mismo helper que usaste en Pensión
                if (idUsuarioActual <= 0)
                {
                    TempData["Error"] = "No se pudo determinar el usuario actual para registrar la eliminación.";
                    return RedirectToAction(nameof(Eliminar), new { id });
                }

                var dto = new EliminarPersonaDto
                {
                    fechaEliminacion = null,        // que la API use DateTime.Now
                    id_usuario = idUsuarioActual
                };

                var json = JsonSerializer.Serialize(dto, JsonOps);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // 👉 Llamamos al endpoint correcto de la API
                var respDel = await client.PutAsync($"{RUTA_PERSONA}/Eliminar/{id}", content);

                if (!respDel.IsSuccessStatusCode)
                {
                    var body = await respDel.Content.ReadAsStringAsync();

                    TempData["Error"] = "No se pudo eliminar la persona.";
                    if (!string.IsNullOrWhiteSpace(body))
                        TempData["ApiDetail"] = body;

                    // Volvemos a la pantalla Eliminar para mostrar el error
                    return RedirectToAction(nameof(Eliminar), new { id });
                }

                TempData["Ok"] = "Persona eliminada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error inesperado al intentar eliminar la persona.";
                TempData["ApiDetail"] = ex.Message;
                return RedirectToAction(nameof(Eliminar), new { id });
            }
        }


        private async Task CargarDiccionariosPersonaAsync(HttpClient client)
        {
            var ops = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // PROVINCIAS
            var respProv = await client.GetAsync("/api/Provincia");
            if (respProv.IsSuccessStatusCode)
            {
                var provJson = await respProv.Content.ReadAsStringAsync();
                var listaProv = JsonSerializer.Deserialize<List<Provincia>>(provJson, ops) ?? new();
                ViewBag.Provincias = listaProv.ToDictionary(x => x.id_provincia, x => x.nombre);
            }
            else ViewBag.Provincias = new Dictionary<int, string>();

            // LOCALIDADES
            var respLoc = await client.GetAsync("/api/Localidad");
            if (respLoc.IsSuccessStatusCode)
            {
                var locJson = await respLoc.Content.ReadAsStringAsync();
                var listaLoc = JsonSerializer.Deserialize<List<Localidad>>(locJson, ops) ?? new();
                ViewBag.Localidades = listaLoc.ToDictionary(x => x.id_localidad, x => x.nombre);
            }
            else ViewBag.Localidades = new Dictionary<int, string>();

            // ESTADOS PERSONA
            var respEst = await client.GetAsync("/api/EstadoPersona");
            if (respEst.IsSuccessStatusCode)
            {
                var estJson = await respEst.Content.ReadAsStringAsync();
                var listaEst = JsonSerializer.Deserialize<List<Estado_Persona>>(estJson, ops) ?? new();
                ViewBag.EstadosPersona = listaEst.ToDictionary(x => x.id_estadoPersona, x => x.descripcion);
            }
            else ViewBag.EstadosPersona = new Dictionary<int, string>();
        }


        // ============================================================
        // ====== AJAX: Localidades por Provincia (filtrado MVC) ======
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> LocalidadesPorProvincia(int provinciaId)
        {
            var client = _http.CreateClient("Api");

            // 🔹 Trae TODAS las localidades y filtra por provincia aquí mismo.
            var resp = await client.GetAsync(RUTA_LOCALIDAD);
            if (!resp.IsSuccessStatusCode)
                return StatusCode((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());

            var json = await resp.Content.ReadAsStringAsync();
            var todas = JsonSerializer.Deserialize<IEnumerable<Localidad>>(json, JsonOps) ?? Enumerable.Empty<Localidad>();

            var filtradas = todas
                .Where(l => l.id_provincia == provinciaId)
                .OrderBy(l => l.nombre)
                .Select(l => new { l.id_localidad, l.nombre });

            return Json(filtradas);
        }

        [HttpGet]
        public async Task<IActionResult> RespuestasHtml(int id_formulario)
        {
            var client = _http.CreateClient("Api");

            // 1) Intento directo: endpoint que ya devuelva la lista de {pregunta, respuesta}
            // Probamos varias rutas comunes para máxima compatibilidad
            var candidatos = new[]
            {
                $"/api/Formulario/QA/{id_formulario}",
                $"/api/PreguntasRespuestas?formularioId={id_formulario}",
                $"/api/PreguntasRespuestas?id_formulario={id_formulario}"
            };

            JsonDocument? qaDoc = null;
            foreach (var url in candidatos)
            {
                try
                {
                    var r = await client.GetAsync(url);
                    if (!r.IsSuccessStatusCode) continue;
                    var s = await r.Content.ReadAsStringAsync();
                    var tmp = JsonDocument.Parse(s);
                    if (tmp.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        qaDoc = tmp;
                        break;
                    }
                }
                catch { /* seguimos probando */ }
            }

            // 2) Si no hubo suerte, intentamos componer: Preguntas por tipo + Respuestas por formulario
            List<(string Pregunta, string Respuesta)> pares = new();

            if (qaDoc != null)
            {
                foreach (var el in qaDoc.RootElement.EnumerateArray())
                {
                    // Admitimos varios nombres posibles
                    string pregunta =
                        (el.TryGetProperty("pregunta", out var p1) ? p1.GetString() :
                        el.TryGetProperty("textoPregunta", out var p2) ? p2.GetString() :
                        el.TryGetProperty("preguntaTexto", out var p3) ? p3.GetString() : null) ?? "—";

                    string respuesta =
                        (el.TryGetProperty("respuesta", out var r1) ? r1.GetString() :
                        el.TryGetProperty("textoRespuesta", out var r2) ? r2.GetString() :
                        el.TryGetProperty("valor", out var r3) ? r3.GetString() : null) ?? "—";

                    pares.Add((pregunta, respuesta));
                }
            }
            else
            {
                // a) Traer el formulario para conocer el tipo
                int idTipo = 0;
                try
                {
                    var fResp = await client.GetAsync($"/api/Formulario/{id_formulario}");
                    if (fResp.IsSuccessStatusCode)
                    {
                        using var fdoc = JsonDocument.Parse(await fResp.Content.ReadAsStringAsync());
                        if (fdoc.RootElement.TryGetProperty("id_tipoFormulario", out var tprop) &&
                            tprop.ValueKind == JsonValueKind.Number)
                        {
                            idTipo = tprop.GetInt32();
                        }
                    }
                }
                catch { /* noop */ }

                // b) Preguntas del tipo
                var preguntas = new Dictionary<int, string>(); // id_pregunta -> texto
                if (idTipo > 0)
                {
                    try
                    {
                        var pResp = await client.GetAsync($"/api/Pregunta?tipoFormularioId={idTipo}");
                        if (!pResp.IsSuccessStatusCode)
                            pResp = await client.GetAsync($"/api/Pregunta?id_tipoFormulario={idTipo}");

                        if (pResp.IsSuccessStatusCode)
                        {
                            using var pdoc = JsonDocument.Parse(await pResp.Content.ReadAsStringAsync());
                            if (pdoc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var el in pdoc.RootElement.EnumerateArray())
                                {
                                    if (el.TryGetProperty("id_pregunta", out var ip) && ip.ValueKind == JsonValueKind.Number)
                                    {
                                        var idp = ip.GetInt32();
                                        var txt = el.TryGetProperty("pregunta", out var tp) ? (tp.GetString() ?? "")
                                                 : el.TryGetProperty("texto", out var tp2) ? (tp2.GetString() ?? "")
                                                 : "";
                                        if (idp > 0) preguntas[idp] = string.IsNullOrWhiteSpace(txt) ? $"Pregunta #{idp}" : txt;
                                    }
                                }
                            }
                        }
                    }
                    catch { /* noop */ }
                }

                // c) Respuestas del formulario
                var respuestas = new Dictionary<int, string>(); // id_pregunta -> respuesta
                try
                {
                    var rResp = await client.GetAsync($"/api/Respuesta?formularioId={id_formulario}");
                    if (!rResp.IsSuccessStatusCode)
                        rResp = await client.GetAsync($"/api/Respuesta?id_formulario={id_formulario}");

                    if (rResp.IsSuccessStatusCode)
                    {
                        using var rdoc = JsonDocument.Parse(await rResp.Content.ReadAsStringAsync());
                        if (rdoc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var el in rdoc.RootElement.EnumerateArray())
                            {
                                int idp = 0;
                                if (el.TryGetProperty("id_pregunta", out var ip) && ip.ValueKind == JsonValueKind.Number)
                                    idp = ip.GetInt32();

                                var val = el.TryGetProperty("respuesta", out var rv) ? rv.GetString()
                                        : el.TryGetProperty("valor", out var rv2) ? rv2.GetString()
                                        : el.TryGetProperty("texto", out var rv3) ? rv3.GetString()
                                        : null;

                                if (idp > 0) respuestas[idp] = val ?? "—";
                            }
                        }
                    }
                }
                catch { /* noop */ }

                // d) Join preguntas + respuestas
                foreach (var kv in preguntas)
                {
                    var respTxt = respuestas.TryGetValue(kv.Key, out var v) ? (string.IsNullOrWhiteSpace(v) ? "—" : v) : "—";
                    pares.Add((kv.Value, respTxt));
                }
                // Si no hay preguntas pero sí respuestas sueltas, las agrego con etiquetas genéricas
                if (!preguntas.Any() && pares.Count == 0 && respuestas.Any())
                {
                    foreach (var kv in respuestas)
                        pares.Add(($"Pregunta #{kv.Key}", string.IsNullOrWhiteSpace(kv.Value) ? "—" : kv.Value));
                }
            }

            // 3) Armar HTML sin crear vista nueva
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<div class=\"card\" style=\"border:1px solid #2FA8A2; border-radius:10px;\">");
            sb.AppendLine("  <div class=\"card-body p-0\">");
            sb.AppendLine("    <table class=\"table mb-0\">");
            sb.AppendLine("      <thead>");
            sb.AppendLine("        <tr style=\"background:#E6F6F5; color:#155e59;\">");
            sb.AppendLine("          <th style=\"border-top-left-radius:10px;\">Pregunta</th>");
            sb.AppendLine("          <th style=\"border-top-right-radius:10px;\">Respuesta</th>");
            sb.AppendLine("        </tr>");
            sb.AppendLine("      </thead>");
            sb.AppendLine("      <tbody>");

            if (pares.Count == 0)
            {
                sb.AppendLine("        <tr><td colspan=\"2\" class=\"text-center text-muted p-4\">No hay preguntas/respuestas para este formulario.</td></tr>");
            }
            else
            {
                foreach (var (preg, respTxt) in pares)
                {
                    var pregEsc = System.Net.WebUtility.HtmlEncode(preg ?? "—");
                    var respEsc = System.Net.WebUtility.HtmlEncode(respTxt ?? "—");
                    sb.AppendLine($"        <tr><td style=\"width:55%;\">{pregEsc}</td><td>{respEsc}</td></tr>");
                }
            }

            sb.AppendLine("      </tbody>");
            sb.AppendLine("    </table>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");

            return Content(sb.ToString(), "text/html");
        }
        // ===================== ⚡ NUEVO: CAMBIAR ESTADO (AUDITORÍA) ===================

        [HttpPost]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> CambiarEstado(int id_persona, int id_estadoPersona)
        {
            // Buscamos el id del usuario logueado
            var claimIdUsuario = User.FindFirst("IdUsuario") ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (claimIdUsuario == null)
            {
                TempData["Error"] = "No se pudo identificar el usuario logueado para registrar la auditoría.";
                return RedirectToAction(nameof(Modificar), new { id = id_persona });
            }

            if (!int.TryParse(claimIdUsuario.Value, out var id_usuario))
            {
                TempData["Error"] = "Id de usuario inválido.";
                return RedirectToAction(nameof(Modificar), new { id = id_persona });
            }

            var client = _http.CreateClient("Api");

            var dto = new
            {
                id_estadoPersona = id_estadoPersona,
                id_usuario = id_usuario
            };

            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"{RUTA_PERSONA}/{id_persona}/estado", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"No se pudo cambiar el estado: {body}";
            }
            else
            {
                TempData["Ok"] = "Estado actualizado correctamente.";
            }

            return RedirectToAction(nameof(Modificar), new { id = id_persona });
        }


        // ===== Clase mínima para deserializar /api/Formulario en Index =====
        private class FormularioMin
        {
            public int id_formulario { get; set; }
            public int id_persona { get; set; }
            public int id_tipoFormulario { get; set; }
            public DateTime? fechaEnvio { get; set; }
            public string? estado { get; set; }
        }

        private class RespuestaMin
        {
            public int id_respuesta { get; set; }
            public int id_formulario { get; set; }
        }
        public class EliminarPersonaDto
        {
            public DateTime? fechaEliminacion { get; set; }
            public int id_usuario { get; set; }
        }
    }
}
