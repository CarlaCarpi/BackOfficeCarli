using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    public class AnimalController : Controller
    {
        private readonly IHttpClientFactory _http;
        public AnimalController(IHttpClientFactory http) => _http = http;

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");

            // 1) Animales
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

            // 2) Catálogos (para mostrar descripciones en vez de IDs)
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

            return View(animals); // @model IEnumerable<Animal>
        }

        // ===================== CREAR =====================
        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            await CargarSelects();
            return View(new Animal()); // @model Animal
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] Animal model)
        {
            // Validaciones mínimas
            if (string.IsNullOrWhiteSpace(model.nombre))
                ModelState.AddModelError(nameof(Animal.nombre), "El nombre es obligatorio.");
            if (model.id_especie <= 0) ModelState.AddModelError(nameof(Animal.id_especie), "Seleccione una especie válida.");
            if (model.id_raza <= 0) ModelState.AddModelError(nameof(Animal.id_raza), "Seleccione una raza válida.");
            if (model.id_tamano <= 0) ModelState.AddModelError(nameof(Animal.id_tamano), "Seleccione un tamaño válido.");
            if (model.id_estadoAnimal <= 0) ModelState.AddModelError(nameof(Animal.id_estadoAnimal), "Seleccione un estado válido.");
            if (model.id_usuario <= 0) ModelState.AddModelError(nameof(Animal.id_usuario), "Seleccione un usuario válido.");

            // Normalizar opcionales (0 -> null)
            if (model.id_persona.HasValue && model.id_persona <= 0) model.id_persona = null;
            if (model.id_pension.HasValue && model.id_pension <= 0) model.id_pension = null;

            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_especie, model.id_raza, model.id_tamano, model.id_estadoAnimal);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model); // id_estadoAnimal mapeado por [JsonPropertyName("id_estado")]
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/Animal", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/Animal -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                await CargarSelects(model.id_especie, model.id_raza, model.id_tamano, model.id_estadoAnimal);
                return View(model);
            }

            ViewBag.Ok = "Animal creado correctamente.";
            ModelState.Clear();
            await CargarSelects();               // recargar combos
            return View(new Animal());           // quedarse en pantalla para cargar más
        }

        // ===================== MODIFICAR =====================
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Animal/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El animal no existe.";
                return RedirectToAction(nameof(Index));
            }
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"GET /api/Animal/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Animal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (model == null)
            {
                TempData["Error"] = "No se pudo deserializar el animal.";
                return RedirectToAction(nameof(Index));
            }

            if (model.id_usuario <= 0) model.id_usuario = 1; // mientras no haya auth

            await CargarSelects(model.id_especie, model.id_raza, model.id_tamano, model.id_estadoAnimal);
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;

            return View(model); // @model Animal
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] Animal model)
        {
            if (model.id_animal <= 0)
                ModelState.AddModelError("", "Identificador inválido.");

            if (string.IsNullOrWhiteSpace(model.nombre))
                ModelState.AddModelError(nameof(Animal.nombre), "El nombre es obligatorio.");
            if (model.id_especie <= 0) ModelState.AddModelError(nameof(Animal.id_especie), "Seleccione una especie válida.");
            if (model.id_raza <= 0) ModelState.AddModelError(nameof(Animal.id_raza), "Seleccione una raza válida.");
            if (model.id_tamano <= 0) ModelState.AddModelError(nameof(Animal.id_tamano), "Seleccione un tamaño válido.");
            if (model.id_estadoAnimal <= 0) ModelState.AddModelError(nameof(Animal.id_estadoAnimal), "Seleccione un estado válido.");
            if (model.id_usuario <= 0) ModelState.AddModelError(nameof(Animal.id_usuario), "Seleccione un usuario válido.");

            // Normalizar opcionales (0 -> null)
            if (model.id_persona.HasValue && model.id_persona <= 0) model.id_persona = null;
            if (model.id_pension.HasValue && model.id_pension <= 0) model.id_pension = null;

            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_especie, model.id_raza, model.id_tamano, model.id_estadoAnimal);
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model); // [JsonPropertyName("id_estado")] ya mapea
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

        // ===================== DETALLE (PARA MODAL EN INDEX) =====================
        // GET: /Animal/Detalle/5  
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

            await CargarDiccionariosBasicos(); // para mostrar descripciones en la vista parcial
            return PartialView("DetalleAnimal", model);
        }

        // ===================== HELPERS =====================
        private async Task CargarSelects(int? espSel = null, int? razaSel = null, int? tamSel = null, int? estSel = null)
        {
            var client = _http.CreateClient("Api");

            var tEsp = client.GetAsync("/api/Especie");
            var tRza = client.GetAsync("/api/Raza");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/Estado_Animal");
            await Task.WhenAll(tEsp, tRza, tTam, tEst);

            ViewBag.Especies = await ToSelectList<Especie>(tEsp.Result, x => x.id_especie, x => x.especie, espSel);
            ViewBag.Razas = await ToSelectList<Raza>(tRza.Result, x => x.id_raza, x => x.raza, razaSel);
            ViewBag.Tamanos = await ToSelectList<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano, tamSel);
            ViewBag.Estados = await ToSelectList<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado, estSel);
        }

        // NUEVO: solo diccionarios para la vista parcial de detalle
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
                Text = textSel(x),
                Value = keySel(x).ToString(),
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
        // ===================== ELIMINAR =====================
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Animal/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El animal no existe o ya fue eliminado.";
                return RedirectToAction(nameof(Index));
            }
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"GET /api/Animal/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Animal>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model is null)
            {
                TempData["Error"] = "No pudimos cargar los datos del animal.";
                // Opcional: guardar detalle para depurar
                TempData["ApiDetail"] = $"Respuesta recibida: {json}";
                return RedirectToAction(nameof(Index));
            }

            // Diccionarios para mostrar descripciones (no IDs)
            await CargarDiccionariosBasicos();

            // Regla: si tiene persona o pensión, bloquear eliminación
            var motivos = new List<string>();
            if (model.id_persona.HasValue) motivos.Add("tiene una persona asignada");
            if (model.id_pension.HasValue) motivos.Add("tiene una pensión asignada");

            ViewBag.Bloqueado = motivos.Any();
            ViewBag.Motivo = string.Join(" y ", motivos);

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");

            // Vuelvo a leer por seguridad y revalido la regla
            var respGet = await client.GetAsync($"/api/Animal/{id}");
            if (respGet.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El animal no existe o ya fue eliminado.";
                return RedirectToAction(nameof(Index));
            }
            if (!respGet.IsSuccessStatusCode)
            {
                var body = await respGet.Content.ReadAsStringAsync();
                TempData["Error"] = $"GET /api/Animal/{id} -> {(int)respGet.StatusCode} {respGet.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var json = await respGet.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Animal>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model is null)
            {
                TempData["Error"] = "No pudimos cargar los datos del animal.";
                // Opcional: guardar detalle para depurar
                TempData["ApiDetail"] = $"Respuesta recibida: {json}";
                return RedirectToAction(nameof(Index));
            }

            if (model.id_persona.HasValue || model.id_pension.HasValue)
            {
                TempData["Error"] = "No se puede eliminar este animal porque tiene datos asociados (persona o pensión).";
                return RedirectToAction(nameof(Eliminar), new { id });
            }

            // Intento borrar
            var respDel = await client.DeleteAsync($"/api/Animal/{id}");
            if (!respDel.IsSuccessStatusCode)
            {
                var body = await respDel.Content.ReadAsStringAsync();

                // Mensaje si la API devuelve conflicto por FK
                if (respDel.StatusCode == System.Net.HttpStatusCode.Conflict ||
                    respDel.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                    (int)respDel.StatusCode == 422)
                {
                    TempData["Error"] = "No se puede eliminar el animal porque está en uso.";
                    if (!string.IsNullOrWhiteSpace(body)) TempData["ApiDetail"] = body;
                    return RedirectToAction(nameof(Eliminar), new { id });
                }

                TempData["Error"] = $"DELETE /api/Animal/{id} -> {(int)respDel.StatusCode} {respDel.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Eliminar), new { id });
            }

            TempData["Ok"] = "Animal eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }

    }
}