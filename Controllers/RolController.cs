using Microsoft.AspNetCore.Mvc;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    public class RolController : Controller
    {
        private readonly IHttpClientFactory _http;
        public RolController(IHttpClientFactory http) => _http = http;



        private const string ADMIN_NAME = "administrador";

        private static bool EsAdminDesc(string? d)
            => (d ?? "").Trim().ToLower() == ADMIN_NAME;

        // ¿Hay usuarios usando este rol?
        private async Task<bool> RolEnUsoAsync(int idRol)
        {
            var client = _http.CreateClient("Api");
            // 1) intento simple: si tu API de usuarios trae id_rol directo
            var resp = await client.GetAsync("/api/usuario");
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync();
            var usuarios = System.Text.Json.JsonSerializer.Deserialize<IEnumerable<SantaRamona.Backoffice.Models.Usuario>>(
                json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? Enumerable.Empty<SantaRamona.Backoffice.Models.Usuario>();

            // a) si tu API completa id_rol
            if (usuarios.Any(u => u.id_rol == idRol)) return true;

            // b) fallback: si trae UsuarioRoles
            if (usuarios.Any(u => (u.UsuarioRoles ?? Array.Empty<Usuario_Rol>()).Any(ur => ur.id_rol == idRol))) return true;

            return false;
        }


        // ✅ INDEX
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/Rol");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/Rol -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(Enumerable.Empty<Rol>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<IEnumerable<Rol>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Rol>();

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(data);
        }

        // ✅ CREAR
        [HttpGet]
        public IActionResult Crear() => View(new Rol());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] string descripcion)
        {
            if (string.IsNullOrWhiteSpace(descripcion))
            {
                ModelState.AddModelError(nameof(Rol.descripcion), "La descripción es obligatoria.");
                return View(new Rol { descripcion = descripcion ?? string.Empty });
            }

            var model = new Rol { descripcion = descripcion.Trim() };
            var client = _http.CreateClient("Api");
            var content = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/Rol", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/Rol -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            TempData["Ok"] = "Rol creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ✅ MODIFICAR
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Rol/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El rol no existe.";
                return RedirectToAction(nameof(Index));
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"GET /api/Rol/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Rol>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model == null)
            {
                TempData["Error"] = "No se pudo obtener el rol.";
                return RedirectToAction(nameof(Index));
            }
            // después de deserializar 'model'
            if (model == null) { TempData["Error"] = "No se pudo obtener el rol."; return RedirectToAction(nameof(Index)); }

            if (EsAdminDesc(model.descripcion))
            {
                TempData["Error"] = "El administrador no puede ser eliminado ni modificado.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] Rol model)
        {
            if (model == null || model.id_rol <= 0)
            {
                ModelState.AddModelError("", "Identificador inválido.");
                return View(model ?? new Rol());
            }

            if (string.IsNullOrWhiteSpace(model.descripcion))
            {
                ModelState.AddModelError(nameof(Rol.descripcion), "La descripción es obligatoria.");
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var content = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/Rol/{model.id_rol}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/Rol/{model.id_rol} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }
            if (model == null || model.id_rol <= 0)
            {
                ModelState.AddModelError("", "Identificador inválido.");
                return View(model ?? new Rol());
            }
            if (string.IsNullOrWhiteSpace(model.descripcion))
            {
                ModelState.AddModelError(nameof(Rol.descripcion), "La descripción es obligatoria.");
                return View(model);
            }

            // bloqueos solicitados
            if (EsAdminDesc(model.descripcion))
            {
                TempData["Error"] = "El administrador no puede ser eliminado ni modificado.";
                return RedirectToAction(nameof(Index));
            }
            if (await RolEnUsoAsync(model.id_rol))
            {
                TempData["Error"] = "El rol no puede ser modificado ni eliminado porque está en uso.";
                return RedirectToAction(nameof(Index));
            }
            TempData["Ok"] = "Rol actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ✅ ELIMINAR
        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Rol/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudo obtener el rol.";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = System.Text.Json.JsonSerializer.Deserialize<Rol>(
                json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model == null)
            {
                TempData["Error"] = "Rol inexistente.";
                return RedirectToAction(nameof(Index));
            }

            // 🔒 Bloqueos antes de mostrar la vista
            if (EsAdminDesc(model.descripcion))
            {
                TempData["Error"] = "El administrador no puede ser eliminado ni modificado.";
                return RedirectToAction(nameof(Index));
            }

            if (await RolEnUsoAsync(model.id_rol))
            {
                TempData["Error"] = "El estado/ rol no puede ser modificado ni eliminado porque está en uso.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }


        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]

        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");

            // Revalidar por si llamaron directo al POST
            var get = await client.GetAsync($"/api/Rol/{id}");
            if (!get.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudo obtener el rol.";
                return RedirectToAction(nameof(Index));
            }

            var jsonGet = await get.Content.ReadAsStringAsync();
            var rol = System.Text.Json.JsonSerializer.Deserialize<Rol>(
                jsonGet, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (rol == null)
            {
                TempData["Error"] = "Rol inexistente.";
                return RedirectToAction(nameof(Index));
            }

            if (EsAdminDesc(rol.descripcion))
            {
                TempData["Error"] = "El administrador no puede ser eliminado ni modificado.";
                return RedirectToAction(nameof(Index));
            }

            if (await RolEnUsoAsync(id))
            {
                TempData["Error"] = "El estado/ rol no puede ser modificado ni eliminado porque está en uso.";
                return RedirectToAction(nameof(Index));
            }

            // Delete real
            var resp = await client.DeleteAsync($"/api/Rol/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"DELETE /api/Rol/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            TempData["Ok"] = "Rol eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }

    }
}
