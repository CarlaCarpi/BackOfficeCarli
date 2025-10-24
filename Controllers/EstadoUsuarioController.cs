using Microsoft.AspNetCore.Mvc;
using SantaRamona.Backoffice.Models;
using System.Text.Json;
using System.Text;

namespace SantaRamona.Backoffice.Controllers
{
    public class EstadoUsuarioController : Controller
    {
        private readonly IHttpClientFactory _http;
        public EstadoUsuarioController(IHttpClientFactory http) => _http = http;

        // ---------------- HELPERS ----------------
        private async Task<bool> EstadoEnUsoAsync(int idEstado)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/usuario");
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync();
            var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Usuario>();

            return usuarios.Any(u => u.id_estadoUsuario == idEstado);
        }

        // ---------------- INDEX ----------------
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/Estado_Usuario");

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"Error al obtener los estados ({(int)resp.StatusCode})";
                return View(Enumerable.Empty<Estado_Usuario>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var estados = JsonSerializer.Deserialize<IEnumerable<Estado_Usuario>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Estado_Usuario>();

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(estados);
        }

        // ---------------- CREAR ----------------
        [HttpGet]
        public IActionResult Crear() => View(new Estado_Usuario());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Estado_Usuario model)
        {
            if (string.IsNullOrWhiteSpace(model.descripcion))
            {
                ModelState.AddModelError(nameof(Estado_Usuario.descripcion), "La descripción es obligatoria.");
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/Estado_Usuario", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/Estado_Usuario -> {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}";
                return View(model);
            }

            TempData["Ok"] = "Estado creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------- MODIFICAR ----------------
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Estado_Usuario/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El estado no existe.";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Estado_Usuario>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // ⚠️ si está en uso, mostramos alerta en la vista y/o bloqueamos en POST
            ViewBag.EnUso = await EstadoEnUsoAsync(id);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar(Estado_Usuario model)
        {
            if (string.IsNullOrWhiteSpace(model.descripcion))
            {
                ModelState.AddModelError(nameof(Estado_Usuario.descripcion), "La descripción es obligatoria.");
                return View(model);
            }

            // 🔒 Bloqueo duro si está en uso
            if (await EstadoEnUsoAsync(model.id_estadoUsuario))
            {
                TempData["Error"] = "No se puede modificar este estado porque está en uso.";
                return RedirectToAction(nameof(Index));
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await client.PutAsync($"/api/Estado_Usuario/{model.id_estadoUsuario}", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/Estado_Usuario/{model.id_estadoUsuario} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}";
                return View(model);
            }

            TempData["Ok"] = "Estado actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------- ELIMINAR ----------------
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Estado_Usuario/{id}");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El estado no existe o ya fue eliminado.";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Estado_Usuario>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // ⚠️ Bloquear si está en uso (para ocultar botón Eliminar y mostrar aviso)
            if (await EstadoEnUsoAsync(id))
            {
                ViewBag.Bloqueado = true;
                ViewBag.Motivo = "está asignado a uno o más usuarios";
            }

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            // 🔒 Revalidar por seguridad
            if (await EstadoEnUsoAsync(id))
            {
                TempData["Error"] = "No se puede eliminar este estado porque está en uso.";
                return RedirectToAction(nameof(Eliminar), new { id });
            }

            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"/api/Estado_Usuario/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();

                if (resp.StatusCode == System.Net.HttpStatusCode.Conflict ||
                    resp.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                    (int)resp.StatusCode == 422)
                {
                    TempData["Error"] = "No se puede eliminar este estado porque está en uso.";
                    if (!string.IsNullOrWhiteSpace(body)) TempData["ApiDetail"] = body;
                    return RedirectToAction(nameof(Eliminar), new { id });
                }

                TempData["Error"] = $"DELETE /api/Estado_Usuario/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}";
                return RedirectToAction(nameof(Eliminar), new { id });
            }

            TempData["Ok"] = "Estado eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
