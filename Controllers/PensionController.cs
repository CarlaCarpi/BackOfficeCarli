using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    [Authorize(Policy = "Activo")]
    public class PensionController : Controller
    {
        private readonly IHttpClientFactory _http;
        public PensionController(IHttpClientFactory http) => _http = http;

        // ====== Rutas API (ajustá si tu API usa otros nombres) ======
        private const string RUTA_PENSION = "/api/Pension";
        private const string RUTA_ESTADO_PENSION = "/api/EstadoPension";
        private const string RUTA_PROVINCIA = "/api/Provincia";
        private const string RUTA_LOCALIDAD = "/api/Localidad";
        private const string RUTA_USUARIO = "/api/Usuario";

        private static readonly JsonSerializerOptions JsonOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ============================================================
        // ===================== MÉTODOS AUXILIARES ===================
        // ============================================================

        private async Task<SelectList> CargarEstadosPensionSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_ESTADO_PENSION);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());
            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<IEnumerable<Estado_Pension>>(json, JsonOps) ?? Enumerable.Empty<Estado_Pension>();
            var dict = lista.ToDictionary(e => e.id_estadoPension, e => e.descripcion);
            return new SelectList(dict, "Key", "Value", seleccionado);
        }

        private async Task<Dictionary<int, string>> CargarEstadosPensionDictAsync(HttpClient client)
        {
            var resp = await client.GetAsync(RUTA_ESTADO_PENSION);
            if (!resp.IsSuccessStatusCode) return new Dictionary<int, string>();
            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<IEnumerable<Estado_Pension>>(json, JsonOps) ?? Enumerable.Empty<Estado_Pension>();
            return lista.ToDictionary(e => e.id_estadoPension, e => e.descripcion);
        }

        private async Task<SelectList> CargarProvinciasSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_PROVINCIA);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());
            var json = await resp.Content.ReadAsStringAsync();
            var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(json, JsonOps) ?? Enumerable.Empty<Provincia>();
            return new SelectList(provincias.Select(p => new { p.id_provincia, p.nombre }), "id_provincia", "nombre", seleccionado);
        }

        private async Task<SelectList> CargarLocalidadesSelectAsync(HttpClient client, int? idProvincia, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_LOCALIDAD);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());
            var json = await resp.Content.ReadAsStringAsync();
            var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(json, JsonOps) ?? Enumerable.Empty<Localidad>();
            if (idProvincia is not null && idProvincia > 0)
                localidades = localidades.Where(l => l.id_provincia == idProvincia);
            return new SelectList(localidades.Select(l => new { l.id_localidad, l.nombre }), "id_localidad", "nombre", seleccionado);
        }

        private async Task<SelectList> CargarUsuariosSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync("/api/Usuario");
            if (!resp.IsSuccessStatusCode)
            return new SelectList(Enumerable.Empty<SelectListItem>());

            var json = await resp.Content.ReadAsStringAsync();
            var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(json, JsonOps)
                   ?? Enumerable.Empty<Usuario>();

            var lista = usuarios
        .Select(u => new
        {
            id = u.id_usuario,
            NombreCompleto = $"{u.apellido}, {u.nombre}"
        })
        .OrderBy(u => u.NombreCompleto);

         return new SelectList(lista, "id", "NombreCompleto", seleccionado);
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
            ViewBag.Query = q ?? string.Empty;

            // Diccionarios que vamos a usar en filtro y en la vista
            var estadosDict = new Dictionary<int, string>();
            var provinciasDict = new Dictionary<int, string>();
            var localidadesDict = new Dictionary<int, string>();
            var usuariosDict = new Dictionary<int, string>();

            IEnumerable<Pension> pensiones = Enumerable.Empty<Pension>();
            bool hasMore = false;

            // === 1) Pido a la API con paginación (y q si viene) ===
            string url = $"{RUTA_PENSION}?pagina={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(q))
                url += $"&q={Uri.EscapeDataString(q)}";

            var resp = await client.GetAsync(url);

            if (!resp.IsSuccessStatusCode)
            {
                // Fallback sin paginación (último recurso)
                var retryUrl = string.IsNullOrWhiteSpace(q)
                    ? RUTA_PENSION
                    : $"{RUTA_PENSION}?q={Uri.EscapeDataString(q!)}";

                var retry = await client.GetAsync(retryUrl);

                if (!retry.IsSuccessStatusCode)
                {
                    var body = await retry.Content.ReadAsStringAsync();
                    ViewBag.ApiError = $"Error al obtener pensiones: {body}";

                    ViewBag.Page = 1;
                    ViewBag.PageSize = pageSize;
                    ViewBag.HasMore = false;

                    ViewBag.Estados = estadosDict;
                    ViewBag.Provincias = provinciasDict;
                    ViewBag.Localidades = localidadesDict;
                    ViewBag.Usuarios = usuariosDict;

                    return View(Enumerable.Empty<Pension>());
                }

                var all = JsonSerializer.Deserialize<IEnumerable<Pension>>(
                    await retry.Content.ReadAsStringAsync(), JsonOps
                ) ?? Enumerable.Empty<Pension>();

                // === Diccionarios para filtro y vista ===
                estadosDict = await CargarEstadosPensionDictAsync(client);

                var respProv = await client.GetAsync(RUTA_PROVINCIA);
                if (respProv.IsSuccessStatusCode)
                {
                    var provJson = await respProv.Content.ReadAsStringAsync();
                    var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(provJson, JsonOps)
                                     ?? Enumerable.Empty<Provincia>();
                    provinciasDict = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
                }

                var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
                if (respLoc.IsSuccessStatusCode)
                {
                    var locJson = await respLoc.Content.ReadAsStringAsync();
                    var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(locJson, JsonOps)
                                      ?? Enumerable.Empty<Localidad>();
                    localidadesDict = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
                }

                var respUsr = await client.GetAsync(RUTA_USUARIO);
                if (respUsr.IsSuccessStatusCode)
                {
                    var usrJson = await respUsr.Content.ReadAsStringAsync();
                    var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(usrJson, JsonOps)
                                   ?? Enumerable.Empty<Usuario>();
                    usuariosDict = usuarios.ToDictionary(
                        u => u.id_usuario,
                        u => string.IsNullOrWhiteSpace(u.nombre) ? $"Usuario #{u.id_usuario}" : u.nombre
                    );
                }

                // === Filtro local (Nombre, EstadoPension, Provincia, y de yapa ID) ===
                if (!string.IsNullOrWhiteSpace(q))
                {
                    var term = q.Trim();

                    all = all.Where(p =>
                    {
                        // Por ID
                        bool porId = int.TryParse(term, out var idBuscado) && p.id_pension == idBuscado;

                        // Por nombre
                        bool porNombre = !string.IsNullOrWhiteSpace(p.nombre) &&
                                         p.nombre.Contains(term, StringComparison.OrdinalIgnoreCase);

                        // Por estado de la pensión (texto)
                        string estadoTxt = estadosDict.TryGetValue(p.id_estadoPension, out var eTxt) ? eTxt : "";
                        bool porEstado = !string.IsNullOrWhiteSpace(estadoTxt) &&
                                         estadoTxt.Contains(term, StringComparison.OrdinalIgnoreCase);

                        // Por provincia (texto)
                        string provTxt = provinciasDict.TryGetValue(p.id_provincia, out var pTxt) ? pTxt : "";
                        bool porProvincia = !string.IsNullOrWhiteSpace(provTxt) &&
                                            provTxt.Contains(term, StringComparison.OrdinalIgnoreCase);

                        return porId || porNombre || porEstado || porProvincia;
                    });
                }

                all = all.OrderByDescending(p => p.id_pension);
                var totalLocal = all.Count();

                pensiones = all
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                hasMore = page * pageSize < totalLocal;

                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.HasMore = hasMore;
            }
            else
            {
                // API respondió bien (paginación server-side)
                pensiones = JsonSerializer.Deserialize<IEnumerable<Pension>>(
                    await resp.Content.ReadAsStringAsync(), JsonOps
                ) ?? Enumerable.Empty<Pension>();

                // === Diccionarios para filtro y vista ===
                estadosDict = await CargarEstadosPensionDictAsync(client);

                var respProv = await client.GetAsync(RUTA_PROVINCIA);
                if (respProv.IsSuccessStatusCode)
                {
                    var provJson = await respProv.Content.ReadAsStringAsync();
                    var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(provJson, JsonOps)
                                     ?? Enumerable.Empty<Provincia>();
                    provinciasDict = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
                }

                var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
                if (respLoc.IsSuccessStatusCode)
                {
                    var locJson = await respLoc.Content.ReadAsStringAsync();
                    var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(locJson, JsonOps)
                                      ?? Enumerable.Empty<Localidad>();
                    localidadesDict = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
                }

                var respUsr = await client.GetAsync(RUTA_USUARIO);
                if (respUsr.IsSuccessStatusCode)
                {
                    var usrJson = await respUsr.Content.ReadAsStringAsync();
                    var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(usrJson, JsonOps)
                                   ?? Enumerable.Empty<Usuario>();
                    usuariosDict = usuarios.ToDictionary(
                        u => u.id_usuario,
                        u => string.IsNullOrWhiteSpace(u.nombre) ? $"Usuario #{u.id_usuario}" : u.nombre
                    );
                }

                // === Filtro local sobre la página devuelta por la API ===
                if (!string.IsNullOrWhiteSpace(q))
                {
                    var term = q.Trim();

                    pensiones = pensiones.Where(p =>
                    {
                        // Por ID
                        bool porId = int.TryParse(term, out var idBuscado) && p.id_pension == idBuscado;

                        // Por nombre
                        bool porNombre = !string.IsNullOrWhiteSpace(p.nombre) &&
                                         p.nombre.Contains(term, StringComparison.OrdinalIgnoreCase);

                        // Por estado de la pensión (texto)
                        string estadoTxt = estadosDict.TryGetValue(p.id_estadoPension, out var eTxt) ? eTxt : "";
                        bool porEstado = !string.IsNullOrWhiteSpace(estadoTxt) &&
                                         estadoTxt.Contains(term, StringComparison.OrdinalIgnoreCase);

                        // Por provincia (texto)
                        string provTxt = provinciasDict.TryGetValue(p.id_provincia, out var pTxt) ? pTxt : "";
                        bool porProvincia = !string.IsNullOrWhiteSpace(provTxt) &&
                                            provTxt.Contains(term, StringComparison.OrdinalIgnoreCase);

                        return porId || porNombre || porEstado || porProvincia;
                    });
                }

                // === HasMore como antes (por header o sondeo) ===
                int total = 0;
                bool hasHeader = resp.Headers.TryGetValues("X-Total-Count", out var vals);
                if (hasHeader)
                    int.TryParse(vals!.FirstOrDefault(), out total);

                if (total > 0)
                {
                    hasMore = (page * pageSize) < total;
                }
                else
                {
                    var probeUrl = $"{RUTA_PENSION}?pagina={page + 1}&pageSize=1";
                    if (!string.IsNullOrWhiteSpace(q))
                        probeUrl += $"&q={Uri.EscapeDataString(q.Trim())}";

                    var probe = await client.GetAsync(probeUrl);
                    if (probe.IsSuccessStatusCode)
                    {
                        var pj = await probe.Content.ReadAsStringAsync();
                        var next = JsonSerializer.Deserialize<IEnumerable<Pension>>(pj, JsonOps)
                                   ?? Enumerable.Empty<Pension>();
                        hasMore = next.Any();
                    }
                    else hasMore = false;
                }

                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.HasMore = hasMore;
            }

            // Mensajes (si los usás)
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            // Orden final por ID
            pensiones = pensiones.OrderByDescending(p => p.id_pension);

            // Pasar diccionarios a la vista
            ViewBag.Estados = estadosDict;
            ViewBag.Provincias = provinciasDict;
            ViewBag.Localidades = localidadesDict;
            ViewBag.Usuarios = usuariosDict;

            return View(pensiones);
        }





        // ============================================================
        // ===================== DETALLE ==============================
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var client = _http.CreateClient("Api");

            var resp = await client.GetAsync($"{RUTA_PENSION}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"Error al obtener pensión #{id}.";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Pension>(await resp.Content.ReadAsStringAsync(), JsonOps);

            ViewBag.Estados = await CargarEstadosPensionDictAsync(client);

            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(await respProv.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Provincia>();
                ViewBag.Provincias = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }
            else ViewBag.Provincias = new Dictionary<int, string>();

            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(await respLoc.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Localidad>();
                ViewBag.Localidades = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }
            else ViewBag.Localidades = new Dictionary<int, string>();

            var respUsr = await client.GetAsync(RUTA_USUARIO);
            if (respUsr.IsSuccessStatusCode)
            {
                var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(await respUsr.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Usuario>();
                ViewBag.Usuarios = usuarios.ToDictionary(u => u.id_usuario, u => string.IsNullOrWhiteSpace(u.nombre) ? $"Usuario #{u.id_usuario}" : u.nombre);
            }
            else ViewBag.Usuarios = new Dictionary<int, string>();

            return PartialView(model);
        }

        // ============================================================
        // ===================== CREAR ================================
        // ============================================================
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear()
        {
            var client = _http.CreateClient("Api");
            ViewBag.Estados = await CargarEstadosPensionSelectAsync(client);
            ViewBag.Provincias = await CargarProvinciasSelectAsync(client);
            ViewBag.Localidades = new SelectList(Enumerable.Empty<SelectListItem>());

            // ❌ No cargamos Usuarios: se asigna automáticamente
            // ViewBag.Usuarios = await CargarUsuariosSelectAsync(client);

            return View(new Pension { fechaIngreso = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear([FromForm] Pension model)
        {
            // 🔐 Forzar usuario autenticado en el registro (ignorar lo que venga del form)
            model.id_usuario = GetCurrentUserId();
            // Si tu modelo tenía [Required] en id_usuario: ya queda cubierto antes de validar.

            // Normalizaciones mínimas
            model.telefono1 = model.telefono1?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(model.telefono2)) model.telefono2 = model.telefono2!.Trim();
            if (model.fechaIngreso == default) model.fechaIngreso = DateTime.Today;

            // 🧹 Por si el form traía un id_usuario (campo oculto), lo ignoramos
            if (Request.Form.ContainsKey("id_usuario"))
                ModelState.Remove(nameof(model.id_usuario));

            if (!ModelState.IsValid)
            {
                var clientErr = _http.CreateClient("Api");
                ViewBag.Estados = await CargarEstadosPensionSelectAsync(clientErr, model.id_estadoPension);
                ViewBag.Provincias = await CargarProvinciasSelectAsync(clientErr, model.id_provincia);
                ViewBag.Localidades = await CargarLocalidadesSelectAsync(clientErr, model.id_provincia, model.id_localidad);
                // ❌ No cargamos usuarios
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync(RUTA_PENSION, content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al guardar pensión: {body}";
                ViewBag.Estados = await CargarEstadosPensionSelectAsync(client, model.id_estadoPension);
                ViewBag.Provincias = await CargarProvinciasSelectAsync(client, model.id_provincia);
                ViewBag.Localidades = await CargarLocalidadesSelectAsync(client, model.id_provincia, model.id_localidad);
                // ❌ No cargamos usuarios
                return View(model);
            }

            TempData["Ok"] = "Pensión creada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // Helper para obtener el ID de usuario desde los claims
        private int GetCurrentUserId()
        {
            // Ajustá estos intentos según cómo seteás el claim en tu login/JWT
            string? raw =
                User.FindFirstValue("id_usuario") ??
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("sub") ??
                "0";

            return int.TryParse(raw, out var id) ? id : 0;
        }

        // ============================================================
        // ===================== MODIFICAR ============================
        // ============================================================
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{RUTA_PENSION}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"No se encontró la pensión #{id}.";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Pension>(await resp.Content.ReadAsStringAsync(), JsonOps);

            ViewBag.Estados = await CargarEstadosPensionSelectAsync(client, model?.id_estadoPension);
            ViewBag.Provincias = await CargarProvinciasSelectAsync(client, model?.id_provincia);
            ViewBag.Localidades = await CargarLocalidadesSelectAsync(client, model?.id_provincia, model?.id_localidad);
            ViewBag.Usuarios = await CargarUsuariosSelectAsync(client, model?.id_usuario);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar([FromForm] Pension model)
        {
            // Normalizar teléfonos
            model.telefono1 = model.telefono1?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(model.telefono2))
                model.telefono2 = model.telefono2!.Trim();

            // 👉 Siempre que se modifica, actualizar fechaEgreso a ahora
            model.fechaEgreso = DateTime.Now;

            if (!ModelState.IsValid)
            {
                var clientErr = _http.CreateClient("Api");
                ViewBag.Estados = await CargarEstadosPensionSelectAsync(clientErr, model.id_estadoPension);
                ViewBag.Provincias = await CargarProvinciasSelectAsync(clientErr, model.id_provincia);
                ViewBag.Localidades = await CargarLocalidadesSelectAsync(clientErr, model.id_provincia, model.id_localidad);
                ViewBag.Usuarios = await CargarUsuariosSelectAsync(clientErr, model.id_usuario);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await client.PutAsync($"{RUTA_PENSION}/{model.id_pension}", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al actualizar pensión: {body}";
                ViewBag.Estados = await CargarEstadosPensionSelectAsync(client, model.id_estadoPension);
                ViewBag.Provincias = await CargarProvinciasSelectAsync(client, model.id_provincia);
                ViewBag.Localidades = await CargarLocalidadesSelectAsync(client, model.id_provincia, model.id_localidad);
                ViewBag.Usuarios = await CargarUsuariosSelectAsync(client, model.id_usuario);
                return View(model);
            }

            TempData["Ok"] = "Pensión actualizada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // ===================== ELIMINAR =============================
        // ============================================================
        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{RUTA_PENSION}/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "La pensión no existe o ya fue eliminada.";
                return RedirectToAction(nameof(Index));
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"Error al obtener pensión: {body}";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Pension>(await resp.Content.ReadAsStringAsync(), JsonOps);
            ViewBag.Estados = await CargarEstadosPensionDictAsync(client);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"{RUTA_PENSION}/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"Error al eliminar pensión: {body}";
                return RedirectToAction(nameof(Index));
            }

            TempData["Ok"] = "Pensión eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // ====== AJAX: Localidades por Provincia (filtrado MVC) ======
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> LocalidadesPorProvincia(int provinciaId)
        {
            var client = _http.CreateClient("Api");

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
    }
}
