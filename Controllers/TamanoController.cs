using Microsoft.AspNetCore.Mvc;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    public class TamanoController : Controller
    {
        private readonly IHttpClientFactory _http;
        public TamanoController(IHttpClientFactory http) => _http = http;

        // GET: /Tamano
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/tamano");

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"GET /api/tamano -> {(int)resp.StatusCode} {resp.ReasonPhrase}";
                return View(Enumerable.Empty<Tamano>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<IEnumerable<Tamano>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? Enumerable.Empty<Tamano>();

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(data);
        }

        // GET: /Tamano/Crear
        [HttpGet]
        public IActionResult Crear()
        {
            return View(new Tamano());
        }

        // POST: /Tamano/Crear
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] string tamano)
        {
            if (string.IsNullOrWhiteSpace(tamano))
            {
                ModelState.AddModelError(nameof(Tamano.tamano), "El tamaño es obligatoria.");
                return View(new Tamano { tamano = tamano ?? string.Empty });
            }

            var model = new Tamano { tamano = tamano.Trim() };
            var client = _http.CreateClient("Api");

            var content = new StringContent(
                JsonSerializer.Serialize(model),
                Encoding.UTF8,
                "application/json"
            );

            var resp = await client.PostAsync("/api/tamano", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/tamano -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model); // me quedo en pantalla con el error
            }

            // Éxito: me quedo en la misma pantalla para poder crear otro
            ViewBag.Ok = "Tamaño creado correctamente.";
            ModelState.Clear();
            return View(new Tamano());
        }

        // GET: /Tamano/Modificar/
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/tamano/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El tamaño no existe.";
                return RedirectToAction(nameof(Index));
            }
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"GET /api/tamano/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Tamano>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;

            return View(model);
        }

        // POST: /Tamano/Modificar
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] int id_tamano, [FromForm] string tamano)
        {
            if (string.IsNullOrWhiteSpace(tamano))
            {
                ModelState.AddModelError(nameof(Tamano.tamano), "El tamaño es obligatoria.");
                return View(new Tamano { id_tamano = id_tamano, tamano = tamano ?? string.Empty });
            }

            var model = new Tamano { id_tamano = id_tamano, tamano = tamano.Trim() };
            var client = _http.CreateClient("Api");

            var content = new StringContent(
                JsonSerializer.Serialize(model),
                Encoding.UTF8,
                "application/json"
            );

            var resp = await client.PutAsync($"/api/tamano/{id_tamano}", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/tamano/{id_tamano} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            TempData["Ok"] = "Tamaño actualizado correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = id_tamano });
        }

        // GET: /Tamano/Eliminar/
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");

            var r = await client.GetAsync($"/api/tamano/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = r.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "El tamaño no existe o ya fue eliminado."
                    : $"No se pudo obtener el tamaño (código {(int)r.StatusCode}).";
                return RedirectToAction(nameof(Index));
            }

            var model = await r.Content.ReadFromJsonAsync<Tamano>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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

                    if (animales.Any(x => x.id_tamano == id))
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

        // Para leer la respuesta de /api/animal como lista 
        private class AnimalMin
        {
            public int id_animal { get; set; }
            public int id_tamano { get; set; }
        }


        // POST: /Tamano/Eliminar/
        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"/api/tamano/{id}");
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Tamaño eliminado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            var r = await client.GetAsync($"/api/tamano/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudo eliminar el tamaño. Intentalo nuevamente.";
                return RedirectToAction(nameof(Index));
            }
            var model = await r.Content.ReadFromJsonAsync<Tamano>(
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
                TempData["Error"] = "No se puede eliminar el tamaño porque está en uso por uno o más animales.";
                return View("Eliminar", model!);
            }

            ViewBag.EnUso = false;
            TempData["Error"] = "No se pudo eliminar el tamaño. Intentalo nuevamente.";
            return View("Eliminar", model!);
        }
    }
}