using System.Diagnostics;
using System.Text.Json;
using FrontSantaRamona.AdopcionModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        public IActionResult IndexPublic()
        {
            return View();
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
        public async Task<IActionResult> Adopcion()
        {
            var client = _http.CreateClient("Api");

            // Obtener animales
            var respAnimals = await client.GetAsync("/api/Animal");
            if (!respAnimals.IsSuccessStatusCode)
            {
                var body = await respAnimals.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/Animal -> {(int)respAnimals.StatusCode} {respAnimals.ReasonPhrase}. Respuesta: {body}";
                return View(Enumerable.Empty<Animal>());
            }

            var animalsJson = await respAnimals.Content.ReadAsStringAsync();
            var animals = JsonSerializer.Deserialize<IEnumerable<Animal>>(animalsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Animal>();

            // Obtener listas de referencia en paralelo
            var tEsp = client.GetAsync("/api/Especie");
            var tRaza = client.GetAsync("/api/Raza");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            await Task.WhenAll(tEsp, tRaza, tTam, tEst);

            // Guardar en ViewBag como diccionarios
            ViewBag.Especies = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            ViewBag.Razas = await ToDict<Raza>(tRaza.Result, x => x.id_raza, x => x.raza);
            ViewBag.Tamanos = await ToDict<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano);
            ViewBag.Estados = await ToDict<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado);

            // Mensajes temporales
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(animals);

            int pageSize = 6; // Animales por página 6/12/18
            var animalsEnAdopcion = animals.Where(a => a.id_estadoAnimal == 2).ToList();
            int totalPages = (int)Math.Ceiling(animalsEnAdopcion.Count / (double)pageSize);

            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.CurrentPage = 1; // Por defecto
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

        // Detalle de un animal en adopción
        public IActionResult InfoAdopcion(int id, int? page)
        {
            var razas = new Dictionary<int, string>
            {
                {1, "De Raza"},
                {2, "Mestizo"}
            };

            var especies = new Dictionary<int, string>
            {
                {1, "Perro"},
                {2, "Gato"}
            };

            var tamanos = new Dictionary<int, string>
            {
                {1, "Grande"},
                {2, "Mediano"},
                {3, "Chico"}
            };

            var animales = new List<Adopcion>
            {
                new Adopcion { Id_Animal = 1, Nombre = "Luna", Edad = 2, Imagen = "/images/adoptados/Labrador.luna.jpg", Id_Raza = 1, Id_Tamano = 1, Historia = "Fue encontrada en una plaza..." },
                new Adopcion { Id_Animal = 2, Nombre = "Max", Edad = 3, Imagen = "/images/adoptados/Beagle.max.jpg", Id_Raza = 1, Id_Tamano = 2, Historia = "Vivió atado casi toda su vida..." },
                // Podés agregar más animales si querés
            };

            var mascota = animales.FirstOrDefault(a => a.Id_Animal == id);
            if (mascota == null)
                return NotFound();

            ViewBag.Razas = razas;
            ViewBag.Especies = especies;
            ViewBag.Tamanos = tamanos;
            ViewBag.Page = page ?? 1;

            return View(mascota);
        }

        // Página de error genérica
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
