using Microsoft.AspNetCore.Mvc;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    public class EspecieController : Controller
    {
        private readonly IHttpClientFactory _http;
        public EspecieController(IHttpClientFactory http) => _http = http;

        // GET: /Especie
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/especie");

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"GET /api/especie -> {(int)resp.StatusCode} {resp.ReasonPhrase}";
                return View(Enumerable.Empty<Especie>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<IEnumerable<Especie>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? Enumerable.Empty<Especie>();

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(data);
        }

        // GET: /Especie/Crear
        [HttpGet]
        public IActionResult Crear()
        {
            return View(new Especie());
        }

        // POST: /Especie/Crear
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] string especie)
        {
            if (string.IsNullOrWhiteSpace(especie))
            {
                ModelState.AddModelError(nameof(Especie.especie), "La especie es obligatoria.");
                return View(new Especie { especie = especie ?? string.Empty });
            }

            var model = new Especie { especie = especie.Trim() };
            var client = _http.CreateClient("Api");

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(model),
                Encoding.UTF8,
                "application/json"
            );

            var resp = await client.PostAsync("/api/especie", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/especie -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model); // me quedo en pantalla con el error
            }

            //me quedo en la misma pantalla para poder crear otra
            ViewBag.Ok = "Especie creada correctamente.";
            ModelState.Clear();                  // limpia validaciones
            return View(new Especie());          // deja el form vacío
        }

        /// GET: /Especie/Modificar/5
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/especie/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "La especie no existe.";
                return RedirectToAction(nameof(Index));
            }
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"GET /api/especie/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = System.Text.Json.JsonSerializer.Deserialize<Especie>(
                json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            //Mostrar mensaje de éxito si viene del POST
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;

            return View(model);
        }

        // POST: /Especie/Modificar
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] int id_especie, [FromForm] string especie)
        {
            if (string.IsNullOrWhiteSpace(especie))
            {
                ModelState.AddModelError(nameof(Especie.especie), "La especie es obligatoria.");
                return View(new Especie { id_especie = id_especie, especie = especie ?? string.Empty });
            }

            var model = new Especie { id_especie = id_especie, especie = especie.Trim() };
            var client = _http.CreateClient("Api");

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(model),
                Encoding.UTF8,
                "application/json"
            );

            var resp = await client.PutAsync($"/api/especie/{id_especie}", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/especie/{id_especie} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            // seteo mensaje y redirijo al mismo Modificar (mismo ID)
            TempData["Ok"] = "Especie actualizada correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = id_especie });
        }
        // GET: /Especie/Eliminar/5
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");

            var r = await client.GetAsync($"/api/especie/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = r.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "La especie no existe o ya fue eliminada."
                    : $"No se pudo obtener la especie (código {(int)r.StatusCode}).";
                return RedirectToAction(nameof(Index));
            }

            var model = await r.Content.ReadFromJsonAsync<Especie>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // ¿Existe algún animal con esta especie?
            bool enUso = false;
            try
            {
                int pagina = 1;
                const int pageSize = 50;
                while (true)
                {
                    var a = await client.GetAsync($"/api/animal?pagina={pagina}&pageSize={pageSize}");
                    if (!a.IsSuccessStatusCode) break;

                    var animales = await a.Content.ReadFromJsonAsync<List<AnimalMin>>();
                    if (animales == null || animales.Count == 0) break;

                    if (animales.Any(x => x.id_especie == id))
                    {
                        enUso = true;
                        break;
                    }

                    if (animales.Count < pageSize) break;
                    pagina++;
                    if (pagina > 2000) break;
                }
            }
            catch { enUso = false; }

            ViewBag.EnUso = enUso;
            return View(model!);
        }

        // Clase 
        private class AnimalMin
        {
            public int id_animal { get; set; }
            public int id_especie { get; set; }
        }


        // POST: /Especie/Eliminar/5
        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"/api/especie/{id}");
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Especie eliminada correctamente.";
                return RedirectToAction(nameof(Index));
            }

            // Volvemos a cargar el modelo para quedarnos en la misma vista
            var r = await client.GetAsync($"/api/especie/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudo eliminar la especie. Intentalo nuevamente.";
                return RedirectToAction(nameof(Index));
            }
            var model = await r.Content.ReadFromJsonAsync<Especie>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            bool esFk = resp.StatusCode == System.Net.HttpStatusCode.Conflict
                        || (int)resp.StatusCode == 422
                        || ((int)resp.StatusCode == 500
                            && (body?.Contains("547") == true
                                || body?.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase) == true
                                || body?.Contains("FK__", StringComparison.OrdinalIgnoreCase) == true));

            if (esFk)
            {
                ViewBag.EnUso = true;
                TempData["Error"] = "No se puede eliminar la especie porque está en uso por uno o más animales.";
                return View("Eliminar", model!);
            }

            ViewBag.EnUso = false;
            TempData["Error"] = "No se pudo eliminar la especie. Intentalo nuevamente.";
            return View("Eliminar", model!);
        }
    }
}
