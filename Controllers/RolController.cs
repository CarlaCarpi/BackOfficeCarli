using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    [Authorize(Policy = "Activo")]
    public class RolController : Controller
    {
        private readonly IHttpClientFactory _http;
        public RolController(IHttpClientFactory http) => _http = http;

        // 🔒 HABILITADOR GLOBAL DE BLOQUEO DE ACCIONES (solo Index queda habilitado)
        private const bool BLOQUEADO = true;

        // Mensaje único (usado en GET/POST)
        private IActionResult DenegarAcceso()
        {
            TempData["Error"] = "La administración de roles está deshabilitada.";
            return RedirectToAction(nameof(Index));
        }

        private const string ADMIN_NAME = "administrador";
        private static bool EsAdminDesc(string? d) => (d ?? "").Trim().ToLower() == ADMIN_NAME;

        // ¿Hay usuarios usando este rol? (queda aquí por compatibilidad si más adelante lo reactivás)
        private async Task<bool> RolEnUsoAsync(int idRol)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/usuario");
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync();
            var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? Enumerable.Empty<Usuario>();

            // Nuevo esquema 1-N: si la API completa id_rol directo
            if (usuarios.Any(u => u.id_rol == idRol)) return true;

            // Fallback legacy (no debería usarse ya)
            if (usuarios.Any(u => (u.UsuarioRoles ?? Array.Empty<Usuario_Rol>()).Any(ur => ur.id_rol == idRol))) return true;

            return false;
        }

        // ======================= INDEX =======================
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
            var data = JsonSerializer.Deserialize<IEnumerable<Rol>>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? Enumerable.Empty<Rol>();

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(data);
        }

        // ======================= CREAR =======================
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public IActionResult Crear()
        {
            if (BLOQUEADO) return DenegarAcceso();
            return View(new Rol());
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public IActionResult Crear([FromForm] string descripcion)
        {
            if (BLOQUEADO) return DenegarAcceso();
            // --- código original queda inalcanzable mientras BLOQUEADO = true ---
            return DenegarAcceso();
        }

        // ======================= MODIFICAR =======================
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public IActionResult Modificar(int id)
        {
            if (BLOQUEADO) return DenegarAcceso();
            // --- código original queda inalcanzable mientras BLOQUEADO = true ---
            return DenegarAcceso();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public IActionResult Modificar([FromForm] Rol model)
        {
            if (BLOQUEADO) return DenegarAcceso();
            // --- código original queda inalcanzable mientras BLOQUEADO = true ---
            return DenegarAcceso();
        }

        // ======================= ELIMINAR =======================
        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public IActionResult Eliminar(int id)
        {
            if (BLOQUEADO) return DenegarAcceso();
            // --- código original queda inalcanzable mientras BLOQUEADO = true ---
            return DenegarAcceso();
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        [Authorize(Roles = "Administrador")]
        public IActionResult EliminarConfirmado(int id)
        {
            if (BLOQUEADO) return DenegarAcceso();
            // --- código original queda inalcanzable mientras BLOQUEADO = true ---
            return DenegarAcceso();
        }
    }
}
