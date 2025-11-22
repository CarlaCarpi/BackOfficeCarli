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

            // Normalizar búsqueda
            q = (q ?? "").Trim();
            ViewBag.Query = q ?? "";

            var estadosDict = new Dictionary<int, string>();
            var provinciasDict = new Dictionary<int, string>();
            var localidadesDict = new Dictionary<int, string>();
            var usuariosDict = new Dictionary<int, string>();

            IEnumerable<Pension> pensiones = Enumerable.Empty<Pension>();
            bool hasMore = false;

            // === 1) Llamado principal a la API ===
            string url = $"{RUTA_PENSION}?pagina={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(q))
                url += $"&q={Uri.EscapeDataString(q!)}";

            var resp = await client.GetAsync(url);

            // ======================================================
            // ================   FALLBACK (API ERROR)   ============
            // ======================================================
            if (!resp.IsSuccessStatusCode)
            {
                string retryUrl = string.IsNullOrWhiteSpace(q)
                    ? RUTA_PENSION
                    : $"{RUTA_PENSION}?q={Uri.EscapeDataString(q!)}";

                var retry = await client.GetAsync(retryUrl);

                if (!retry.IsSuccessStatusCode)
                {
                    ViewBag.ApiError = $"Error al obtener pensiones: {await retry.Content.ReadAsStringAsync()}";

                    ViewBag.Page = 1;
                    ViewBag.PageSize = pageSize;
                    ViewBag.HasMore = false;

                    ViewBag.Estados = estadosDict;
                    ViewBag.Provincias = provinciasDict;
                    ViewBag.Localidades = localidadesDict;
                    ViewBag.Usuarios = usuariosDict;

                    return View(Enumerable.Empty<Pension>());
                }

                // 🔹 Traigo todo
                var all = JsonSerializer.Deserialize<IEnumerable<Pension>>(
                    await retry.Content.ReadAsStringAsync(), JsonOps
                ) ?? Enumerable.Empty<Pension>();

                // 🔥 FILTRAR eliminado
                all = all.Where(p => p.fechaEliminacion == null);

                // === Diccionarios ===
                estadosDict = await CargarEstadosPensionDictAsync(client);

                var respProv = await client.GetAsync(RUTA_PROVINCIA);
                if (respProv.IsSuccessStatusCode)
                    provinciasDict = JsonSerializer.Deserialize<IEnumerable<Provincia>>(
                        await respProv.Content.ReadAsStringAsync(), JsonOps
                    )?.ToDictionary(p => p.id_provincia, p => p.nombre) ?? new();

                var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
                if (respLoc.IsSuccessStatusCode)
                    localidadesDict = JsonSerializer.Deserialize<IEnumerable<Localidad>>(
                        await respLoc.Content.ReadAsStringAsync(), JsonOps
                    )?.ToDictionary(l => l.id_localidad, l => l.nombre) ?? new();

                var respUsr = await client.GetAsync(RUTA_USUARIO);
                if (respUsr.IsSuccessStatusCode)
                    usuariosDict = JsonSerializer.Deserialize<IEnumerable<Usuario>>(
                        await respUsr.Content.ReadAsStringAsync(), JsonOps
                    )?.ToDictionary(u => u.id_usuario,
                                    u => string.IsNullOrWhiteSpace(u.nombre) ? $"Usuario #{u.id_usuario}" : u.nombre) ?? new();

                // === Filtro local (si q tiene algo) ===
                if (!string.IsNullOrWhiteSpace(q))
                {
                    var term = q.Trim();

                    all = all.Where(p =>
                    {
                        bool porId = int.TryParse(term, out var idB) && p.id_pension == idB;
                        bool porNom = !string.IsNullOrWhiteSpace(p.nombre) &&
                                      p.nombre.Contains(term, StringComparison.OrdinalIgnoreCase);

                        bool porEstado = estadosDict.TryGetValue(p.id_estadoPension, out var eTxt) &&
                                         eTxt.Contains(term, StringComparison.OrdinalIgnoreCase);

                        bool porProv = provinciasDict.TryGetValue(p.id_provincia, out var pTxt) &&
                                       pTxt.Contains(term, StringComparison.OrdinalIgnoreCase);

                        return porId || porNom || porEstado || porProv;
                    });
                }

                // Orden y paginado
                all = all.OrderByDescending(p => p.id_pension);

                int totalLocal = all.Count();
                pensiones = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                hasMore = page * pageSize < totalLocal;

                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.HasMore = hasMore;

                ViewBag.Estados = estadosDict;
                ViewBag.Provincias = provinciasDict;
                ViewBag.Localidades = localidadesDict;
                ViewBag.Usuarios = usuariosDict;

                return View(pensiones);
            }

            // ======================================================
            // ================   API OK (NORMAL)   =================
            // ======================================================
            pensiones = JsonSerializer.Deserialize<IEnumerable<Pension>>(
                await resp.Content.ReadAsStringAsync(), JsonOps
            ) ?? Enumerable.Empty<Pension>();

            // 🔥 FILTRAR eliminado
            pensiones = pensiones.Where(p => p.fechaEliminacion == null);

            // === Diccionarios ===
            estadosDict = await CargarEstadosPensionDictAsync(client);

            var respProvOK = await client.GetAsync(RUTA_PROVINCIA);
            if (respProvOK.IsSuccessStatusCode)
                provinciasDict = JsonSerializer.Deserialize<IEnumerable<Provincia>>(
                    await respProvOK.Content.ReadAsStringAsync(), JsonOps
                )?.ToDictionary(p => p.id_provincia, p => p.nombre) ?? new();

            var respLocOK = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLocOK.IsSuccessStatusCode)
                localidadesDict = JsonSerializer.Deserialize<IEnumerable<Localidad>>(
                    await respLocOK.Content.ReadAsStringAsync(), JsonOps
                )?.ToDictionary(l => l.id_localidad, l => l.nombre) ?? new();

            var respUsrOK = await client.GetAsync(RUTA_USUARIO);
            if (respUsrOK.IsSuccessStatusCode)
                usuariosDict = JsonSerializer.Deserialize<IEnumerable<Usuario>>(
                    await respUsrOK.Content.ReadAsStringAsync(), JsonOps
                )?.ToDictionary(u => u.id_usuario,
                                u => string.IsNullOrWhiteSpace(u.nombre) ? $"Usuario #{u.id_usuario}" : u.nombre) ?? new();

            // === Filtro local (solo sobre página recibida) ===
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();

                pensiones = pensiones.Where(p =>
                {
                    bool porId = int.TryParse(term, out var idB) && p.id_pension == idB;
                    bool porNom = !string.IsNullOrWhiteSpace(p.nombre) &&
                                  p.nombre.Contains(term, StringComparison.OrdinalIgnoreCase);
                    bool porEstado = estadosDict.TryGetValue(p.id_estadoPension, out var eTxt) &&
                                     eTxt.Contains(term, StringComparison.OrdinalIgnoreCase);
                    bool porProv = provinciasDict.TryGetValue(p.id_provincia, out var pTxt) &&
                                   pTxt.Contains(term, StringComparison.OrdinalIgnoreCase);

                    return porId || porNom || porEstado || porProv;
                });
            }

            // === HasMore: detecto más páginas ===
            int total = 0;
            if (resp.Headers.TryGetValues("X-Total-Count", out var vals))
                int.TryParse(vals.FirstOrDefault(), out total);

            if (total > 0)
                hasMore = (page * pageSize) < total;
            else
            {
                // Sondeo
                var probeUrl = $"{RUTA_PENSION}?pagina={page + 1}&pageSize=1";
                if (!string.IsNullOrWhiteSpace(q))
                    probeUrl += $"&q={Uri.EscapeDataString(q)}";

                var probe = await client.GetAsync(probeUrl);
                if (probe.IsSuccessStatusCode)
                {
                    var next = JsonSerializer.Deserialize<IEnumerable<Pension>>(
                        await probe.Content.ReadAsStringAsync(), JsonOps
                    ) ?? Enumerable.Empty<Pension>();

                    // 🔥 También FILTRAR eliminadas
                    next = next.Where(p => p.fechaEliminacion == null);

                    hasMore = next.Any();
                }
            }

            // === Mensajes ===
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            // Orden final
            pensiones = pensiones.OrderByDescending(p => p.id_pension);

            // === Pasar Diccionarios ===
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.HasMore = hasMore;

            ViewBag.Estados = estadosDict;
            ViewBag.Provincias = provinciasDict;
            ViewBag.Localidades = localidadesDict;
            ViewBag.Usuarios = usuariosDict;

            return View(pensiones);
        }




        [HttpGet]
        public async Task<IActionResult> Mas(int page = 1, int pageSize = 20, string? q = null)
        {
            var client = _http.CreateClient("Api");

            // ⚠ Usá el mismo nombre de parámetro que en Index: "pagina"
            string url = $"{RUTA_PENSION}?pagina={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(q))
                url += $"&q={Uri.EscapeDataString(q.Trim())}";

            var resp = await client.GetAsync(url);

            if (!resp.IsSuccessStatusCode)
                return StatusCode((int)resp.StatusCode, "Error al obtener más pensiones.");

            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<List<Pension>>(json, JsonOps)
                        ?? new List<Pension>();

            if (!lista.Any())
                return NoContent(); // 204 → el JS lo interpreta como "No hay más"

            // ========== 🔹 CARGAR DICCIONARIOS IGUAL QUE EN INDEX ==========

            var estadosDict = await CargarEstadosPensionDictAsync(client);

            var provinciasDict = new Dictionary<int, string>();
            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var provJson = await respProv.Content.ReadAsStringAsync();
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(provJson, JsonOps)
                                 ?? Enumerable.Empty<Provincia>();
                provinciasDict = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }

            var localidadesDict = new Dictionary<int, string>();
            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var locJson = await respLoc.Content.ReadAsStringAsync();
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(locJson, JsonOps)
                                  ?? Enumerable.Empty<Localidad>();
                localidadesDict = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }

            var usuariosDict = new Dictionary<int, string>();
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

            // Pasar diccionarios al parcial (PensionRows los usa)
            ViewBag.Estados = estadosDict;
            ViewBag.Provincias = provinciasDict;
            ViewBag.Localidades = localidadesDict;
            ViewBag.Usuarios = usuariosDict;

            // ========== 🔹 HAS MORE PARA EL JS ==========

            bool hasMore = false;

            // Si el API manda X-Total-Count, podés reutilizar la lógica de Index,
            // pero si no, con esto simple alcanza:
            if (lista.Count == pageSize)
                hasMore = true;

            Response.Headers["X-HasMore"] = hasMore.ToString();

            // volvemos SOLO las filas <tr>
            return PartialView("_PensionRows", lista);
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
            // 👉 Este campo lo vamos a setear nosotros, no el form
            ModelState.Remove(nameof(model.id_usuario));

            // 👉 Tomar el usuario logueado (igual que en Persona / Animal)
            var claimIdUsuario = User.FindFirst("IdUsuario")
                                  ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (claimIdUsuario != null && int.TryParse(claimIdUsuario.Value, out var idUsu))
                model.id_usuario = idUsu;

            // Normalizar teléfonos
            model.telefono1 = model.telefono1?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(model.telefono2))
                model.telefono2 = model.telefono2!.Trim();

            // Siempre que se modifica, actualizar fechaEgreso a ahora
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
            var resp = await client.GetAsync($"{RUTA_PENSION}/{id}"); // /api/Pension/{id}

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "La pensión no existe o ya fue eliminada.";
                return RedirectToAction(nameof(Index));
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"GET {RUTA_PENSION}/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}.";
                TempData["ApiDetail"] = body;
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Pension>(json, JsonOps);

            if (model == null)
            {
                TempData["Error"] = "No se pudo leer la pensión desde la API.";
                return RedirectToAction(nameof(Index));
            }

            // 👉 Usuario actual que va a ELIMINAR
            var idUsuarioActual = GetCurrentUserId();
            if (idUsuarioActual != 0)
                model.id_usuario = idUsuarioActual;

            // 👉 Fecha de eliminación (también la mandamos al form oculta)
            model.fechaEliminacion = DateTime.Now;

            // 👉 Diccionarios para mostrar Provincia, Localidad, Estado, Usuario
            await CargarDiccionariosPension();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Eliminar(Pension model)
        {
            var client = _http.CreateClient("Api");

            // Refuerzo del lado servidor
            if (!model.fechaEliminacion.HasValue)
                model.fechaEliminacion = DateTime.Now;

            if (model.id_usuario == 0)
                model.id_usuario = GetCurrentUserId();

            var json = JsonSerializer.Serialize(model, JsonOps);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 👉 Llama al endpoint de la API: [HttpPut("Eliminar/{id:int}")]
            var resp = await client.PutAsync($"{RUTA_PENSION}/Eliminar/{model.id_pension}", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"Error al eliminar la pensión: {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                TempData["ApiDetail"] = body;
                return RedirectToAction(nameof(Index));
            }

            TempData["Ok"] = "Pensión eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }





        private async Task CargarDiccionariosPension()
        {
            var client = _http.CreateClient("Api");
            var ops = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // PROVINCIAS
            var respProv = await client.GetAsync("/api/Provincia");
            if (respProv.IsSuccessStatusCode)
            {
                var jsonProv = await respProv.Content.ReadAsStringAsync();
                var listaProv = JsonSerializer.Deserialize<List<Provincia>>(jsonProv, ops) ?? new();
                ViewBag.Provincias = listaProv
                    .GroupBy(p => p.id_provincia)
                    .ToDictionary(g => g.Key, g => g.First().nombre ?? "");
            }
            else
            {
                ViewBag.Provincias = new Dictionary<int, string>();
            }

            // LOCALIDADES
            var respLoc = await client.GetAsync("/api/Localidad");
            if (respLoc.IsSuccessStatusCode)
            {
                var jsonLoc = await respLoc.Content.ReadAsStringAsync();
                var listaLoc = JsonSerializer.Deserialize<List<Localidad>>(jsonLoc, ops) ?? new();
                ViewBag.Localidades = listaLoc
                    .GroupBy(l => l.id_localidad)
                    .ToDictionary(g => g.Key, g => g.First().nombre ?? "");
            }
            else
            {
                ViewBag.Localidades = new Dictionary<int, string>();
            }

            // ESTADOS DE PENSIÓN
            var respEst = await client.GetAsync("/api/EstadoPension");
            if (respEst.IsSuccessStatusCode)
            {
                var jsonEst = await respEst.Content.ReadAsStringAsync();
                var listaEst = JsonSerializer.Deserialize<List<Estado_Pension>>(jsonEst, ops) ?? new();
                ViewBag.EstadosPension = listaEst
                    .GroupBy(e => e.id_estadoPension)
                    .ToDictionary(g => g.Key, g => g.First().descripcion ?? "");
            }
            else
            {
                ViewBag.EstadosPension = new Dictionary<int, string>();
            }

            // USUARIOS
            var respUsr = await client.GetAsync("/api/Usuario");
            if (respUsr.IsSuccessStatusCode)
            {
                var jsonUsr = await respUsr.Content.ReadAsStringAsync();
                var listaUsr = JsonSerializer.Deserialize<List<Usuario>>(jsonUsr, ops) ?? new();
                ViewBag.Usuarios = listaUsr
                    .GroupBy(u => u.id_usuario)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var u = g.First();
                            var nombre = (u.nombre ?? "").Trim();
                            var apellido = (u.apellido ?? "").Trim();
                            var full = (nombre + " " + apellido).Trim();
                            return string.IsNullOrWhiteSpace(full) ? $"Usuario #{u.id_usuario}" : full;
                        });
            }
            else
            {
                ViewBag.Usuarios = new Dictionary<int, string>();
            }
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
