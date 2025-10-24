using Microsoft.AspNetCore.Mvc;
using SantaRamona.Backoffice.Models;
using System.Text.Json;
using System.Text;

namespace SantaRamona.Backoffice.Controllers
{
    public class RolController : Controller
    {
        private const string ADMIN_ROLE_NAME = "administrador"; // usar lower + OrdIgnoreCase
        private readonly IHttpClientFactory _http;
        public RolController(IHttpClientFactory http) => _http = http;

        // ---------------- HELPERS ----------------
        private static bool EsAdmin(string? descripcion)
            => string.Equals((descripcion ?? string.Empty).Trim(), ADMIN_ROLE_NAME, StringComparison.OrdinalIgnoreCase);

        private async Task<bool> RolEnUsoAsync(int idRol)
        {
            var client = _http.CreateClient("Api");

            // 1) intento directo: /api/Usuario_Rol?rol=ID  (si existe)
            var respTry = await client.GetAsync($"/api/Usuario_Rol?rol={idRol}");
            if (respTry.IsSuccessStatusCode)
            {
                var body = await respTry.Content.ReadAsStringAsync();
                // asumo que devuelve una lista de vínculos usuario-rol
                var userRoles = JsonSerializer.Deserialize<IEnumerable<Usuario_Rol>>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Usuario_Rol>();
                return userRoles.Any();
            }

            // 2) fallback genérico: traigo usuarios y consulto roles por usuario
            var respUsers = await client.GetAsync("/api/usuario");
            if (!respUsers.IsSuccessStatusCode) return false;

            var jsonUsers = await respUsers.Content.ReadAsStringAsync();
            var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(jsonUsers,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Usuario>();

            foreach (var u in usuarios)
            {
                var respRoles = await client.GetAsync($"/api/Usuario/{u.id_usuario}/roles");
                if (!respRoles.IsSuccessStatusCode) continue;

                var jsonRoles = await respRoles.Content.ReadAsStringAsync();
                var roles = JsonSerializer.Deserialize<IEnumerable<Rol>>(jsonRoles,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Rol>();

                if (roles.Any(r => r.id_rol == idRol)) return true;
            }

            return false;
        }

        // ---------------- INDEX ----------------
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/Rol");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/Rol -> {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}";
                return View(Enumerable.Empty<Rol>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<IEnumerable<Rol>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Rol>();

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(lista);
        }

        // ---------------- CREAR ----------------
        [HttpGet]
        public IActionResult Crear() => View(new Rol());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] Rol model)
        {
            if (string.IsNullOrWhiteSpace(model.descripcion))
            {
                ModelState.AddModelError(nameof(Rol.descripcion), "La descripción es obligatoria.");
                return View(model);
            }

            // Evitar duplicar Administrador
            if (EsAdmin(model.descripcion))
            {
                // si ya existe Admin, la API debería rechazar; por las dudas prevenimos acá
            }

            var client = _http.CreateClient("Api");
            var content = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync("/api/Rol", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/Rol -> {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}";
                return View(model);
            }

            TempData["Ok"] = "Rol creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------- MODIFICAR ----------------
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

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Rol>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            ViewBag.EsAdmin = EsAdmin(model?.descripcion);
            ViewBag.EnUso = await RolEnUsoAsync(id);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] Rol model)
        {
            if (EsAdmin(model.descripcion))
            {
                TempData["Error"] = "El rol Administrador no puede ser modificado.";
                return RedirectToAction(nameof(Index));
            }

            if (await RolEnUsoAsync(model.id_rol))
            {
                TempData["Error"] = "No se puede modificar este rol porque está en uso.";
                return RedirectToAction(nameof(Index));
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
                TempData["Error"] = "No se pudo actualizar el rol.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Ok"] = "Rol actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------- ELIMINAR ----------------
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Rol/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El rol no existe o ya fue eliminado.";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Rol>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (EsAdmin(model?.descripcion))
            {
                ViewBag.Bloqueado = true;
                ViewBag.Motivo = "es el rol Administrador";
            }
            else if (await RolEnUsoAsync(id))
            {
                ViewBag.Bloqueado = true;
                ViewBag.Motivo = "está asignado a uno o más usuarios";
            }

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            // Revalidar bloqueos
            var client = _http.CreateClient("Api");
            var respGet = await client.GetAsync($"/api/Rol/{id}");
            var json = await respGet.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Rol>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (EsAdmin(model?.descripcion))
            {
                TempData["Error"] = "El rol Administrador no puede ser eliminado.";
                return RedirectToAction(nameof(Eliminar), new { id });
            }

            if (await RolEnUsoAsync(id))
            {
                TempData["Error"] = "No se puede eliminar este rol porque está en uso.";
                return RedirectToAction(nameof(Eliminar), new { id });
            }

            var respDel = await client.DeleteAsync($"/api/Rol/{id}");
            if (!respDel.IsSuccessStatusCode)
            {
                var body = await respDel.Content.ReadAsStringAsync();
                if (respDel.StatusCode == System.Net.HttpStatusCode.Conflict ||
                    respDel.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                    (int)respDel.StatusCode == 422)
                {
                    TempData["Error"] = "No se puede eliminar este rol porque está en uso.";
                    if (!string.IsNullOrWhiteSpace(body)) TempData["ApiDetail"] = body;
                    return RedirectToAction(nameof(Eliminar), new { id });
                }

                TempData["Error"] = $"DELETE /api/Rol/{id} -> {(int)respDel.StatusCode} {respDel.ReasonPhrase}. {body}";
                return RedirectToAction(nameof(Eliminar), new { id });
            }

            TempData["Ok"] = "Rol eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
