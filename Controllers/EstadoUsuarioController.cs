using Microsoft.AspNetCore.Mvc;
using SantaRamona.Backoffice.Models;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    public class EstadoUsuarioController : Controller
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger<EstadoUsuarioController> _logger;

        public EstadoUsuarioController(IHttpClientFactory http, ILogger<EstadoUsuarioController> logger)
        {
            _http = http;
            _logger = logger;
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

        // ===== INDEX =====
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

        // ===== CREAR =====
        [HttpGet]
        public IActionResult Crear() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Estado_Usuario model)
        {
            if (!ModelState.IsValid) return View(model);

            var client = _http.CreateClient("Api");
            var content = new StringContent(JsonSerializer.Serialize(model), System.Text.Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(ESTADO_API, content);

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Estado creado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = $"Error al crear el estado: {resp.ReasonPhrase}";
            return View(model);
        }

        // ===== MODIFICAR =====
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{ESTADO_API}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudo obtener el estado.";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Estado_Usuario>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model == null)
            {
                TempData["Error"] = "Estado inexistente.";
                return RedirectToAction(nameof(Index));
            }

            // 🔒 igual que Rol: bloqueo antes de abrir vista
            if (await EstadoEnUsoAsync(model.id_estadoUsuario))
            {
                TempData["Error"] = "El estado no puede ser modificado ni eliminado porque está en uso.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar(Estado_Usuario model)
        {
            if (!ModelState.IsValid) return View(model);

            var client = _http.CreateClient("Api");

            // 🔒 revalidación
            if (await EstadoEnUsoAsync(model.id_estadoUsuario))
            {
                TempData["Error"] = "El estado no puede ser modificado ni eliminado porque está en uso.";
                return RedirectToAction(nameof(Index));
            }

            var content = new StringContent(JsonSerializer.Serialize(model), System.Text.Encoding.UTF8, "application/json");
            var resp = await client.PutAsync($"{ESTADO_API}/{model.id_estadoUsuario}", content);

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Estado modificado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = $"Error al modificar el estado: {resp.ReasonPhrase}";
            return RedirectToAction(nameof(Index));
        }

        // ===== ELIMINAR =====
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{ESTADO_API}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudo obtener el estado.";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Estado_Usuario>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model == null)
            {
                TempData["Error"] = "Estado inexistente.";
                return RedirectToAction(nameof(Index));
            }

            // 🔒 igual que Rol
            if (await EstadoEnUsoAsync(model.id_estadoUsuario))
            {
                TempData["Error"] = "El estado no puede ser modificado ni eliminado porque está en uso.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost, ActionName("Eliminar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");

            // 🔒 revalidación
            if (await EstadoEnUsoAsync(id))
            {
                TempData["Error"] = "El estado no puede ser modificado ni eliminado porque está en uso.";
                return RedirectToAction(nameof(Index));
            }

            var resp = await client.DeleteAsync($"{ESTADO_API}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"DELETE {ESTADO_API}/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            TempData["Ok"] = "Estado eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
