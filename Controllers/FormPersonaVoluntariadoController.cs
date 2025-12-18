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
    
    [Route("[controller]/[action]/{id?}")]
    public class FormPersonaVoluntariadoController : Controller
    {
        private readonly IHttpClientFactory _http;

        private static readonly JsonSerializerOptions JsonOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

       


        private const string RUTA_PERSONA = "/api/Persona";
        private const string RUTA_ESTADO_PERSONA = "/api/EstadoPersona";
        private const string RUTA_PROVINCIA = "/api/Provincia";
        private const string RUTA_LOCALIDAD = "/api/Localidad";
        private const string RUTA_PREGUNTA = "/api/Pregunta";
        private const string RUTA_RESPUESTA = "/api/Respuesta/lote";

        public FormPersonaVoluntariadoController(IHttpClientFactory http)
        {
            _http = http;
        }

        // ===================== GET: formulario voluntariado =====================
        [HttpGet]
        public async Task<IActionResult> FormularioVoluntariado(int? idPersona)
        {
            if (idPersona == null)
            {
                ViewBag.ApiError = "No se recibió la persona a asociar.";
                return RedirectToAction("PersonaVoluntario");
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

           
            preguntas = preguntas
                .Where(p => p.id_tipoFormulario == 3 & p.activo) 
                .OrderBy(p => p.orden)
                .ToList();

            // Cargar Provincias y Localidades
            ViewBag.Provincia = await CargarProvinciasSelectAsync(client);
            ViewBag.Localidad = await CargarLocalidadesSelectAsync(client, null);

            
            var vm = new FormVM
            {
                Preguntas = preguntas,
                Respuestas = new Dictionary<int, string>()
            };

            return View("~/Views/Formularios/FormVoluntariado.cshtml",vm );
        }

        // ===================== POST: enviar respuestas voluntariado =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FormularioVoluntariado(int idPersona, Dictionary<int, string> respuestas)
        {
            var client = _http.CreateClient("Api");

            try
            {
                if (idPersona <= 0)
                {
                    Console.WriteLine("⚠️ idPersona inválido o no enviado.");
                    TempData["Error"] = "No se recibió la persona asociada.";
                    return RedirectToAction("PersonaVoluntario");
                }

                if (respuestas == null || respuestas.Count == 0)
                {
                    Console.WriteLine("⚠️ No llegaron respuestas desde la vista.");
                    TempData["Error"] = "Debe completar el cuestionario antes de enviar.";
                    return RedirectToAction("FormularioVoluntariado", new { idPersona });
                }

                Console.WriteLine($"✅ Recibidas {respuestas.Count} respuestas para persona {idPersona}.");

                // Crear FORMULARIO (id_tipoFormulario = 3 => Voluntariado)
                var nuevoFormularioPayload = new
                {
                    id_persona = idPersona,
                    id_tipoFormulario = 3
                };

                var jsonFormulario = JsonSerializer.Serialize(nuevoFormularioPayload);
                var contentFormulario = new StringContent(jsonFormulario, Encoding.UTF8, "application/json");

               
                var respForm = await client.PostAsync("https://webapisantaramona.somee.com/api/Formulario", contentFormulario);
                if (!respForm.IsSuccessStatusCode)
                {
                    var txt = await respForm.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Error creando formulario: {(int)respForm.StatusCode} {txt}");
                    TempData["Error"] = "No se pudo crear el formulario.";
                    return RedirectToAction("FormularioVoluntariado", new { idPersona });
                }

               
                var formBody = await respForm.Content.ReadAsStringAsync();
                var formularioCreado = JsonSerializer.Deserialize<Formulario>(formBody, JsonOps);
                if (formularioCreado == null || formularioCreado.id_formulario <= 0)
                {
                    Console.WriteLine("❌ La API no devolvió id_formulario en la creación del formulario.");
                    TempData["Error"] = "No se pudo obtener el ID del formulario creado.";
                    return RedirectToAction("FormularioVoluntariado", new { idPersona });
                }

                int idFormulario = formularioCreado.id_formulario;
                Console.WriteLine($"✅ Formulario creado: id_formulario = {idFormulario}");

               
                var lotePayload = new
                {
                    respuestas = respuestas.Select(r => new { id_pregunta = r.Key, respuesta = r.Value }).ToList()
                };

                var jsonLote = JsonSerializer.Serialize(lotePayload);
                var contentLote = new StringContent(jsonLote, Encoding.UTF8, "application/json");

              
                var rutaLote = $"https://webapisantaramona.somee.com/api/Respuesta/lote/{idFormulario}";
                var respLote = await client.PostAsync(rutaLote, contentLote);

                if (respLote.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Respuestas guardadas en lote correctamente.");
                    TempData["Ok"] = "Formulario enviado correctamente.";
                    return RedirectToAction("IndexPublic", "HomePublic");
                }

               
                var txtLote = await respLote.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️ Intento lote falló: {(int)respLote.StatusCode} {txtLote}. Intentando guardar individualmente...");

                // fallback: guardado individual
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
                        
                    }
                    else
                    {
                        Console.WriteLine($"✅ Guardada respuesta pregunta {kv.Key}");
                    }
                }

                TempData["Ok"] = "Formulario enviado correctamente.";
                return RedirectToAction("IndexPublic", "HomePublic");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Excepción en FormularioAdopcion POST: {ex}");
                TempData["Error"] = "Ocurrió un error al guardar el formulario.";
                return RedirectToAction("FormularioVoluntariado", new { idPersona });
            }
        }

        // ===================== FORMULARIO PERSONA VOLUNTARIO =====================
        [HttpGet]
        public async Task<IActionResult> PersonaVoluntario()
        {
            var client = _http.CreateClient("Api");
            var provincias = await CargarProvinciasSelectAsync(client);

            ViewBag.Estados = await CargarEstadosSelectAsync(client);
            ViewBag.Provincia = provincias;
            ViewBag.Localidad = new SelectList(Enumerable.Empty<SelectListItem>());

            return View("~/Views/Formularios/FormPerVoluntariado.cshtml", new Persona { fechaIngreso = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> PersonaVoluntario([FromForm] Persona persona)
        {

            persona.telefono1 = persona.telefono1?.Trim();
            if (!string.IsNullOrWhiteSpace(persona.telefono2))
                persona.telefono2 = persona.telefono2.Trim();

            if (persona.fechaIngreso == default)
                persona.fechaIngreso = DateTime.Today;

            if (persona.id_estadoPersona == null || persona.id_estadoPersona == 0)
                persona.id_estadoPersona = 1;

            //  VALIDAR EDAD 
            if (persona.fechaNacimiento != null)
            {
                var hoy = DateTime.Today;
                int edad = hoy.Year - persona.fechaNacimiento.Value.Year;
                if (persona.fechaNacimiento.Value > hoy.AddYears(-edad))
                    edad--;

                if (edad < 18 || edad > 100)
                {
                    ModelState.AddModelError("fechaNacimiento", "Debe tener entre 18 y 100 años.");
                }
            }

            if (!ModelState.IsValid)
            {
                var clientErr = _http.CreateClient("Api");
                ViewBag.Estados = await CargarEstadosSelectAsync(clientErr, persona.id_estadoPersona);
                ViewBag.Provincia = await CargarProvinciasSelectAsync(clientErr, persona.id_provincia);
                ViewBag.Localidad = await CargarLocalidadesSelectAsync(clientErr, persona.id_provincia, persona.id_localidad);

                return View("~/Views/Formularios/FormPerVoluntariado.cshtml", persona);
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
                
                return View("~/Views/Formularios/FormPerVoluntariado.cshtml", persona);
            }

            var respuestaBody = await resp.Content.ReadAsStringAsync();
            var personaCreada = JsonSerializer.Deserialize<Persona>(respuestaBody, JsonOps);

            if (personaCreada == null || personaCreada.id_persona == 0)
            {
                TempData["Error"] = "No se pudo obtener el ID de la persona creada.";
                return RedirectToAction("PersonaVoluntario"); //form de persona
            }

            TempData["idPersonaCreada"] = personaCreada.id_persona;
            TempData["Ok"] = "Sus datos han sido enviados correctamente.";
            return RedirectToAction("FormularioVoluntariado", "FormPersonaVoluntariado", new { idPersona = personaCreada.id_persona });
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