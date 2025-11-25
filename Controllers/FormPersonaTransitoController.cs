using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SantaRamona.Backoffice.Controllers
{
    // Este controlador es para el formulario de adopción
    // También gestiona la carga dinámica de provincias y localidades
    [Route("[controller]/[action]/{id?}")]

    public class FormPersonaTransitoController : Controller
    {
        private readonly IHttpClientFactory _http;

        private static readonly JsonSerializerOptions JsonOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ====== Rutas API ======
        //private const string RUTA_PERSONA = "https://webapisantaramona.somee.com/api/Persona";
        //private const string RUTA_ESTADO_PERSONA = "https://webapisantaramona.somee.com/api/EstadoPersona";
        //private const string RUTA_PROVINCIA = "https://webapisantaramona.somee.com/api/Provincia";
        //private const string RUTA_LOCALIDAD = "https://webapisantaramona.somee.com/api/Localidad";
        //private const string RUTA_PREGUNTA = "https://webapisantaramona.somee.com/api/Pregunta";
        //private const string RUTA_RESPUESTA = "https://webapisantaramona.somee.com/api/Respuesta/lote";

        private const string RUTA_PERSONA = "/api/Persona";
        private const string RUTA_ESTADO_PERSONA = "/api/EstadoPersona";
        private const string RUTA_PROVINCIA = "/api/Provincia";
        private const string RUTA_LOCALIDAD = "/api/Localidad";
        private const string RUTA_PREGUNTA = "/api/Pregunta";
        private const string RUTA_RESPUESTA = "/api/Respuesta/lote";

        //        //ANTES
        //        //private const string RUTA_PERSONA = "/api/Persona";
        //        //private const string RUTA_ESTADO_PERSONA = "/api/EstadoPersona";
        //        //private const string RUTA_PROVINCIA = "/api/Provincia";
        //        //private const string RUTA_LOCALIDAD = "/api/Localidad";
        public FormPersonaTransitoController(IHttpClientFactory http)
        {
            _http = http;
        }

        // ===================== GET: formulario adopción =====================
        [HttpGet]
        public async Task<IActionResult> FormularioTransito(int? idPersona)
        {
            if (idPersona == null)
            {
                ViewBag.ApiError = "No se recibió la persona a asociar.";
                return RedirectToAction("PersonaTransito");
            }

            ViewBag.IdPersona = idPersona;

            var client = _http.CreateClient("Api");

            // Traer todas las preguntas
            var resp = await client.GetAsync(RUTA_PREGUNTA);
            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"Error al obtener preguntas: {(int)resp.StatusCode} {resp.ReasonPhrase}";
                return View(new List<Pregunta>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var preguntas = JsonSerializer.Deserialize<List<Pregunta>>(json, JsonOps) ?? new List<Pregunta>();

            // Filtrar solo preguntas del formulario de adopción (tipoFormulario = 2)
            preguntas = preguntas
                .Where(p => p.id_tipoFormulario == 2 & p.activo)
                .OrderBy(p => p.orden)
                .ToList();

            // Cargar Provincias y Localidades en ViewBag para el form
            ViewBag.Provincia = await CargarProvinciasSelectAsync(client);
            ViewBag.Localidad = await CargarLocalidadesSelectAsync(client, null);


            var vm = new FormVM
            {
                Preguntas = preguntas,
                Respuestas = new Dictionary<int, string>()
            };


            return View("~/Views/Formularios/FormTransito.cshtml", vm);
        }

        // ===================== POST: enviar respuestas =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FormularioTransito(int idPersona, Dictionary<int, string> respuestas)
        {
            var client = _http.CreateClient("Api");

            try
            {
                // 1) Validaciones básicas
                if (idPersona <= 0)
                {
                    Console.WriteLine("⚠️ idPersona inválido o no enviado.");
                    TempData["Error"] = "No se recibió la persona asociada.";
                    return RedirectToAction("PersonaTransito");
                }

                if (respuestas == null || respuestas.Count == 0)
                {
                    Console.WriteLine("⚠️ No llegaron respuestas desde la vista.");
                    TempData["Error"] = "Debe completar el cuestionario antes de enviar.";
                    return RedirectToAction("PersonaTransito", new { idPersona });
                }

                Console.WriteLine($"✅ Recibidas {respuestas.Count} respuestas para persona {idPersona}.");

                // 2) Crear FORMULARIO (id_tipoFormulario = 1 => Adopción)
                var nuevoFormularioPayload = new
                {
                    id_persona = idPersona,
                    id_tipoFormulario = 2
                };

                var jsonFormulario = JsonSerializer.Serialize(nuevoFormularioPayload);
                var contentFormulario = new StringContent(jsonFormulario, Encoding.UTF8, "application/json");

                // Intentamos crear formulario en: POST /api/Formulario
                var respForm = await client.PostAsync("https://webapisantaramona.somee.com/api/Formulario", contentFormulario);
                if (!respForm.IsSuccessStatusCode)
                {
                    var txt = await respForm.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Error creando formulario: {(int)respForm.StatusCode} {txt}");
                    TempData["Error"] = "No se pudo crear el formulario (error interno).";
                    return RedirectToAction("FormularioTransito", new { idPersona });
                }

                // Leer id_formulario desde la respuesta (se espera que la API retorne el objeto creado)
                var formBody = await respForm.Content.ReadAsStringAsync();
                var formularioCreado = JsonSerializer.Deserialize<Formulario>(formBody, JsonOps);
                if (formularioCreado == null || formularioCreado.id_formulario <= 0)
                {
                    Console.WriteLine("❌ La API no devolvió id_formulario en la creación del formulario.");
                    TempData["Error"] = "No se pudo obtener el ID del formulario creado.";
                    return RedirectToAction("FormularioTransito", new { idPersona });
                }

                int idFormulario = formularioCreado.id_formulario;
                Console.WriteLine($"✅ Formulario creado: id_formulario = {idFormulario}");

                // 3) Preparar payload para lote (si tu API acepta)
                var lotePayload = new
                {
                    respuestas = respuestas.Select(r => new { id_pregunta = r.Key, respuesta = r.Value }).ToList()
                };

                var jsonLote = JsonSerializer.Serialize(lotePayload);
                var contentLote = new StringContent(jsonLote, Encoding.UTF8, "application/json");

                // 4) Intentar POST a /api/Respuesta/lote/{idFormulario}
                var rutaLote = $"https://webapisantaramona.somee.com/api/Respuesta/lote/{idFormulario}";
                var respLote = await client.PostAsync(rutaLote, contentLote);

                if (respLote.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Respuestas guardadas en lote correctamente.");
                    TempData["Ok"] = "Formulario enviado correctamente.";
                    return RedirectToAction("IndexPublic", "HomePublic");
                }

                // Si no funcionó el endpoint lote, hacemos posts individuales (fallback)
                var txtLote = await respLote.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️ Intento lote falló: {(int)respLote.StatusCode} {txtLote}. Intentando guardar individualmente...");

                foreach (var kv in respuestas)
                {
                    var rEntidad = new Respuesta
                    {
                        id_formulario = idFormulario,
                        id_pregunta = kv.Key,
                        respuesta = kv.Value ?? string.Empty
                    };

                    var jr = JsonSerializer.Serialize(rEntidad);
                    var cr = new StringContent(jr, Encoding.UTF8, "application/json");

                    var rResp = await client.PostAsync("https://webapisantaramona.somee.com/api/Respuesta", cr);
                    if (!rResp.IsSuccessStatusCode)
                    {
                        var body = await rResp.Content.ReadAsStringAsync();
                        Console.WriteLine($"❌ Error guardando respuesta pregunta {kv.Key}: {(int)rResp.StatusCode} {body}");
                        // Decidir: seguir intentando o abortar. Aquí seguimos para intentar guardar todas.
                    }
                    else
                    {
                        Console.WriteLine($"✅ Guardada respuesta pregunta {kv.Key}");
                    }
                }

                TempData["Ok"] = "Formulario y respuestas guardadas correctamente.";
                return RedirectToAction("IndexPublic", "HomePublic");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Excepción en FormularioAdopcion POST: {ex}");
                TempData["Error"] = "Ocurrió un error al guardar el formulario.";
                return RedirectToAction("FormularioTransito", new { idPersona });
            }
        }



        // ===================== FORMULARIO PERSONA ================================
        [HttpGet]
        public async Task<IActionResult> PersonaTransito()
        {
            var client = _http.CreateClient("Api");
            var provincias = await CargarProvinciasSelectAsync(client);

            ViewBag.Estados = await CargarEstadosSelectAsync(client);
            ViewBag.Provincia = provincias;
            ViewBag.Localidad = new SelectList(Enumerable.Empty<SelectListItem>());

            return View("~/Views/Formularios/FormPerTransito.cshtml", new Persona { fechaIngreso = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> PersonaTransito([FromForm] Persona persona)
        {
            persona.telefono1 = persona.telefono1?.Trim();
            if (!string.IsNullOrWhiteSpace(persona.telefono2))
                persona.telefono2 = persona.telefono2.Trim();

            if (persona.fechaIngreso == default)
                persona.fechaIngreso = DateTime.Today;

            if (persona.id_estadoPersona == null || persona.id_estadoPersona == 0)
                persona.id_estadoPersona = 1;

            if (!ModelState.IsValid)
            {
                var clientErr = _http.CreateClient("Api");
                ViewBag.Estados = await CargarEstadosSelectAsync(clientErr, persona.id_estadoPersona);
                ViewBag.Provincia = await CargarProvinciasSelectAsync(clientErr, persona.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(clientErr, persona.id_provincia, persona.id_localidad);

                return View("~/Views/Formularios/FormPerTransito.cshtml", persona);
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

                return View("~/Views/Formularios/FormPerTransito.cshtml", persona);
            }

            var respuestaBody = await resp.Content.ReadAsStringAsync();
            var personaCreada = JsonSerializer.Deserialize<Persona>(respuestaBody, JsonOps);

            if (personaCreada == null || personaCreada.id_persona == 0)
            {
                TempData["Error"] = "No se pudo obtener el ID de la persona creada.";
                return RedirectToAction("PersonaTransito");
            }

            TempData["idPersonaCreada"] = personaCreada.id_persona;
            TempData["Ok"] = "Sus datos han sido enviados correctamente.";
            return RedirectToAction("FormularioTransito", "FormPersonaTransito", new { idPersona = personaCreada.id_persona });
        }

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

        public async Task<SelectList> CargarProvinciasSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_PROVINCIA);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());

            var json = await resp.Content.ReadAsStringAsync();
            var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(json, JsonOps) ?? Enumerable.Empty<Provincia>();

            return new SelectList(provincias.Select(p => new { p.id_provincia, p.nombre }),
                                  "id_provincia", "nombre", seleccionado);
        }

        public async Task<SelectList> CargarLocalidadesSelectAsync(HttpClient client, int? idProvincia, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_LOCALIDAD);
            if (!resp.IsSuccessStatusCode) return new SelectList(Enumerable.Empty<SelectListItem>());

            var json = await resp.Content.ReadAsStringAsync();
            var localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(json, JsonOps) ?? Enumerable.Empty<Localidad>();

            if (idProvincia is not null && idProvincia > 0)
                localidades = localidades.Where(l => l.id_provincia == idProvincia);

            return new SelectList(localidades.Select(l => new { l.id_localidad, l.nombre }),
                                  "id_localidad", "nombre", seleccionado);
        }

        // AJAX
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
   

