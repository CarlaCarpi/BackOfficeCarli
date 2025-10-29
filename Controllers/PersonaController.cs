using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;
using System.Linq;          
using System.Net.Http;


namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    public class PersonaController : Controller
    {
        private readonly IHttpClientFactory _http;
        public PersonaController(IHttpClientFactory http) => _http = http;

        // ====== Rutas API ======
        private const string RUTA_PERSONA = "/api/Persona";
        private const string RUTA_ESTADO_PERSONA = "/api/EstadoPersona";
        private const string RUTA_PROVINCIA = "/api/Provincia";
        private const string RUTA_LOCALIDAD = "/api/Localidad";

        private static readonly JsonSerializerOptions JsonOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ============================================================
        // ===================== MÉTODOS AUXILIARES ===================
        // ============================================================

        private async Task<SelectList> CargarEstadosSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_ESTADO_PERSONA);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());

            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<IEnumerable<Estado_Persona>>(json, JsonOps) ?? Enumerable.Empty<Estado_Persona>();

            var dict = lista.ToDictionary(e => e.id_estadoPersona, e => e.descripcion);
            return new SelectList(dict, "Key", "Value", seleccionado);
        }

        private async Task<Dictionary<int, string>> CargarEstadosDictAsync(HttpClient client)
        {
            var resp = await client.GetAsync(RUTA_ESTADO_PERSONA);
            if (!resp.IsSuccessStatusCode) return new Dictionary<int, string>();

            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<IEnumerable<Estado_Persona>>(json, JsonOps) ?? Enumerable.Empty<Estado_Persona>();
            return lista.ToDictionary(e => e.id_estadoPersona, e => e.descripcion);
        }

        private async Task<SelectList> CargarProvinciasSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_PROVINCIA);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());

            var json = await resp.Content.ReadAsStringAsync();
            var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(json, JsonOps) ?? Enumerable.Empty<Provincia>();

            return new SelectList(provincias.Select(p => new { p.id_provincia, p.nombre }),
                                  "id_provincia", "nombre", seleccionado);
        }

        private async Task<SelectList> CargarLocalidadesSelectAsync(HttpClient client, int? idProvincia, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_LOCALIDAD);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());

            var json = await resp.Content.ReadAsStringAsync();
            var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(json, JsonOps) ?? Enumerable.Empty<Localidad>();

            // Filtrar si hay provincia seleccionada
            if (idProvincia is not null && idProvincia > 0)
                localidades = localidades.Where(l => l.id_provincia == idProvincia);

            return new SelectList(localidades.Select(l => new { l.id_localidad, l.nombre }),
                                  "id_localidad", "nombre", seleccionado);
        }

        // ============================================================
        // ===================== INDEX ===============================
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");

            // Personas
            var resp = await client.GetAsync(RUTA_PERSONA);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al obtener personas: {body}";
                ViewBag.Estados = new Dictionary<int, string>();
                ViewBag.Provincia = new Dictionary<int, string>();
                ViewBag.Localidad = new Dictionary<int, string>();
                return View(Enumerable.Empty<Persona>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var personas = JsonSerializer.Deserialize<IEnumerable<Persona>>(json, JsonOps) ?? Enumerable.Empty<Persona>();

            // Estados (id -> descripcion)
            ViewBag.Estados = await CargarEstadosDictAsync(client);

            // Provincias (id -> nombre)
            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var jsonProv = await respProv.Content.ReadAsStringAsync();
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(jsonProv, JsonOps) ?? Enumerable.Empty<Provincia>();
                ViewBag.Provincia = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }
            else
            {
                ViewBag.Provincia = new Dictionary<int, string>();
            }

            // Localidades (id -> nombre)
            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var jsonLoc = await respLoc.Content.ReadAsStringAsync();
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(jsonLoc, JsonOps) ?? Enumerable.Empty<Localidad>();
                ViewBag.Localidad = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }
            else
            {
                ViewBag.Localidad = new Dictionary<int, string>();
            }

            // Mensajes
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(personas);
        }


        // ============================================================
        // ===================== DETALLE ==============================
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var client = _http.CreateClient("Api");

            // Persona
            var resp = await client.GetAsync($"{RUTA_PERSONA}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"Error al obtener persona #{id}.";
                return RedirectToAction(nameof(Index));
            }
            var persona = JsonSerializer.Deserialize<Persona>(
                await resp.Content.ReadAsStringAsync(), JsonOps);

            // Estados (id -> descripción)
            ViewBag.Estados = await CargarEstadosDictAsync(client);

            // Provincias (id -> nombre)
            var respProv = await client.GetAsync(RUTA_PROVINCIA);
            if (respProv.IsSuccessStatusCode)
            {
                var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(
                    await respProv.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Provincia>();
                ViewBag.Provincia = provincias.ToDictionary(p => p.id_provincia, p => p.nombre);
            }
            else ViewBag.Provincia = new Dictionary<int, string>();

            // Localidades (id -> nombre)
            var respLoc = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLoc.IsSuccessStatusCode)
            {
                var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(
                    await respLoc.Content.ReadAsStringAsync(), JsonOps) ?? Enumerable.Empty<Localidad>();
                ViewBag.Localidad = localidades.ToDictionary(l => l.id_localidad, l => l.nombre);
            }
            else ViewBag.Localidad = new Dictionary<int, string>();

            // Si Detalle se renderiza dentro de un modal con fetch:
            // return PartialView(persona);
            return View(persona);
        }


        // ============================================================
        // ===================== CREAR ================================
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            var client = _http.CreateClient("Api");
            ViewBag.Estados = await CargarEstadosSelectAsync(client);
            ViewBag.Provincia = await CargarProvinciasSelectAsync(client);
            ViewBag.Localidad = new SelectList(Enumerable.Empty<SelectListItem>());

            return View(new Persona { fechaIngreso = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] Persona persona)
        {
            // telefono1 es Required y no-nullable en el modelo → Trim directo
            persona.telefono1 = persona.telefono1.Trim();
            if (!string.IsNullOrWhiteSpace(persona.telefono2))
                persona.telefono2 = persona.telefono2!.Trim();

            // si tu compilador se queja de 'default', usá:
            if (persona.fechaIngreso == default(DateTime))
                persona.fechaIngreso = DateTime.Today;

            if (!ModelState.IsValid)
            {
                var clientErr = _http.CreateClient("Api");
                ViewBag.Estados = await CargarEstadosSelectAsync(clientErr, persona.id_estadoPersona);
                ViewBag.Provincia = await CargarProvinciasSelectAsync(clientErr, persona.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(clientErr, persona.id_provincia, persona.id_localidad);
                return View(persona);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(persona);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync(RUTA_PERSONA, content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al guardar persona: {body}";

                ViewBag.Estados = await CargarEstadosSelectAsync(client, persona.id_estadoPersona);
                ViewBag.Provincia = await CargarProvinciasSelectAsync(client, persona.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(client, persona.id_provincia, persona.id_localidad);
                return View(persona);
            }

            TempData["Ok"] = "Persona creada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // ===================== MODIFICAR ============================
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{RUTA_PERSONA}/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"No se encontró la persona #{id}.";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Persona>(await resp.Content.ReadAsStringAsync(), JsonOps);

            ViewBag.Estados = await CargarEstadosSelectAsync(client, model?.id_estadoPersona);
            ViewBag.Provincia = await CargarProvinciasSelectAsync(client, model?.id_provincia);
            ViewBag.Localidad = await CargarLocalidadesSelectAsync(client, model?.id_provincia, model?.id_localidad);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] Persona persona)
        {
            persona.telefono1 = persona.telefono1?.Trim();
            if (!string.IsNullOrWhiteSpace(persona.telefono2)) persona.telefono2 = persona.telefono2!.Trim();

            if (!ModelState.IsValid)
            {
                var clientErr = _http.CreateClient("Api");
                ViewBag.Estados = await CargarEstadosSelectAsync(clientErr, persona.id_estadoPersona);
                ViewBag.Provincia = await CargarProvinciasSelectAsync(clientErr, persona.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(clientErr, persona.id_provincia, persona.id_localidad);
                return View(persona);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(persona);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"{RUTA_PERSONA}/{persona.id_persona}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"Error al actualizar persona: {body}";
                ViewBag.Estados = await CargarEstadosSelectAsync(client, persona.id_estadoPersona);
                ViewBag.Provincia = await CargarProvinciasSelectAsync(client, persona.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(client, persona.id_provincia, persona.id_localidad);
                return View(persona);
            }

            TempData["Ok"] = "Persona actualizada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // ===================== ELIMINAR =============================
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"{RUTA_PERSONA}/{id}");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "La persona no existe o ya fue eliminada.";
                return RedirectToAction(nameof(Index));
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"Error al obtener persona: {body}";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<Persona>(await resp.Content.ReadAsStringAsync(), JsonOps);
            ViewBag.Estados = await CargarEstadosDictAsync(client);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"{RUTA_PERSONA}/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"Error al eliminar persona: {body}";
                return RedirectToAction(nameof(Index));
            }

            TempData["Ok"] = "Persona eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // ====== AJAX: Localidades por Provincia (filtrado MVC) ======
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> LocalidadesPorProvincia(int provinciaId)
        {
            var client = _http.CreateClient("Api");

            // 🔹 Trae TODAS las localidades y filtra por provincia aquí mismo.
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
