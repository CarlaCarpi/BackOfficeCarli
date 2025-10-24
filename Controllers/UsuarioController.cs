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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] Usuario model)
        {
            if (string.IsNullOrWhiteSpace(model.nombre))
                ModelState.AddModelError(nameof(Usuario.nombre), "El nombre es obligatorio.");
            if (string.IsNullOrWhiteSpace(model.apellido))
                ModelState.AddModelError(nameof(Usuario.apellido), "El apellido es obligatorio.");
            if (string.IsNullOrWhiteSpace(model.email))
                ModelState.AddModelError(nameof(Usuario.email), "El email es obligatorio.");
            if (model.id_estadoUsuario <= 0)
                ModelState.AddModelError(nameof(Usuario.id_estadoUsuario), "Seleccione un estado válido.");

            if (model.fechaAlta == default) model.fechaAlta = DateTime.Now;

            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_estadoUsuario, model.id_rol);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/usuario", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/usuario -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                await CargarSelects(model.id_estadoUsuario, model.id_rol);
                return View(model);
            }

            // asignar rol si se envió y existe endpoint
            try
            {
                var body = await resp.Content.ReadAsStringAsync();
                var creado = JsonSerializer.Deserialize<Usuario>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (model.id_rol.HasValue && creado != null)
                {
                    var setContent = new StringContent(JsonSerializer.Serialize(new { id_usuario = creado.id_usuario, id_rol = model.id_rol.Value }), Encoding.UTF8, "application/json");
                    await client.PostAsync("/api/Usuario_Rol", setContent); // ajustá si tu API difiere
                }
            }
            catch { /* evitar romper el flujo si falla el set de rol */ }

            TempData["Ok"] = "Usuario creado correctamente.";
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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] Usuario model)
        {
            if (model.id_usuario <= 0)
                ModelState.AddModelError("", "Identificador inválido.");

            // 🔒 bloquea admin
            if (await EsAdministradorAsync(model.id_usuario))
            {
                TempData["Error"] = "El usuario Administrador no puede ser modificado.";
                return RedirectToAction(nameof(Index)); // <- volvemos al Index con mensaje
            }

            if (model.id_estadoUsuario <= 0)
                ModelState.AddModelError(nameof(Usuario.id_estadoUsuario), "Seleccione un estado válido.");

            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_estadoUsuario, model.id_rol);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/usuario/{model.id_usuario}", content);
            if (!resp.IsSuccessStatusCode)
            {
                // podés elegir:  (a) quedarse en la vista con detalle del error  ó  (b) volver a Index con banner
                // Opción (b) — lo que pediste: banner en Index
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"No se pudo actualizar el usuario (HTTP {(int)resp.StatusCode}).";
                // si querés sumar el detalle, descomenta:
                // TempData["ApiDetail"] = body;
                return RedirectToAction(nameof(Index));
            }

            // guardar rol si vino
            try
            {
                if (model.id_rol.HasValue)
                {
                    var setContent = new StringContent(
                        JsonSerializer.Serialize(new { id_usuario = model.id_usuario, id_rol = model.id_rol.Value }),
                        Encoding.UTF8, "application/json");
                    await client.PostAsync("/api/Usuario_Rol", setContent);
                }
            }
            catch { /* no romper */ }

            TempData["Ok"] = "Usuario modificado correctamente.";
            return RedirectToAction(nameof(Index)); // <- éxito al Index
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
