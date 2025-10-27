using System.Text;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    // [Authorize(Roles = "Administrador")]
    public class UsuarioController : Controller
    {
        private readonly IHttpClientFactory _http;
        private const string ADMIN_ROLE_NAME = "administrador"; // comparar en lower

        public UsuarioController(IHttpClientFactory http) => _http = http;

        // ===================== HELPERS (ROLES) =====================

        private async Task<bool> AsignarRolAsync(HttpClient client, int idUsuario, int idRol)
        {
            var put = await client.PutAsync($"/api/Usuario/{idUsuario}/rol/{idRol}", null);
            return put.IsSuccessStatusCode;
        }

        private async Task<int?> GetRolActualAsync(int idUsuario)
        {
            var client = _http.CreateClient("Api");

            // a) /api/Usuario/{id}/roles (si existe)
            var r1 = await client.GetAsync($"/api/Usuario/{idUsuario}/roles");
            if (r1.IsSuccessStatusCode)
            {
                try
                {
                    var json1 = await r1.Content.ReadAsStringAsync();
                    var roles1 = JsonSerializer.Deserialize<IEnumerable<Rol>>(json1,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Rol>();
                    var rid1 = roles1.FirstOrDefault()?.id_rol;
                    if (rid1.HasValue && rid1.Value > 0) return rid1.Value;
                }
                catch { /* seguimos probando */ }
            }

            // b) /api/Usuario_Rol?usuarioId={id} (si existe)
            var r2 = await client.GetAsync($"/api/Usuario_Rol?usuarioId={idUsuario}");
            if (r2.IsSuccessStatusCode)
            {
                try
                {
                    var json2 = await r2.Content.ReadAsStringAsync();
                    var uroles = JsonSerializer.Deserialize<IEnumerable<Usuario_Rol>>(json2,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Usuario_Rol>();
                    var rid2 = uroles.FirstOrDefault()?.id_rol;
                    if (rid2 > 0) return rid2;
                }
                catch { }
            }

            // c) /api/Usuario_Rol/{id} (si existe)
            var r3 = await client.GetAsync($"/api/Usuario_Rol/{idUsuario}");
            if (r3.IsSuccessStatusCode)
            {
                try
                {
                    var json3 = await r3.Content.ReadAsStringAsync();

                    // puede venir un objeto o una lista
                    var uno = JsonSerializer.Deserialize<Usuario_Rol>(json3,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (uno != null && uno.id_rol > 0) return uno.id_rol;

                    var lista = JsonSerializer.Deserialize<IEnumerable<Usuario_Rol>>(json3,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Usuario_Rol>();
                    var rid3 = lista.FirstOrDefault()?.id_rol;
                    if (rid3 > 0) return rid3;
                }
                catch { }
            }

            return null;
        }

        private async Task<bool> EsAdministradorAsync(int idUsuario)
        {
            var rid = await GetRolActualAsync(idUsuario);
            if (rid is null || rid <= 0) return false;

            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/Rol");
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync();
            var roles = JsonSerializer.Deserialize<IEnumerable<Rol>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Rol>();

            var desc = roles.FirstOrDefault(r => r.id_rol == rid)?.descripcion ?? string.Empty;
            return desc.Trim().ToLower() == ADMIN_ROLE_NAME;
        }

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");

            var respUsuarios = await client.GetAsync("/api/usuario");
            if (!respUsuarios.IsSuccessStatusCode)
            {
                var body = await respUsuarios.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/usuario -> {(int)respUsuarios.StatusCode} {respUsuarios.ReasonPhrase}. Respuesta: {body}";
                return View(Enumerable.Empty<Usuario>());
            }

            var usersJson = await respUsuarios.Content.ReadAsStringAsync();
            var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(usersJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Usuario>();

            var tEstados = client.GetAsync("/api/Estado_Usuario");
            await Task.WhenAll(tEstados);

            ViewBag.Estados = await ToDict<Estado_Usuario>(tEstados.Result, x => x.id_estadoUsuario, x => x.descripcion);

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            // --- Mapeo usuario -> nombre de rol para la grilla ---
            try
            {
                var client2 = _http.CreateClient("Api");

                var respRoles = await client2.GetAsync("/api/Rol");
                var dictRoles = new Dictionary<int, string>();
                if (respRoles.IsSuccessStatusCode)
                {
                    var jr = await respRoles.Content.ReadAsStringAsync();
                    var roles = JsonSerializer.Deserialize<IEnumerable<Rol>>(jr,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Rol>();
                    dictRoles = roles.ToDictionary(r => r.id_rol, r => r.descripcion ?? $"#{r.id_rol}");
                }

                var rolPorUsuario = new Dictionary<int, string>();
                foreach (var u in usuarios)
                {
                    var rid = await GetRolActualAsync(u.id_usuario);
                    rolPorUsuario[u.id_usuario] = (rid.HasValue && rid.Value > 0 && dictRoles.ContainsKey(rid.Value))
                        ? dictRoles[rid.Value]
                        : "-";
                }
                ViewBag.RolPorUsuario = rolPorUsuario;
            }
            catch
            {
                ViewBag.RolPorUsuario = new Dictionary<int, string>();
            }

            return View(usuarios);
        }

        // ===================== CREAR =====================
        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            await CargarSelects();
            return View(new Usuario());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Usuario model)
        {
            if (model.fechaAlta == default) model.fechaAlta = DateTime.Now;

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { PropertyNamingPolicy = null });
            var resp = await client.PostAsync("/api/Usuario", new StringContent(json, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "Error al crear usuario.";
                await CargarSelects(model.id_estadoUsuario, model.id_rol);
                return View(model);
            }

            var body = await resp.Content.ReadAsStringAsync();
            var creado = JsonSerializer.Deserialize<Usuario>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model.id_rol.HasValue && model.id_rol.Value > 0 && (creado?.id_usuario ?? 0) > 0)
            {
                var ok = await AsignarRolAsync(client, creado!.id_usuario, model.id_rol.Value);
                if (!ok) TempData["Error"] = "Usuario creado, pero no se pudo asignar el rol.";
            }

            if (TempData["Error"] == null) TempData["Ok"] = "Usuario creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===================== DETALLE (MODAL) =====================
        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/usuario/{id}");
            if (!resp.IsSuccessStatusCode) return NotFound();

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Usuario>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (model is null) return NotFound();

            // set rol actual para mostrar en modal
            model.id_rol = await GetRolActualAsync(id);

            await CargarDiccionariosBasicos();
            return PartialView("DetalleUsuario", model);
        }

        // ===================== MODIFICAR =====================
        [HttpGet]
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
            var model = JsonSerializer.Deserialize<Usuario>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (model == null)
            {
                TempData["Error"] = "No se pudo deserializar el usuario.";
                return RedirectToAction(nameof(Index));
            }

            // rol actual (si API devuelve varios, tomamos el primero)
            model.id_rol = await GetRolActualAsync(model.id_usuario);

            await CargarSelects(model.id_estadoUsuario, model.id_rol);
            ViewBag.EsAdmin = await EsAdministradorAsync(model.id_usuario);

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar(Usuario model)
        {
            if (model.fechaAlta == default) model.fechaAlta = DateTime.Now;

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { PropertyNamingPolicy = null });
            var resp = await client.PutAsync($"/api/Usuario/{model.id_usuario}", new StringContent(json, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "Error al modificar usuario.";
                await CargarSelects(model.id_estadoUsuario, model.id_rol);
                return View(model);
            }

            if (model.id_rol.HasValue && model.id_rol.Value > 0)
            {
                var ok = await AsignarRolAsync(client, model.id_usuario, model.id_rol.Value);
                if (!ok) TempData["Error"] = "Usuario modificado, pero no se pudo actualizar el rol.";
            }

            if (TempData["Error"] == null) TempData["Ok"] = "Usuario modificado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===================== ELIMINAR =====================
        [HttpGet]
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
            var model = JsonSerializer.Deserialize<Usuario>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            await CargarDiccionariosBasicos();

            if (model != null && await EsAdministradorAsync(model.id_usuario))
            {
                ViewBag.Bloqueado = true;
                ViewBag.Motivo = "posee el rol Administrador";
            }

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
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
                if (respDel.StatusCode == System.Net.HttpStatusCode.Conflict ||
                    respDel.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                    (int)respDel.StatusCode == 422)
                {
                    TempData["Error"] = "No se puede eliminar el usuario porque está en uso.";
                    if (!string.IsNullOrWhiteSpace(body)) TempData["ApiDetail"] = body;
                    return RedirectToAction(nameof(Eliminar), new { id });
                }

                TempData["Error"] = $"DELETE /api/usuario/{id} -> {(int)respDel.StatusCode} {respDel.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Eliminar), new { id });
            }

            TempData["Ok"] = "Usuario eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS (SELECTS / DICCIONARIOS) =====================

        private async Task CargarSelects(int? estadoSel = null, int? rolSel = null)
        {
            var client = _http.CreateClient("Api");
            var tEst = client.GetAsync("/api/Estado_Usuario");
            var tRol = client.GetAsync("/api/Rol");
            await Task.WhenAll(tEst, tRol);

            ViewBag.Estados = await ToSelectList<Estado_Usuario>(tEst.Result, x => x.id_estadoUsuario, x => x.descripcion, estadoSel);
            ViewBag.Roles = await ToSelectList<Rol>(tRol.Result, x => x.id_rol, x => x.descripcion, rolSel);
        }

        private async Task CargarDiccionariosBasicos()
        {
            var client = _http.CreateClient("Api");
            var tEst = client.GetAsync("/api/Estado_Usuario");
            var tRol = client.GetAsync("/api/Rol");
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
            var items = new List<SelectListItem> { new SelectListItem { Text = "Seleccione...", Value = "" } };
            if (resp is null || !resp.IsSuccessStatusCode) return items;

            var json = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<IEnumerable<T>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<T>();

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
            var list = JsonSerializer.Deserialize<IEnumerable<T>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<T>();

            return list.GroupBy(keySel).ToDictionary(g => g.Key, g => valSel(g.First()));
        }
    }
}
