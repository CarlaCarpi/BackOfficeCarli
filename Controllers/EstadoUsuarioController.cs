using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SantaRamona.Backoffice.Models;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    [Authorize(Policy = "Activo")]
    public class EstadoUsuarioController : Controller
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger<EstadoUsuarioController> _logger;

        public EstadoUsuarioController(IHttpClientFactory http, ILogger<EstadoUsuarioController> logger)
        {
            _http = http;
            _logger = logger;
        }

        // ===== Bloqueo global de acciones (solo Index habilitado) =====
        private const bool BLOQUEADO = true;
        private IActionResult DenegarAcceso()
        {
            TempData["Error"] = "La administración de estados está temporalmente deshabilitada.";
            return RedirectToAction(nameof(Index));
        }

        // ===== Helpers (mismo patrón que Rol) =====
        private const string ESTADO_API = "/api/Estado_Usuario";

        private async Task<bool> EstadoEnUsoAsync(int idEstado)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/usuario");
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync();
            var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? Enumerable.Empty<Usuario>();

            return usuarios.Any(u => u.id_estadoUsuario == idEstado);
        }

        // ===== INDEX (habilitado) =====
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync(ESTADO_API);
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudieron obtener los estados.";
                return View(Enumerable.Empty<Estado_Usuario>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<IEnumerable<Estado_Usuario>>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? Enumerable.Empty<Estado_Usuario>();

            return View(lista);
        }

        // ===== CREAR (bloqueado) =====
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public IActionResult Crear()
        {
            if (BLOQUEADO) return DenegarAcceso();
            return DenegarAcceso(); // fallback por si se desactiva arriba
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public IActionResult Crear(Estado_Usuario model)
        {
            if (BLOQUEADO) return DenegarAcceso();
            return DenegarAcceso();
        }

        // ===== MODIFICAR (bloqueado) =====
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public IActionResult Modificar(int id)
        {
            if (BLOQUEADO) return DenegarAcceso();
            return DenegarAcceso();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public IActionResult Modificar(Estado_Usuario model)
        {
            if (BLOQUEADO) return DenegarAcceso();
            return DenegarAcceso();
        }

        // ===== ELIMINAR (bloqueado) =====
        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public IActionResult Eliminar(int id)
        {
            if (BLOQUEADO) return DenegarAcceso();
            return DenegarAcceso();
        }

        [HttpPost, ActionName("Eliminar"), ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public IActionResult EliminarConfirmado(int id)
        {
            if (BLOQUEADO) return DenegarAcceso();
            return DenegarAcceso();
        }
    }
}
