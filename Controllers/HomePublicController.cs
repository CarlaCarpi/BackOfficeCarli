using System.Diagnostics;
using System.Text.Json;
using FrontSantaRamona.AdopcionModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PruebaFront.Models;
using santa_ramona_BackOffice.Models;
using SantaRamona.Backoffice.Controllers;
using SantaRamona.Backoffice.Models;


namespace SantaRamona.BackOffice.Controllers
{
    public class HomePublicController : Controller
    {
       

        private readonly ILogger<HomePublicController> _logger;
        private readonly IHttpClientFactory _http;

        // Único constructor para inyección de dependencias
        public HomePublicController(IHttpClientFactory http, ILogger<HomePublicController> logger)
        {
            _http = http;
            _logger = logger;
        }

        // Página principal (inicio público)
        [HttpGet]
        public async Task<IActionResult>  IndexPublic(int? page)
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
            //var animalesEnAdopcion = animals.Where(a => a.id_estadoAnimal == 2).ToList();
            var animalesEnAdopcion = animales.Where(a => a.id_estadoAnimal == 1 || a.id_estadoAnimal == 2 || a.id_estadoAnimal == 3).ToList();


            // ==== Obtener listas de referencia en paralelo ====
            var tEsp = client.GetAsync("/api/Especie");
            var tRaza = client.GetAsync("/api/Raza");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            await Task.WhenAll(tEsp, tRaza, tTam, tEst);

            // ==== Guardar en ViewBag ====
            ViewBag.Especies = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            ViewBag.Razas = await ToDict<Raza>(tRaza.Result, x => x.id_raza, x => x.raza);
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
        public IActionResult FormPersona()
        {
            // Podés tener esta vista en /Views/Formularios/FormPersona.cshtml
            return View("~/Views/Formularios/FormPersona.cshtml");
        }

        // Página de voluntariado
        public IActionResult Voluntariado()
        {
            var voluntariados = new List<VoluntariadoInfo>
            {
                new VoluntariadoInfo { Id = 1, Texto = "Ser mayor de 18 años", ImagenUrl = "/images/juancito.jpg" },
                new VoluntariadoInfo { Id = 2, Texto = "Disponibilidad fines de semana", ImagenUrl = "/images/nina.jpg" },
                new VoluntariadoInfo { Id = 3, Texto = "Amor por los animales", ImagenUrl = "/images/sasha.jpg" }
            };

            return View(voluntariados);
        }

        // Página para donar
        public IActionResult Donar()
        {
            return View();
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
            //var animalesEnAdopcion = animals.Where(a => a.id_estadoAnimal == 2).ToList();
            var animalesEnAdopcion = animales.Where(a => a.id_estadoAnimal == 1 || a.id_estadoAnimal == 2 || a.id_estadoAnimal == 3).ToList();


            // ==== PAGINACIÓN ====
            int pageSize = 1; // cantidad de animales por página 6/12/18
            int totalPages = (int)Math.Ceiling(animalesEnAdopcion.Count / (double)pageSize);
            int currentPage = page ?? 1;

            // Obtener solo los animales de la página actual
            var animalesPaged = animalesEnAdopcion
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // ==== Obtener listas de referencia en paralelo ====
            var tEsp = client.GetAsync("/api/Especie");
            var tRaza = client.GetAsync("/api/Raza");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            await Task.WhenAll(tEsp, tRaza, tTam, tEst);

            // ==== Guardar en ViewBag ====
            ViewBag.Especies = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            ViewBag.Razas = await ToDict<Raza>(tRaza.Result, x => x.id_raza, x => x.raza);
            ViewBag.Tamanos = await ToDict<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano);
            ViewBag.Estados = await ToDict<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado);

            // Datos para la vista
            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;

            return View(animalesPaged);
        }


        // Detalle de un animal en adopción
        [HttpGet]
        public async Task<IActionResult> InfoAdopcion(int id)
        {
            
                var client = _http.CreateClient("Api");
                var resp = await client.GetAsync($"/api/Animal/{id}");
                if (!resp.IsSuccessStatusCode) return NotFound();

                var json = await resp.Content.ReadAsStringAsync();
                var model = JsonSerializer.Deserialize<Animal>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (model is null) return NotFound();

                await CargarDiccionariosBasicos();
                return PartialView("InfoAdopcion", model);
            
        }




        // ===================== HELPERS =====================
        private async Task CargarSelects(int? espSel = null, int? razaSel = null, int? tamSel = null, int? estSel = null)
        {
            var client = _http.CreateClient("Api");

            var tEsp = client.GetAsync("/api/Especie");
            var tRza = client.GetAsync("/api/Raza");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal"); 
           
            await Task.WhenAll(tEsp, tRza, tTam, tEst);

            ViewBag.Especies = await ToSelectList<Especie>(tEsp.Result, x => x.id_especie, x => x.especie, espSel);
            ViewBag.Razas = await ToSelectList<Raza>(tRza.Result, x => x.id_raza, x => x.raza, razaSel);
            ViewBag.Tamanos = await ToSelectList<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano, tamSel);
            ViewBag.Estados = await ToSelectList<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado, estSel);
            
        }

        private async Task CargarDiccionariosBasicos()
        {
            var client = _http.CreateClient("Api");

            var tEsp = client.GetAsync("/api/Especie");
            var tRza = client.GetAsync("/api/Raza");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            ;
            await Task.WhenAll(tEsp, tRza, tTam, tEst); 

            ViewBag.Especies = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            ViewBag.Razas = await ToDict<Raza>(tRza.Result, x => x.id_raza, x => x.raza);
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
