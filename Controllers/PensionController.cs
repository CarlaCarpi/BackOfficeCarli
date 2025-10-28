using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Net.Http;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    public class PensionController : Controller
    {
        private readonly IHttpClientFactory _http;
        public PensionController(IHttpClientFactory http) => _http = http;

        // ====== Rutas API (ajustá si tu API usa otros nombres) ======
        private const string RUTA_PENSION = "/api/Pension";
        private const string RUTA_ESTADO_PENSION = "/api/EstadoPension";
        private const string RUTA_PROVINCIA = "/api/Provincia";
        private const string RUTA_LOCALIDAD = "/api/Localidad";
        private const string RUTA_USUARIO = "/api/Usuario";

        private static readonly JsonSerializerOptions JsonOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ============================================================
        // ===================== MÉTODOS AUXILIARES ===================
        // ============================================================

        private async Task<SelectList> CargarEstadosPensionSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_ESTADO_PENSION);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());
            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<IEnumerable<Estado_Pension>>(json, JsonOps) ?? Enumerable.Empty<Estado_Pension>();
            var dict = lista.ToDictionary(e => e.id_estadoPension, e => e.descripcion);
            return new SelectList(dict, "Key", "Value", seleccionado);
        }

        private async Task<Dictionary<int, string>> CargarEstadosPensionDictAsync(HttpClient client)
        {
            var resp = await client.GetAsync(RUTA_ESTADO_PENSION);
            if (!resp.IsSuccessStatusCode) return new Dictionary<int, string>();
            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<IEnumerable<Estado_Pension>>(json, JsonOps) ?? Enumerable.Empty<Estado_Pension>();
            return lista.ToDictionary(e => e.id_estadoPension, e => e.descripcion);
        }

        private async Task<SelectList> CargarProvinciasSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_PROVINCIA);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());
            var json = await resp.Content.ReadAsStringAsync();
            var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(json, JsonOps) ?? Enumerable.Empty<Provincia>();
            return new SelectList(provincias.Select(p => new { p.id_provincia, p.nombre }), "id_provincia", "nombre", seleccionado);
        }

        private async Task<SelectList> CargarLocalidadesSelectAsync(HttpClient client, int? idProvincia, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_LOCALIDAD);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());
            var json = await resp.Content.ReadAsStringAsync();
            var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(json, JsonOps) ?? Enumerable.Empty<Localidad>();
            if (idProvincia is not null && idProvincia > 0)
                localidades = localidades.Where(l => l.id_provincia == idProvincia);
            return new SelectList(localidades.Select(l => new { l.id_localidad, l.nombre }), "id_localidad", "nombre", seleccionado);
        }

        private async Task<SelectList> CargarUsuariosSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_USUARIO);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());
            var json = await resp.Content.ReadAsStringAsync();
            var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(json, JsonOps) ?? Enumerable.Empty<Usuario>();
            // Ajustá el "texto" visible según tu modelo Usuario
            return new SelectList(usuarios.Select(u => new { u.id_usuario, nombre = string.IsNullOrWhiteSpace(u.nombre) ? $"Usuario #{u.id_usuario}" : u.nombre }),
                                  "id_usuario", "nombre", seleccionado);
        }

        // ============================================================
        // ===================== INDEX ===============================
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");

            // Pensiones
            var resp = await client.GetAsync(RUTA_PENSION);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al obtener pensiones: {body}";
                ViewBag.Estados = new Dictionary<int, string>();
                ViewBag.Provincias = new Dictionary<int, string>();
                ViewBag.Localidades = new Dictionary<int, string>();
                ViewBag.Usuarios = new Dictionary<int, string>();
                return View(Enumerable.Empty<Pension>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var pensiones = JsonSerializer.Deserialize<IEnumerable<Pension>>(json, JsonOps) ?? Enumerable.Empty<Pension>();

            // Estados
            ViewBag.Estados = await CargarEstadosPensionDictAsync(client);

            // Provincias
            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(await respProv.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Provincia>();
                ViewBag.Provincias = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }
            else ViewBag.Provincias = new Dictionary<int, string>();

            // Localidades
            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(await respLoc.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Localidad>();
                ViewBag.Localidades = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }
            else ViewBag.Localidades = new Dictionary<int, string>();

            // Usuarios (para mostrar nombre en la grilla si querés)
            var respUsr = await client.GetAsync(RUTA_USUARIO);
            if (respUsr.IsSuccessStatusCode)
            {
                var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(await respUsr.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Usuario>();
                ViewBag.Usuarios = usuarios.ToDictionary(u => u.id_usuario, u => string.IsNullOrWhiteSpace(u.nombre) ? $"Usuario #{u.id_usuario}" : u.nombre);
            }
            else ViewBag.Usuarios = new Dictionary<int, string>();

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(pensiones);
        }

        // ============================================================
        // ===================== DETALLE ==============================
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var client = _http.CreateClient("Api");

            var resp = await client.GetAsync($"{RUTA_PENSION}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"Error al obtener pensión #{id}.";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Pension>(await resp.Content.ReadAsStringAsync(), JsonOps);

            ViewBag.Estados = await CargarEstadosPensionDictAsync(client);

            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(await respProv.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Provincia>();
                ViewBag.Provincias = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }
            else ViewBag.Provincias = new Dictionary<int, string>();

            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(await respLoc.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Localidad>();
                ViewBag.Localidades = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }
            else ViewBag.Localidades = new Dictionary<int, string>();

            var respUsr = await client.GetAsync(RUTA_USUARIO);
            if (respUsr.IsSuccessStatusCode)
            {
                var usuarios = JsonSerializer.Deserialize<IEnumerable<Usuario>>(await respUsr.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Usuario>();
                ViewBag.Usuarios = usuarios.ToDictionary(u => u.id_usuario, u => string.IsNullOrWhiteSpace(u.nombre) ? $"Usuario #{u.id_usuario}" : u.nombre);
            }
            else ViewBag.Usuarios = new Dictionary<int, string>();

            return View(model);
        }

        // ============================================================
        // ===================== CREAR ================================
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            var client = _http.CreateClient("Api");
            ViewBag.Estados = await CargarEstadosPensionSelectAsync(client);
            ViewBag.Provincias = await CargarProvinciasSelectAsync(client);
            ViewBag.Localidades = new SelectList(Enumerable.Empty<SelectListItem>());
            ViewBag.Usuarios = await CargarUsuariosSelectAsync(client);

            return View(new Pension { fechaIngreso = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] Pension model)
        {
            // Normalizaciones mínimas
            model.telefono1 = model.telefono1?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(model.telefono2)) model.telefono2 = model.telefono2!.Trim();
            if (model.fechaIngreso == default(DateTime)) model.fechaIngreso = DateTime.Today;

            if (!ModelState.IsValid)
            {
                var clientErr = _http.CreateClient("Api");
                ViewBag.Estados = await CargarEstadosPensionSelectAsync(clientErr, model.id_estadoPension);
                ViewBag.Provincias = await CargarProvinciasSelectAsync(clientErr, model.id_provincia);
                ViewBag.Localidades = await CargarLocalidadesSelectAsync(clientErr, model.id_provincia, model.id_localidad);
                ViewBag.Usuarios = await CargarUsuariosSelectAsync(clientErr, model.id_usuario);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(RUTA_PENSION, content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al guardar pensión: {body}";
                ViewBag.Estados = await CargarEstadosPensionSelectAsync(client, model.id_estadoPension);
                ViewBag.Provincias = await CargarProvinciasSelectAsync(client, model.id_provincia);
                ViewBag.Localidades = await CargarLocalidadesSelectAsync(client, model.id_provincia, model.id_localidad);
                ViewBag.Usuarios = await CargarUsuariosSelectAsync(client, model.id_usuario);
                return View(model);
            }

            TempData["Ok"] = "Pensión creada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // ===================== MODIFICAR ============================
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{RUTA_PENSION}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"No se encontró la pensión #{id}.";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Pension>(await resp.Content.ReadAsStringAsync(), JsonOps);

            ViewBag.Estados = await CargarEstadosPensionSelectAsync(client, model?.id_estadoPension);
            ViewBag.Provincias = await CargarProvinciasSelectAsync(client, model?.id_provincia);
            ViewBag.Localidades = await CargarLocalidadesSelectAsync(client, model?.id_provincia, model?.id_localidad);
            ViewBag.Usuarios = await CargarUsuariosSelectAsync(client, model?.id_usuario);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] Pension model)
        {
            model.telefono1 = model.telefono1?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(model.telefono2)) model.telefono2 = model.telefono2!.Trim();

            if (!ModelState.IsValid)
            {
                var clientErr = _http.CreateClient("Api");
                ViewBag.Estados = await CargarEstadosPensionSelectAsync(clientErr, model.id_estadoPension);
                ViewBag.Provincias = await CargarProvinciasSelectAsync(clientErr, model.id_provincia);
                ViewBag.Localidades = await CargarLocalidadesSelectAsync(clientErr, model.id_provincia, model.id_localidad);
                ViewBag.Usuarios = await CargarUsuariosSelectAsync(clientErr, model.id_usuario);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await client.PutAsync($"{RUTA_PENSION}/{model.id_pension}", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al actualizar pensión: {body}";
                ViewBag.Estados = await CargarEstadosPensionSelectAsync(client, model.id_estadoPension);
                ViewBag.Provincias = await CargarProvinciasSelectAsync(client, model.id_provincia);
                ViewBag.Localidades = await CargarLocalidadesSelectAsync(client, model.id_provincia, model.id_localidad);
                ViewBag.Usuarios = await CargarUsuariosSelectAsync(client, model.id_usuario);
                return View(model);
            }

            TempData["Ok"] = "Pensión actualizada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // ===================== ELIMINAR =============================
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{RUTA_PENSION}/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "La pensión no existe o ya fue eliminada.";
                return RedirectToAction(nameof(Index));
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"Error al obtener pensión: {body}";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Pension>(await resp.Content.ReadAsStringAsync(), JsonOps);
            ViewBag.Estados = await CargarEstadosPensionDictAsync(client);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"{RUTA_PENSION}/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"Error al eliminar pensión: {body}";
                return RedirectToAction(nameof(Index));
            }

            TempData["Ok"] = "Pensión eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // ====== AJAX: Localidades por Provincia (filtrado MVC) ======
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> LocalidadesPorProvincia(int provinciaId)
        {
            var client = _http.CreateClient("Api");

            var resp = await client.GetAsync(RUTA_LOCALIDAD);
            if (!resp.IsSuccessStatusCode)
                return StatusCode((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());

            var json = await resp.Content.ReadAsStringAsync();
            var todas = JsonSerializer.Deserialize<IEnumerable<Localidad>>(json, JsonOps) ?? Enumerable.Empty<Localidad>();

            var filtradas = todas
                .Where(l => l.id_provincia == provinciaId)
                .OrderBy(l => l.nombre)
                .Select(l => new { l.id_localidad, l.nombre });

            return Json(filtradas);
        }
    }
}
