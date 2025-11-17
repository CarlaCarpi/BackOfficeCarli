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
    public class UsuarioController : Controller
    {
        private readonly IHttpClientFactory _http;
        private const string ADMIN_ROLE_NAME = "administrador";

        // Ruta base para paginación
        private const string RUTA_USUARIO = "/api/usuario";

        private static readonly string[] EmailsProtegidos = new[]
        {
            "admin@santaramona.somee.com",
            "santaramonaprotectora@gmail.com"
        };

        private static bool EsEmailProtegido(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var normalizado = email.Trim().ToLower();
            return EmailsProtegidos.Any(e => e.Equals(normalizado, StringComparison.OrdinalIgnoreCase));
        }

        private static readonly JsonSerializerOptions JsonOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public UsuarioController(IHttpClientFactory http) => _http = http;

        // ===================== HELPERS (ROLES) =====================

        private async Task<int?> GetAdminRoleIdAsync()
        {
            var client = _http.CreateClient("Api");
            var r = await client.GetAsync("/api/rol");
            if (!r.IsSuccessStatusCode) return null;

            var js = await r.Content.ReadAsStringAsync();
            var roles = JsonSerializer.Deserialize<IEnumerable<Rol>>(js, JsonOps) ?? Enumerable.Empty<Rol>();
            return roles
                .Where(x => (x.descripcion ?? "").Trim().ToLower() == ADMIN_ROLE_NAME)
                .Select(x => (int?)x.id_rol)
                .FirstOrDefault();
        }

        private async Task<bool> EsAdministradorAsync(int idUsuario)
        {
            var client = _http.CreateClient("Api");
            var rUser = await client.GetAsync($"/api/usuario/{idUsuario}");
            if (!rUser.IsSuccessStatusCode)
                return false;

            var ju = await rUser.Content.ReadAsStringAsync();
            var u = JsonSerializer.Deserialize<Usuario>(ju, JsonOps);
            if (u is null || (u.id_rol ?? 0) <= 0)
                return false;

            var adminId = await GetAdminRoleIdAsync();
            return adminId.HasValue && u.id_rol == adminId.Value;
        }

        private async Task<bool> EsUnicoAdministradorAsync(int idUsuario)
        {
            var adminId = await GetAdminRoleIdAsync();
            if (!adminId.HasValue)
                return false;

            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/usuario");
            if (!resp.IsSuccessStatusCode)
                return false;

            var json = await resp.Content.ReadAsStringAsync();
            var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(json, JsonOps) ?? Enumerable.Empty<Usuario>();

            var admins = usuarios.Where(u => u.id_rol == adminId.Value).ToList();
            return admins.Count == 1 && admins.First().id_usuario == idUsuario;
        }

        private async Task<bool> EsEmailProtegidoAsync(int idUsuario)
        {
            var client = _http.CreateClient("Api");
            var rUser = await client.GetAsync($"/api/usuario/{idUsuario}");
            if (!rUser.IsSuccessStatusCode)
                return false;

            var ju = await rUser.Content.ReadAsStringAsync();
            var u = JsonSerializer.Deserialize<Usuario>(ju, JsonOps);
            if (u is null)
                return false;

            return EsEmailProtegido(u.email);
        }


        // ===================== INDEX con paginación =====================

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20, string? q = null)
        {
            var client = _http.CreateClient("Api");

            // URL paginada (si tu API todavía no filtra por q, esto igual te sirve)
            var url = $"{RUTA_USUARIO}?pagina={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(q))
                url += $"&q={Uri.EscapeDataString(q.Trim())}";

            var respUsuarios = await client.GetAsync(url);
            if (!respUsuarios.IsSuccessStatusCode)
            {
                var body = await respUsuarios.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET {url} -> {(int)respUsuarios.StatusCode} {respUsuarios.ReasonPhrase}. Respuesta: {body}";
                ViewBag.Estados = new Dictionary<int, string>();
                ViewBag.RolPorUsuario = new Dictionary<int, string>();
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.HasMore = false;
                ViewBag.Query = q ?? "";
                return View(Enumerable.Empty<Usuario>());
            }

            var usersJson = await respUsuarios.Content.ReadAsStringAsync();
            var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(usersJson, JsonOps) ?? Enumerable.Empty<Usuario>();

            // Catálogos
            var tEstados = client.GetAsync("/api/Estado_Usuario");
            var tRoles = client.GetAsync("/api/rol");
            await Task.WhenAll(tEstados, tRoles);

            ViewBag.Estados = await ToDict<Estado_Usuario>(tEstados.Result, x => x.id_estadoUsuario, x => x.descripcion);

            var dictRoles = await ToDict<Rol>(tRoles.Result, x => x.id_rol, x => x.descripcion ?? "-");

            var rolPorUsuario = usuarios.ToDictionary(
                u => u.id_usuario,
                u =>
                {
                    var rid = u.id_rol ?? 0;
                    return (rid > 0 && dictRoles.ContainsKey(rid)) ? dictRoles[rid] : "-";
                });

            // Filtro client-side adicional, igual que tu versión original
            if (!string.IsNullOrWhiteSpace(q))
            {
                string ql = q.Trim().ToLower();
                usuarios = usuarios.Where(u =>
                    u.id_usuario.ToString().Contains(ql)
                    || (u.nombre ?? "").ToLower().Contains(ql)
                    || (u.apellido ?? "").ToLower().Contains(ql)
                    || (u.email ?? "").ToLower().Contains(ql)
                    || ((rolPorUsuario.TryGetValue(u.id_usuario, out var rr) ? rr.ToLower() : "")).Contains(ql)
                ).ToList();
            }

            // Orden por código (como tenías)
            usuarios = usuarios.OrderByDescending(p => p.id_usuario);

            // HasMore (mismo patrón que Animal)
            int total = 0;
            bool hasHeader = respUsuarios.Headers.TryGetValues("X-Total-Count", out var vals);
            if (hasHeader) int.TryParse(vals!.FirstOrDefault(), out total);

            bool hasMore;
            if (total > 0)
            {
                hasMore = (page * pageSize) < total;
            }
            else
            {
                var probeUrl = $"{RUTA_USUARIO}?pagina={page + 1}&pageSize=1";
                if (!string.IsNullOrWhiteSpace(q)) probeUrl += $"&q={Uri.EscapeDataString(q.Trim())}";
                var probe = await client.GetAsync(probeUrl);
                if (probe.IsSuccessStatusCode)
                {
                    var pj = await probe.Content.ReadAsStringAsync();
                    var next = JsonSerializer.Deserialize<IEnumerable<Usuario>>(pj, JsonOps) ?? Enumerable.Empty<Usuario>();
                    hasMore = next.Any();
                }
                else hasMore = false;
            }

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.HasMore = hasMore;
            ViewBag.Query = q ?? "";
            ViewBag.RolPorUsuario = rolPorUsuario;

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(usuarios);
        }

        // === Acción AJAX "Ver más" (usa el mismo partial _UsuarioRows que ya tenés) ===
        [HttpGet]

        public async Task<IActionResult> Mas(int page = 2, int pageSize = 20, string? q = null)
        {
            var client = _http.CreateClient("Api");

            var url = $"{RUTA_USUARIO}?pagina={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(q))
                url += $"&q={Uri.EscapeDataString(q.Trim())}";

            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return Content("");

            var json = await resp.Content.ReadAsStringAsync();
            var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(json, JsonOps) ?? Enumerable.Empty<Usuario>();

            // Catálogos para el partial
            var tEstados = client.GetAsync("/api/Estado_Usuario");
            var tRoles = client.GetAsync("/api/rol");
            await Task.WhenAll(tEstados, tRoles);

            ViewBag.Estados = await ToDict<Estado_Usuario>(tEstados.Result, x => x.id_estadoUsuario, x => x.descripcion);
            var dictRoles = await ToDict<Rol>(tRoles.Result, x => x.id_rol, x => x.descripcion ?? "-");

            var rolPorUsuario = usuarios.ToDictionary(
                u => u.id_usuario,
                u =>
                {
                    var rid = u.id_rol ?? 0;
                    return (rid > 0 && dictRoles.ContainsKey(rid)) ? dictRoles[rid] : "-";
                });
            ViewBag.RolPorUsuario = rolPorUsuario;

            // Ordenados igual que el Index
            usuarios = usuarios.OrderByDescending(u => u.id_usuario);

            // HasMore
            int total = 0;
            bool hasHeader = resp.Headers.TryGetValues("X-Total-Count", out var vals);
            if (hasHeader) int.TryParse(vals!.FirstOrDefault(), out total);

            bool hasMore;
            if (total > 0)
            {
                hasMore = (page * pageSize) < total;
            }
            else
            {
                var probeUrl = $"{RUTA_USUARIO}?pagina={page + 1}&pageSize=1";
                if (!string.IsNullOrWhiteSpace(q)) probeUrl += $"&q={Uri.EscapeDataString(q.Trim())}";
                var probe = await client.GetAsync(probeUrl);
                if (probe.IsSuccessStatusCode)
                {
                    var pj = await probe.Content.ReadAsStringAsync();
                    var next = JsonSerializer.Deserialize<IEnumerable<Usuario>>(pj, JsonOps) ?? Enumerable.Empty<Usuario>();
                    hasMore = next.Any();
                }
                else hasMore = false;
            }

            if (!usuarios.Any())
            {
                Response.Headers["X-HasMore"] = "false";
                return NoContent();
            }

            Response.Headers["X-HasMore"] = hasMore ? "true" : "false";
            return PartialView("_UsuarioRows", usuarios);
        }


        // ===================== DETALLE (MODAL) =====================

        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/usuario/{id}");
            if (!resp.IsSuccessStatusCode) return NotFound();

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Usuario>(json, JsonOps);
            if (model is null) return NotFound();

            await CargarDiccionariosBasicos();
            return PartialView("DetalleUsuario", model);
        }

        // ===================== CREAR =====================

        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear()
        {
            await CargarSelects();
            return View(new Usuario());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear(Usuario model)
        {
            if (!(model.id_rol.HasValue && model.id_rol.Value > 0))
                ModelState.AddModelError(nameof(Usuario.id_rol), "Seleccione un rol.");

            if (model.id_estadoUsuario <= 0)
                ModelState.AddModelError(nameof(Usuario.id_estadoUsuario), "Seleccione un estado.");

            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_estadoUsuario, model.id_rol);
                return View(model);
            }

            if (model.fechaAlta == default) model.fechaAlta = DateTime.Now;

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { PropertyNamingPolicy = null });
            var resp = await client.PostAsync("/api/usuario", new StringContent(json, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "Error al crear usuario.";
                await CargarSelects(model.id_estadoUsuario, model.id_rol);
                return View(model);
            }

            TempData["Ok"] = "Usuario creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===================== MODIFICAR =====================

        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");

            var resp = await client.GetAsync($"/api/usuario/{id}");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El usuario no existe.";
                return RedirectToAction(nameof(Index));
            }
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"GET /api/usuario/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Usuario>(json, JsonOps);
            if (model == null)
            {
                TempData["Error"] = "No se pudo deserializar el usuario.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.clave))
            {
                try
                {
                    var r2 = await client.GetAsync($"/api/usuario/{id}/clave");
                    if (r2.IsSuccessStatusCode)
                    {
                        var raw = await r2.Content.ReadAsStringAsync();
                        string? clave = null;
                        try
                        {
                            using var doc = JsonDocument.Parse(raw);
                            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                            {
                                if (doc.RootElement.TryGetProperty("clave", out var c1) && c1.ValueKind == JsonValueKind.String)
                                    clave = c1.GetString();
                                else if (doc.RootElement.TryGetProperty("password", out var c2) && c2.ValueKind == JsonValueKind.String)
                                    clave = c2.GetString();
                            }
                        }
                        catch
                        {
                            clave = raw?.Trim().Trim('"');
                        }
                        if (!string.IsNullOrWhiteSpace(clave))
                            model.clave = clave!;
                    }
                }
                catch { }
            }

            ViewData["ClaveReal"] = model.clave ?? "";

            if (!model.id_rol.HasValue) model.id_rol = 0;

            await CargarSelects(model.id_estadoUsuario, model.id_rol);
            ViewBag.EsAdmin = await EsAdministradorAsync(model.id_usuario);
            ViewBag.UnicoAdmin = await EsUnicoAdministradorAsync(model.id_usuario);
            ViewBag.EsProtegido = EsEmailProtegido(model.email);
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar(Usuario model)
        {
            // No queremos que valide la clave
            ModelState.Remove(nameof(Usuario.clave));

            var client = _http.CreateClient("Api");

            // Traemos el usuario actual UNA vez
            Usuario? actual = null;
            try
            {
                var rGet = await client.GetAsync($"/api/usuario/{model.id_usuario}");
                if (rGet.IsSuccessStatusCode)
                {
                    var js = await rGet.Content.ReadAsStringAsync();
                    actual = JsonSerializer.Deserialize<Usuario>(js, JsonOps);
                }
            }
            catch { }

            // Si por algún motivo no pudimos traerlo, seguimos como antes
            bool esProtegido = actual != null && EsEmailProtegido(actual.email);

            // Validaciones estándar
            if (!(model.id_rol.HasValue && model.id_rol.Value > 0))
                ModelState.AddModelError(nameof(Usuario.id_rol), "Seleccione un rol.");
            if (model.id_estadoUsuario <= 0)
                ModelState.AddModelError(nameof(Usuario.id_estadoUsuario), "Seleccione un estado.");

            // 🔒 NUEVO: si es cuenta protegida, NO permitir cambiar rol ni estado
            if (esProtegido && actual != null)
            {
                if (model.id_rol != actual.id_rol)
                    ModelState.AddModelError(nameof(Usuario.id_rol),
                        "No se puede cambiar el rol de esta cuenta protegida.");

                if (model.id_estadoUsuario != actual.id_estadoUsuario)
                    ModelState.AddModelError(nameof(Usuario.id_estadoUsuario),
                        "No se puede cambiar el estado de esta cuenta protegida.");
                if (!string.Equals(model.email?.Trim(), actual.email?.Trim(), StringComparison.OrdinalIgnoreCase))
                    ModelState.AddModelError(nameof(Usuario.email),
                        "No se puede modificar el email de esta cuenta protegida.");
            }

            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_estadoUsuario, model.id_rol);
                return View(model);
            }

            if (model.fechaAlta == default) model.fechaAlta = DateTime.Now;

            // === LÓGICA DE CLAVE (igual que ya tenías) ===

            string? claveAEnviar = null;
            if (!string.IsNullOrWhiteSpace(model.clave))
            {
                claveAEnviar = model.clave.Trim();
            }
            else
            {
                var deEndpointClave = await ObtenerClaveActualApiAsync(model.id_usuario);
                bool enmascarada = !string.IsNullOrEmpty(deEndpointClave) &&
                                   deEndpointClave.All(ch => ch == '*' || ch == '•');

                if (!string.IsNullOrWhiteSpace(deEndpointClave) && !enmascarada)
                    claveAEnviar = deEndpointClave;
                else if (!string.IsNullOrWhiteSpace(actual?.clave) && !(actual!.clave!.All(ch => ch == '*' || ch == '•')))
                    claveAEnviar = actual!.clave;
            }

            if (string.IsNullOrWhiteSpace(claveAEnviar))
            {
                TempData["Error"] = "La API exige la clave para actualizar el usuario, y no pudimos recuperarla automáticamente.";
                await CargarSelects(model.id_estadoUsuario, model.id_rol);
                return View(model);
            }

            var payload = new
            {
                id_usuario = model.id_usuario,
                clave = claveAEnviar,
                email = model.email ?? actual?.email ?? "",
                nombre = model.nombre ?? actual?.nombre ?? "",
                apellido = model.apellido ?? actual?.apellido ?? "",
                direccion = model.direccion ?? actual?.direccion,
                altura = model.altura ?? actual?.altura,
                departamento = model.departamento ?? actual?.departamento,
                telefono = model.telefono == 0 && actual != null ? actual.telefono : model.telefono,
                fechaAlta = (model.fechaAlta == default && actual != null) ? actual.fechaAlta : model.fechaAlta,
                id_estadoUsuario = (model.id_estadoUsuario == 0 && actual != null) ? actual.id_estadoUsuario : model.id_estadoUsuario,
                id_rol = (model.id_rol.HasValue && model.id_rol.Value > 0) ? model.id_rol.Value : (actual?.id_rol ?? 0)
            };

            // Si es el único admin, seguimos respetando esa regla
            if (await EsUnicoAdministradorAsync(model.id_usuario))
            {
                var adminId = await GetAdminRoleIdAsync();
                if (adminId.HasValue && model.id_rol != adminId.Value)
                {
                    TempData["Error"] = "Acción no permitida. Este usuario es el único Administrador del sistema y no puede modificarse su rol.";
                    await CargarSelects(model.id_estadoUsuario, model.id_rol);
                    return View(model);
                }
            }

            var jsonSend = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = null });
            var resp = await client.PutAsync(
                $"/api/usuario/{model.id_usuario}",
                new StringContent(jsonSend, Encoding.UTF8, "application/json")
            );

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();

                if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    TempData["Error"] = string.IsNullOrWhiteSpace(body)
                        ? "No se puede cambiar el rol: es el único Administrador del sistema."
                        : body;
                }
                else
                {
                    TempData["Error"] = $"Error al modificar usuario. API -> {(int)resp.StatusCode} {resp.ReasonPhrase}";
                    TempData["ApiError"] = body;
                }

                await CargarSelects(model.id_estadoUsuario, model.id_rol);
                return View(model);
            }

            TempData["Ok"] = "Usuario modificado correctamente.";
            return RedirectToAction(nameof(Index));
        }


        // ===================== ELIMINAR =====================

        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/usuario/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El usuario no existe o ya fue eliminado.";
                return RedirectToAction(nameof(Index));
            }
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"GET /api/usuario/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Usuario>(json, JsonOps);

            await CargarDiccionariosBasicos();

            if (model != null)
            {
                var esUnicoAdmin = await EsUnicoAdministradorAsync(model.id_usuario);
                if (esUnicoAdmin)
                {
                    ViewBag.UnicoAdminEliminar = true;
                }

                if (EsEmailProtegido(model.email))
                {
                    ViewBag.Bloqueado = true;
                    ViewBag.Motivo = "Corresponde a una cuenta administrativa del sistema";
                }
                else if (await EsAdministradorAsync(model.id_usuario))
                {
                    ViewBag.Bloqueado = true;
                    ViewBag.Motivo = "Posee el rol Administrador";
                }
            }

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(model);
        }


        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            if (await EsEmailProtegidoAsync(id))
            {
                TempData["Error"] = "Este usuario no puede ser eliminado.";
                return RedirectToAction(nameof(Eliminar), new { id });
            }

            if (await EsAdministradorAsync(id))
            {
                TempData["Error"] = "El usuario Administrador no puede ser eliminado.";
                return RedirectToAction(nameof(Eliminar), new { id });
            }

            var client = _http.CreateClient("Api");
            var respDel = await client.DeleteAsync($"/api/usuario/{id}");

            if (!respDel.IsSuccessStatusCode)
            {
                var body = await respDel.Content.ReadAsStringAsync();

                TempData["Error"] = "No puedes eliminar este usuario porque está siendo usado.";
                TempData["ApiDetail"] = body;

                return RedirectToAction(nameof(Eliminar), new { id });
            }

            TempData["Ok"] = "Usuario eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }


        // ===================== HELPERS (SELECTS / DICCIONARIOS) =====================

        private async Task CargarSelects(int? estadoSel = null, int? rolSel = null)
        {
            var client = _http.CreateClient("Api");

            var rEst = await client.GetAsync("/api/Estado_Usuario");
            var estados = new List<SelectListItem> { new SelectListItem { Text = "Seleccione...", Value = "" } };
            if (rEst.IsSuccessStatusCode)
            {
                var js = await rEst.Content.ReadAsStringAsync();
                var list = JsonSerializer.Deserialize<IEnumerable<Estado_Usuario>>(js, JsonOps) ?? Enumerable.Empty<Estado_Usuario>();
                estados.AddRange(list.Select(e => new SelectListItem
                {
                    Text = e.descripcion,
                    Value = e.id_estadoUsuario.ToString(),
                    Selected = estadoSel.HasValue && e.id_estadoUsuario == estadoSel.Value
                }));
            }
            ViewBag.Estados = estados;

            var items = new List<SelectListItem> {
                new SelectListItem {
                    Text = "Seleccione...",
                    Value = "",
                    Selected = !rolSel.HasValue || rolSel.Value <= 0
                }
            };

            var rRol = await client.GetAsync("/api/rol");
            if (rRol.IsSuccessStatusCode)
            {
                var jr = await rRol.Content.ReadAsStringAsync();
                var roles = JsonSerializer.Deserialize<IEnumerable<Rol>>(jr, JsonOps) ?? Enumerable.Empty<Rol>();
                foreach (var r in roles)
                {
                    if (r.id_rol <= 0) continue;

                    items.Add(new SelectListItem
                    {
                        Text = r.descripcion ?? $"#{r.id_rol}",
                        Value = r.id_rol.ToString(),
                        Selected = rolSel.HasValue && rolSel.Value == r.id_rol
                    });
                }
            }

            ViewBag.Roles = items;
        }

        private async Task CargarDiccionariosBasicos()
        {
            var client = _http.CreateClient("Api");
            var tEst = client.GetAsync("/api/Estado_Usuario");
            var tRol = client.GetAsync("/api/rol");
            await Task.WhenAll(tEst, tRol);

            ViewBag.Estados = await ToDict<Estado_Usuario>(tEst.Result, x => x.id_estadoUsuario, x => x.descripcion);
            ViewBag.Roles = await ToDict<Rol>(tRol.Result, x => x.id_rol, x => x.descripcion);
        }

        private static async Task<List<SelectListItem>> ToSelectList<T>(
          HttpResponseMessage resp,
          Func<T, int> keySel,
          Func<T, string> textSel,
          int? selected = null)
        {
            var items = new List<SelectListItem>();
            if (resp is null || !resp.IsSuccessStatusCode) return items;

            var json = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<IEnumerable<T>>(json, JsonOps) ?? Enumerable.Empty<T>();

            items.AddRange(list.Select(x => new SelectListItem
            {
                Text = textSel(x),
                Value = keySel(x).ToString(),
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
            var list = JsonSerializer.Deserialize<IEnumerable<T>>(json, JsonOps) ?? Enumerable.Empty<T>();

            return list.GroupBy(keySel).ToDictionary(g => g.Key, g => valSel(g.First()));
        }

        // ===================== CLAVE (GET desde vista) =====================

        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> ObtenerClave(int id)
        {
            var urlApi = $"/api/usuario/{id}/clave";
            try
            {
                var client = _http.CreateClient("Api");
                var r = await client.GetAsync(urlApi);

                var status = (int)r.StatusCode;
                var raw = await r.Content.ReadAsStringAsync();

                if (!r.IsSuccessStatusCode)
                    return Json(new { ok = false, status, msg = "API no OK", raw });

                string? clave = null;
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("clave", out var c1) && c1.ValueKind == JsonValueKind.String)
                            clave = c1.GetString();
                        else if (doc.RootElement.TryGetProperty("password", out var c2) && c2.ValueKind == JsonValueKind.String)
                            clave = c2.GetString();
                    }
                }
                catch
                {
                    clave = raw?.Trim().Trim('"');
                }

                if (string.IsNullOrWhiteSpace(clave))
                    return Json(new { ok = false, status, msg = "Vacío", raw });

                var masked = clave.All(ch => ch == '*' || ch == '•');
                return Json(new { ok = true, clave, masked, status });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, msg = "Excepción en backoffice", error = ex.Message, urlApi });
            }
        }

        private async Task<string?> ObtenerClaveActualApiAsync(int idUsuario)
        {
            var client = _http.CreateClient("Api");
            var r = await client.GetAsync($"/api/usuario/{idUsuario}/clave");
            if (!r.IsSuccessStatusCode) return null;

            var raw = await r.Content.ReadAsStringAsync();

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("clave", out var p1) && p1.ValueKind == JsonValueKind.String)
                        return p1.GetString();
                    if (root.TryGetProperty("password", out var p2) && p2.ValueKind == JsonValueKind.String)
                        return p2.GetString();
                }
            }
            catch { }

            return raw?.Trim().Trim('"');
        }
    }
}
