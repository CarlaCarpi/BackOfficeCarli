using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]")]
    public class AnimalController : Controller
    {
        private readonly IHttpClientFactory _http;
        public AnimalController(IHttpClientFactory http) => _http = http;

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");

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

            var tEsp = client.GetAsync("/api/Especie");
            var tRaza = client.GetAsync("/api/Raza");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            await Task.WhenAll(tEsp, tRaza, tTam, tEst);

            ViewBag.Especies = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            ViewBag.Razas = await ToDict<Raza>(tRaza.Result, x => x.id_raza, x => x.raza);
            ViewBag.Tamanos = await ToDict<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano);
            ViewBag.Estados = await ToDict<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado);

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(animals);
        }

        // ===================== CREAR =====================
        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            await CargarSelects();
            return View(new Animal());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] Animal model, IFormFile? imagenFile)
        {
            ModelState.Remove(nameof(Animal.id_usuario));
            if (model.id_usuario <= 0)
                model.id_usuario = 1;

            if (string.IsNullOrWhiteSpace(model.nombre))
                ModelState.AddModelError(nameof(Animal.nombre), "El nombre es obligatorio.");
            if (model.id_especie <= 0) ModelState.AddModelError(nameof(Animal.id_especie), "Seleccione una especie válida.");
            if (model.id_raza <= 0) ModelState.AddModelError(nameof(Animal.id_raza), "Seleccione una raza válida.");
            if (model.id_tamano <= 0) ModelState.AddModelError(nameof(Animal.id_tamano), "Seleccione un tamaño válido.");
            if (model.id_estadoAnimal <= 0) ModelState.AddModelError(nameof(Animal.id_estadoAnimal), "Seleccione un estado válido.");

            if (model.id_persona.HasValue && model.id_persona <= 0) model.id_persona = null;
            if (model.id_pension.HasValue && model.id_pension <= 0) model.id_pension = null;

            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_especie, model.id_raza, model.id_tamano, model.id_estadoAnimal);
                return View(model);
            }

            // la línea para setear la fecha si no viene
            if (!model.fechaIngreso.HasValue)
                model.fechaIngreso = DateTime.Now;

            // Imagen opcional
            byte[]? imageBytes = null;
            if (imagenFile != null && imagenFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await imagenFile.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            var client = _http.CreateClient("Api");

            var payload = new
            {
                id_animal = model.id_animal,
                nombre = model.nombre,
                sexo = model.sexo,
                edadValor = model.edadValor,
                edadUnidad = model.edadUnidad,
                imagen = imageBytes,
                id_especie = model.id_especie,
                id_tamano = model.id_tamano,
                id_raza = model.id_raza,
                id_estadoAnimal = model.id_estadoAnimal,
                id_persona = model.id_persona,
                id_pension = model.id_pension,
                id_usuario = model.id_usuario,
                fechaIngreso = model.fechaIngreso,
                fechaAdopcion = model.fechaAdopcion,
                historia = model.historia,
                seguimiento = model.seguimiento
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/Animal", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"POST /api/Animal -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                await CargarSelects(model.id_especie, model.id_raza, model.id_tamano, model.id_estadoAnimal);
                return View(model);
            }

            ViewBag.Ok = "Animal creado correctamente.";
            ModelState.Clear();
            await CargarSelects();
            return View(new Animal());
        }

        // ===================== MODIFICAR =====================
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Animal/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"No se pudo cargar el animal {id}.";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Animal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (model == null)
            {
                TempData["Error"] = "No se pudo deserializar el animal.";
                return RedirectToAction(nameof(Index));
            }

            if (model.id_usuario <= 0) model.id_usuario = 1;

            await CargarSelects(model.id_especie, model.id_raza, model.id_tamano, model.id_estadoAnimal);
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] Animal model, IFormFile? imagenFile)
        {
            if (model.id_usuario <= 0)
                model.id_usuario = 1;

            if (string.IsNullOrWhiteSpace(model.nombre))
                ModelState.AddModelError(nameof(Animal.nombre), "El nombre es obligatorio.");
            if (model.id_especie <= 0) ModelState.AddModelError(nameof(Animal.id_especie), "Seleccione una especie válida.");
            if (model.id_raza <= 0) ModelState.AddModelError(nameof(Animal.id_raza), "Seleccione una raza válida.");
            if (model.id_tamano <= 0) ModelState.AddModelError(nameof(Animal.id_tamano), "Seleccione un tamaño válido.");
            if (model.id_estadoAnimal <= 0) ModelState.AddModelError(nameof(Animal.id_estadoAnimal), "Seleccione un estado válido.");

            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_especie, model.id_raza, model.id_tamano, model.id_estadoAnimal);
                return View(model);
            }

            byte[]? imageBytes = null;
            if (imagenFile != null && imagenFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await imagenFile.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            var client = _http.CreateClient("Api");

            var payload = new
            {
                id_animal = model.id_animal,
                nombre = model.nombre,
                sexo = model.sexo,
                edadValor = model.edadValor,
                edadUnidad = model.edadUnidad,
                imagen = imageBytes ?? model.imagen,
                id_especie = model.id_especie,
                id_tamano = model.id_tamano,
                id_raza = model.id_raza,
                id_estadoAnimal = model.id_estadoAnimal,
                id_persona = model.id_persona,
                id_pension = model.id_pension,
                id_usuario = model.id_usuario,
                fechaIngreso = model.fechaIngreso,
                fechaAdopcion = model.fechaAdopcion,
                historia = model.historia,
                seguimiento = model.seguimiento
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/Animal/{model.id_animal}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/Animal/{model.id_animal} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                await CargarSelects(model.id_especie, model.id_raza, model.id_tamano, model.id_estadoAnimal);
                return View(model);
            }

            TempData["Ok"] = "Animal actualizado correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = model.id_animal });
        }

        // ===================== DETALLE =====================
        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Animal/{id}");
            if (!resp.IsSuccessStatusCode) return NotFound();

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Animal>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model is null) return NotFound();

            await CargarDiccionariosBasicos();
            return PartialView("DetalleAnimal", model);
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
    }
}
