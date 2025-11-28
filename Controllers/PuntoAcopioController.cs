using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    [Authorize(Policy = "Activo")]
    public class PuntoAcopioController : Controller
    {
        private readonly IHttpClientFactory _http;
        public PuntoAcopioController(IHttpClientFactory http) => _http = http;

        // ====== Rutas API ======
        private const string RUTA_PUNTO_ACOPIO = "/api/PuntoAcopio";
        private const string RUTA_PROVINCIA = "/api/Provincia";
        private const string RUTA_LOCALIDAD = "/api/Localidad";

        private static readonly JsonSerializerOptions JsonOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ============================================================
        // ===================== MÉTODOS AUXILIARES ===================
        // ============================================================

        private async Task<SelectList> CargarProvinciasSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_PROVINCIA);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());

            var json = await resp.Content.ReadAsStringAsync();
            var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(json, JsonOps)
                             ?? Enumerable.Empty<Provincia>();

            return new SelectList(
                provincias.Select(p => new { p.id_provincia, p.nombre }),
                "id_provincia", "nombre", seleccionado
            );
        }

        private async Task<SelectList> CargarLocalidadesSelectAsync(HttpClient client, int? idProvincia, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_LOCALIDAD);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());

            var json = await resp.Content.ReadAsStringAsync();
            var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(json, JsonOps)
                              ?? Enumerable.Empty<Localidad>();

            if (idProvincia is not null && idProvincia > 0)
                localidades = localidades.Where(l => l.id_provincia == idProvincia);

            return new SelectList(
                localidades.Select(l => new { l.id_localidad, l.nombre }),
                "id_localidad", "nombre", seleccionado
            );
        }

        // ============================================================
        // ===================== INDEX ================================
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var client = _http.CreateClient("Api");

            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 20;

            // ---- 1) Puntos ----
            IEnumerable<Punto_Acopio> puntos = Enumerable.Empty<Punto_Acopio>();

            var resp = await client.GetAsync(RUTA_PUNTO_ACOPIO);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET {RUTA_PUNTO_ACOPIO} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
            }
            else
            {
                var json = await resp.Content.ReadAsStringAsync();
                try
                {
                    // La API ahora devuelve directamente un array []
                    puntos = JsonSerializer.Deserialize<IEnumerable<Punto_Acopio>>(json, JsonOps)
                             ?? Enumerable.Empty<Punto_Acopio>();
                }
                catch (JsonException je)
                {
                    ViewBag.ApiError = $"Error parseando JSON de {RUTA_PUNTO_ACOPIO}: {je.Message}";
                    puntos = Enumerable.Empty<Punto_Acopio>();
                }
            }

            // Ordeno siempre igual
            var lista = puntos
                .OrderBy(p => p.id_puntoAcopio)
                .ToList();

            var total = lista.Count;
            var pageItems = lista
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var hasMore = (page * pageSize) < total;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.HasMore = hasMore;

            // ---- 2) Provincias (id -> nombre) ----
            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var jsonProv = await respProv.Content.ReadAsStringAsync();
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(jsonProv, JsonOps)
                                 ?? Enumerable.Empty<Provincia>();
                ViewBag.Provincias = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }
            else
            {
                ViewBag.Provincias = new Dictionary<int, string>();
            }

            // ---- 3) Localidades (id -> nombre) ----
            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var jsonLoc = await respLoc.Content.ReadAsStringAsync();
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(jsonLoc, JsonOps)
                                  ?? Enumerable.Empty<Localidad>();
                ViewBag.Localidades = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }
            else
            {
                ViewBag.Localidades = new Dictionary<int, string>();
            }

            // ---- 4) Mensajes ----
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            // ---- 5) Devolver solo la primera página ----
            return View(pageItems);
        }

        // Devuelve más filas para el botón "Ver más"
        [HttpGet]
        public async Task<IActionResult> Mas(int page = 2, int pageSize = 20)
        {
            var client = _http.CreateClient("Api");

            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 20;

            // 1) Traer TODOS los puntos de la API (sin paginación)
            var resp = await client.GetAsync(RUTA_PUNTO_ACOPIO);
            if (!resp.IsSuccessStatusCode)
            {
                // si algo falla, que el front pueda mostrar "Intentar de nuevo"
                return StatusCode((int)resp.StatusCode);
            }

            var json = await resp.Content.ReadAsStringAsync();
            IEnumerable<Punto_Acopio> puntos;
            try
            {
                puntos = JsonSerializer.Deserialize<IEnumerable<Punto_Acopio>>(json, JsonOps)
                         ?? Enumerable.Empty<Punto_Acopio>();
            }
            catch
            {
                puntos = Enumerable.Empty<Punto_Acopio>();
            }

            // Orden y paginado en memoria
            var lista = puntos
                .OrderBy(p => p.id_puntoAcopio)
                .ToList();

            var total = lista.Count;
            var pageItems = lista
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            if (!pageItems.Any())
            {
                Response.Headers["X-HasMore"] = "false";
                return StatusCode(204); // No Content
            }

            var hasMore = (page * pageSize) < total;
            Response.Headers["X-HasMore"] = hasMore ? "true" : "false";

            // Diccionarios que usa la vista para mostrar nombres
            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var jsonProv = await respProv.Content.ReadAsStringAsync();
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(jsonProv, JsonOps)
                                 ?? Enumerable.Empty<Provincia>();
                ViewBag.Provincias = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }
            else ViewBag.Provincias = new Dictionary<int, string>();

            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var jsonLoc = await respLoc.Content.ReadAsStringAsync();
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(jsonLoc, JsonOps)
                                  ?? Enumerable.Empty<Localidad>();
                ViewBag.Localidades = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }
            else ViewBag.Localidades = new Dictionary<int, string>();

            // Devuelvo solo las filas <tr> usando un partial
            return PartialView("_PuntoAcopioRows", pageItems);
        }


        // ============================================================
        // ===================== DETALLE ==============================
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var client = _http.CreateClient("Api");

            var resp = await client.GetAsync($"{RUTA_PUNTO_ACOPIO}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"Error al obtener punto de acopio #{id}.";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Punto_Acopio>(
                await resp.Content.ReadAsStringAsync(), JsonOps
            );

            // Diccionarios para mostrar nombres
            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(
                    await respProv.Content.ReadAsStringAsync(), JsonOps
                ) ?? Enumerable.Empty<Provincia>();
                ViewBag.Provincias = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }
            else ViewBag.Provincias = new Dictionary<int, string>();

            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(
                    await respLoc.Content.ReadAsStringAsync(), JsonOps
                ) ?? Enumerable.Empty<Localidad>();
                ViewBag.Localidades = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }
            else ViewBag.Localidades = new Dictionary<int, string>();

            return View(model);
        }

        // ============================================================
        // ===================== CREAR ================================
        // ============================================================

        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear()
        {
            var client = _http.CreateClient("Api");
            ViewBag.Provincia = await CargarProvinciasSelectAsync(client);
            ViewBag.Localidad = new SelectList(Enumerable.Empty<SelectListItem>());
            return View(new Punto_Acopio { activo = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear([FromForm] Punto_Acopio punto)
        {
            // Normalizaciones básicas
            punto.nombre = punto.nombre?.Trim();
            punto.calle = punto.calle?.Trim();
            if (!string.IsNullOrWhiteSpace(punto.departamento))
                punto.departamento = punto.departamento!.Trim();

            if (!ModelState.IsValid)
            {
                var clientErr = _http.CreateClient("Api");
                ViewBag.Provincia = await CargarProvinciasSelectAsync(clientErr, punto.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(clientErr, punto.id_provincia, punto.id_localidad);
                return View(punto);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(punto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync(RUTA_PUNTO_ACOPIO, content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al guardar punto de acopio: {body}";
                ViewBag.Provincia = await CargarProvinciasSelectAsync(client, punto.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(client, punto.id_provincia, punto.id_localidad);
                return View(punto);
            }

            TempData["Ok"] = "Punto de acopio creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // ===================== MODIFICAR ============================
        // ============================================================

        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{RUTA_PUNTO_ACOPIO}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"No se encontró el punto de acopio #{id}.";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Punto_Acopio>(
                await resp.Content.ReadAsStringAsync(), JsonOps
            );

            ViewBag.Provincia = await CargarProvinciasSelectAsync(client, model?.id_provincia);
            ViewBag.Localidad = await CargarLocalidadesSelectAsync(client, model?.id_provincia, model?.id_localidad);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar([FromForm] Punto_Acopio punto)
        {
            punto.nombre = punto.nombre?.Trim();
            punto.calle = punto.calle?.Trim();
            if (!string.IsNullOrWhiteSpace(punto.departamento))
                punto.departamento = punto.departamento!.Trim();

            if (!ModelState.IsValid)
            {
                var clientErr = _http.CreateClient("Api");
                ViewBag.Provincia = await CargarProvinciasSelectAsync(clientErr, punto.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(clientErr, punto.id_provincia, punto.id_localidad);
                return View(punto);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(punto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"{RUTA_PUNTO_ACOPIO}/{punto.id_puntoAcopio}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al actualizar punto de acopio: {body}";
                ViewBag.Provincia = await CargarProvinciasSelectAsync(client, punto.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(client, punto.id_provincia, punto.id_localidad);
                return View(punto);
            }

            TempData["Ok"] = "Punto de acopio actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // ===================== ELIMINAR =============================
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{RUTA_PUNTO_ACOPIO}/{id}");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El punto de acopio no existe o ya fue eliminado.";
                return RedirectToAction(nameof(Index));
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"Error al obtener punto de acopio: {body}";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Punto_Acopio>(
                await resp.Content.ReadAsStringAsync(), JsonOps
            );

            // Para la vista de confirmación
            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(
                    await respProv.Content.ReadAsStringAsync(), JsonOps
                ) ?? Enumerable.Empty<Provincia>();
                ViewBag.Provincias = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }
            else ViewBag.Provincias = new Dictionary<int, string>();

            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(
                    await respLoc.Content.ReadAsStringAsync(), JsonOps
                ) ?? Enumerable.Empty<Localidad>();
                ViewBag.Localidades = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }
            else ViewBag.Localidades = new Dictionary<int, string>();

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"{RUTA_PUNTO_ACOPIO}/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"Error al eliminar punto de acopio: {body}";
                return RedirectToAction(nameof(Index));
            }

            TempData["Ok"] = "Punto de acopio eliminado correctamente.";
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
