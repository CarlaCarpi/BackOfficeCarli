using FrontSantaRamona.AdopcionModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PruebaFront.Models;
using santa_ramona_BackOffice.Models;
using SantaRamona.Backoffice.Controllers;
using SantaRamona.Backoffice.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;


namespace SantaRamona.BackOffice.Controllers
{
    // gestiona la carga dinámica de provincias y localidades
    [Route("[controller]/[action]/{id?}")]
    [AllowAnonymous]
    public class HomePublicController : Controller
    {


        private readonly ILogger<HomePublicController> _logger;
        private readonly IHttpClientFactory _http;

        private static readonly JsonSerializerOptions JsonOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ====== Rutas API ======
        private const string RUTA_PUNTO_ACOPIO = "/api/PuntoAcopio";
        private const string RUTA_PROVINCIA = "/api/Provincia";
        private const string RUTA_LOCALIDAD = "/api/Localidad";

        // Único constructor para inyección de dependencias
        public HomePublicController(IHttpClientFactory http, ILogger<HomePublicController> logger)
        {
            _http = http;
            _logger = logger;
        }

        // Página principal (inicio público)
        [HttpGet]
        public async Task<IActionResult> IndexPublic(int? page)
        {
            var client = _http.CreateClient("Api");

            // Obtener animales
            var respAnimales = await client.GetAsync("/api/Animal");
            if (!respAnimales.IsSuccessStatusCode)
            {
                var body = await respAnimales.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/Animal -> {(int)respAnimales.StatusCode} {respAnimales.ReasonPhrase}. Respuesta: {body}";
                return View(Enumerable.Empty<Animal>());
            }

            var animalesJson = await respAnimales.Content.ReadAsStringAsync();
            var animales = JsonSerializer.Deserialize<IEnumerable<Animal>>(animalesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Animal>();

            // Filtrar solo animales en adopción
           
            var animalesEnAdopcion = animales.Where(a => a.id_estadoAnimal == 1 || a.id_estadoAnimal == 2 || a.id_estadoAnimal == 3).ToList();


            // ==== Obtener listas de referencia en paralelo ====
            var tEsp = client.GetAsync("/api/Especie");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            await Task.WhenAll(tEsp, tTam, tEst);

            // ==== Guardar en ViewBag ====
            ViewBag.Especies = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            ViewBag.Tamanos = await ToDict<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano);
            ViewBag.Estados = await ToDict<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado);



            return View(animalesEnAdopcion);
        }

        // Política de privacidad
        public IActionResult Privacy()
        {
            return View();
        }

        // Formulario de voluntariado o contacto
        public IActionResult FormPerVoluntariado()
        {
            
            return View("~/Views/Formularios/FormPerVoluntariado.cshtml");
        }

       
        // Formulario de adopción 
        public IActionResult FormPerAdopcion()
        {
            
            return View("~/Views/Formularios/FormPerAdopcion.cshtml");
        }


        // Formulario de transito
        public IActionResult FormPerTransito()
        {
           
            return View("~/Views/Formularios/FormPerTransito.cshtml");
        }

        public IActionResult FormVoluntariado()
        {
            return View("~/Views/Formularios/FormVoluntariado.cshtml");
            
        }

        // Página de voluntariado
        public IActionResult Voluntariado()
        {

            return View();
        }


        [HttpGet]

        public async Task<IActionResult> Donar()
        {
            var client = _http.CreateClient("Api");

           
            var respDon = await client.GetAsync("/api/donacion");
            var donaciones = Enumerable.Empty<Donacion>();



            if (respDon.IsSuccessStatusCode)
            {
                var jsonDon = await respDon.Content.ReadAsStringAsync();
                donaciones = JsonSerializer.Deserialize<IEnumerable<Donacion>>(jsonDon, JsonOps) ?? Enumerable.Empty<Donacion>();
            }

          
            IEnumerable<Punto_Acopio> puntos = Enumerable.Empty<Punto_Acopio>();
            
            var respPuntos = await client.GetAsync(RUTA_PUNTO_ACOPIO);
            if (respPuntos.IsSuccessStatusCode)
            {
                var jsonPuntos = await respPuntos.Content.ReadAsStringAsync();

                IEnumerable<Punto_Acopio> puntosTmp = Enumerable.Empty<Punto_Acopio>();

                try
                {
                    using var doc = JsonDocument.Parse(jsonPuntos);

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        puntosTmp = JsonSerializer.Deserialize<IEnumerable<Punto_Acopio>>(jsonPuntos, JsonOps)
                                   ?? Enumerable.Empty<Punto_Acopio>();
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("items", out var items))
                        {
                            puntosTmp = JsonSerializer.Deserialize<IEnumerable<Punto_Acopio>>(items.GetRawText(), JsonOps)
                                       ?? Enumerable.Empty<Punto_Acopio>();
                        }
                        else if (doc.RootElement.TryGetProperty("puntos", out var puntosProp))
                        {
                            puntosTmp = JsonSerializer.Deserialize<IEnumerable<Punto_Acopio>>(puntosProp.GetRawText(), JsonOps)
                                       ?? Enumerable.Empty<Punto_Acopio>();
                        }
                        else
                        {
                            
                            puntosTmp = JsonSerializer.Deserialize<IEnumerable<Punto_Acopio>>(jsonPuntos, JsonOps)
                                       ?? Enumerable.Empty<Punto_Acopio>();
                        }
                    }
                }
                catch (JsonException)
                {
                    puntosTmp = Enumerable.Empty<Punto_Acopio>();
                }

                
                puntos = puntosTmp.Where(p => p.activo).ToList();
            }
           
            Dictionary<int, string> provincias = new();
            var respProvincias = await client.GetAsync(RUTA_PROVINCIA);
            if (respProvincias.IsSuccessStatusCode)
            {
                var jsonProvincias = await respProvincias.Content.ReadAsStringAsync();
                provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(jsonProvincias, JsonOps)
                             ?.ToDictionary(p => p.id_provincia, p => p.nombre) ?? new();
            }

           
            Dictionary<int, string> localidades = new();
            var respLocalidades = await client.GetAsync(RUTA_LOCALIDAD);
            if (respLocalidades.IsSuccessStatusCode)
            {
                var jsonLocalidades = await respLocalidades.Content.ReadAsStringAsync();
                localidades = JsonSerializer.Deserialize<IEnumerable<Localidad>>(jsonLocalidades, JsonOps)
                              ?.ToDictionary(l => l.id_localidad, l => l.nombre) ?? new();
            }

          
            var vm = new DonarViewModel
            {
                Donaciones = donaciones,
                PuntosAcopio = puntos,
                Provincias = provincias,
                Localidades = localidades
            };

           
            return View(vm);
        }
        // ============================================================
        // ===================== MÉTODOS AUXILIARES ===================
        // ============================================================
        public async Task<SelectList> CargarProvinciasSelectAsync(HttpClient client, int? seleccionado = null)
        {
            var resp = await client.GetAsync(RUTA_PROVINCIA);

            if (!resp.IsSuccessStatusCode)
            {
                return new SelectList(Enumerable.Empty<SelectListItem>());
            }

            var json = await resp.Content.ReadAsStringAsync();

            var provincias = JsonSerializer.Deserialize<IEnumerable<Provincia>>(json, JsonOps)
                              ?? Enumerable.Empty<Provincia>();

            return new SelectList(provincias.Select(p => new { p.id_provincia, p.nombre }),
                                  "id_provincia", "nombre", seleccionado);
        }

        public async Task<SelectList> CargarLocalidadesSelectAsync(HttpClient client, int? idProvincia, int? seleccionado = null)
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
        // =========================== AJAX ===========================
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


        // Listado general de animales en adopción
        // ===================== ADOPCION =====================
        [HttpGet]
        public async Task<IActionResult> Adopcion(int? page)
        {
            var client = _http.CreateClient("Api");

            // Obtener animales
            var respAnimales = await client.GetAsync("/api/Animal");
            if (!respAnimales.IsSuccessStatusCode)
            {
                var body = await respAnimales.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/Animal -> {(int)respAnimales.StatusCode} {respAnimales.ReasonPhrase}. Respuesta: {body}";
                return View(Enumerable.Empty<Animal>());
            }

            var animalesJson = await respAnimales.Content.ReadAsStringAsync();
            var animales = JsonSerializer.Deserialize<IEnumerable<Animal>>(animalesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Animal>();

            // Filtrar solo animales en adopción
           
            var animalesEnAdopcion = animales.Where(a => a.id_estadoAnimal == 1 || a.id_estadoAnimal == 2 || a.id_estadoAnimal == 3).ToList();


            // ==== PAGINACIÓN ====
            int pageSize = 12; // cantidad de animales por página 6/12/18
            int totalPages = (int)Math.Ceiling(animalesEnAdopcion.Count / (double)pageSize);
            int currentPage = page ?? 1;

            // Obtener solo los animales de la página actual
            var animalesPaged = animalesEnAdopcion
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // ==== Obtener listas de referencia en paralelo ====
            var tEsp = client.GetAsync("/api/Especie");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            await Task.WhenAll(tEsp, tTam, tEst);

            // ==== Guardar en ViewBag ====
            ViewBag.Especies = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            ViewBag.Tamanos = await ToDict<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano);
            ViewBag.Estados = await ToDict<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado);

            // Datos para la vista
            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;

            return View(animalesPaged);
        }


        // Detalle de un animal en adopción
        [HttpGet]
        public async Task<IActionResult> InfoAdopcion(int? id)
        {
            if (id == null)
                return BadRequest();

            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Animal/{id}");

            if (!resp.IsSuccessStatusCode)
                return NotFound();

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Animal>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model is null)
                return NotFound();

            await CargarDiccionariosBasicos();

            return View("InfoAdopcion", model);
        }




        // ===================== HELPERS =====================
        private async Task CargarSelects(int? espSel = null, int? tamSel = null, int? estSel = null)
        {
            var client = _http.CreateClient("Api");

            var tEsp = client.GetAsync("/api/Especie");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");

            await Task.WhenAll(tEsp, tTam, tEst);

            ViewBag.Especies = await ToSelectList<Especie>(tEsp.Result, x => x.id_especie, x => x.especie, espSel);
            ViewBag.Tamanos = await ToSelectList<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano, tamSel);
            ViewBag.Estados = await ToSelectList<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado, estSel);

        }

        private async Task CargarDiccionariosBasicos()
        {
            var client = _http.CreateClient("Api");

            var tEsp = client.GetAsync("/api/Especie");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            ;
            await Task.WhenAll(tEsp, tTam, tEst);

            ViewBag.Especies = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            ViewBag.Tamanos = await ToDict<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano);
            ViewBag.Estados = await ToDict<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado);

        }

        // Convierte HttpResponseMessage a SelectList
        private static async Task<List<SelectListItem>> ToSelectList<T>(
            HttpResponseMessage resp,
            Func<T, int> keySel,
            Func<T, string> textSel,
            int? selected = null)
        {
            var items = new List<SelectListItem> { new SelectListItem { Text = "Seleccione...", Value = "" } };

            if (resp is null || !resp.IsSuccessStatusCode) return items;

            var json = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<IEnumerable<T>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<T>();

            items.AddRange(list.Select(x => new SelectListItem
            {
                Value = keySel(x).ToString(),
                Text = textSel(x),
                Selected = selected.HasValue && keySel(x) == selected.Value
            }));

            return items;
        }

        // Convierte HttpResponseMessage a Dictionary<int,string>
        private static async Task<Dictionary<int, string>> ToDict<T>(
            HttpResponseMessage resp,
            Func<T, int> keySel,
            Func<T, string> valSel)
        {
            if (resp is null || !resp.IsSuccessStatusCode)
                return new Dictionary<int, string>();

            var json = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<IEnumerable<T>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<T>();

            return list.GroupBy(keySel).ToDictionary(g => g.Key, g => valSel(g.First()));
        }



        // Página de error genérica
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


    }
}