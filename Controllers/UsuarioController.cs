using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    //[Authorize(Roles = "Administrador")] // solo Admin accede a este módulo
    public class UsuarioController : Controller
    {
        private readonly IHttpClientFactory _http;
        private const string ADMIN_ROLE_NAME = "administrador"; // comparar en lower

        public UsuarioController(IHttpClientFactory http) => _http = http;
        private async Task<bool> AsignarRolAsync(HttpClient client, int idUsuario, int idRol)
        {
            // 1) Intento endpoint REST común
            var put = await client.PutAsync($"/api/Usuario/{idUsuario}/rol/{idRol}", null);
            if (put.IsSuccessStatusCode) return true;

            // 2) Fallback: POST a tabla puente
            var payload = new { id_usuario = idUsuario, id_rol = idRol };
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var post = await client.PostAsync("/api/Usuario_Rol", content);
            return post.IsSuccessStatusCode;
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

            var json = System.Text.Json.JsonSerializer.Serialize(model, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null });
            var resp = await client.PostAsync("/api/Usuario", new StringContent(json, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode) { TempData["Error"] = "Error al crear usuario."; return View(model); }

            var body = await resp.Content.ReadAsStringAsync();
            var creado = System.Text.Json.JsonSerializer.Deserialize<Usuario>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (model.id_rol.HasValue && model.id_rol.Value > 0 && creado?.id_usuario > 0)
            {
                var ok = await AsignarRolAsync(client, creado.id_usuario, model.id_rol.Value);
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

            // rol actual (si tu API devuelve varios, tomamos el primero)
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

            var json = System.Text.Json.JsonSerializer.Serialize(model, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null });
            var resp = await client.PutAsync($"/api/Usuario/{model.id_usuario}", new StringContent(json, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode) { TempData["Error"] = "Error al modificar usuario."; return View(model); }

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

        // ===================== HELPERS =====================
        private async Task<bool> EsAdministradorAsync(int idUsuario)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Usuario/{idUsuario}/roles"); // ajustá si difiere
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync();
            var roles = JsonSerializer.Deserialize<IEnumerable<Rol>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Rol>();

            return roles.Any(r => (r.descripcion ?? string.Empty).Trim().ToLower() == ADMIN_ROLE_NAME);
        }

        private async Task<int?> GetRolActualAsync(int idUsuario)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Usuario/{idUsuario}/roles"); // ajustá si difiere
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            var roles = JsonSerializer.Deserialize<IEnumerable<Rol>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Rol>();

            return roles.FirstOrDefault()?.id_rol;
        }

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
