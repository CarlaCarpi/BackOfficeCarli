using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    public class RazaController : Controller
    {
        private readonly IHttpClientFactory _http;
        public RazaController(IHttpClientFactory http) => _http = http;

        // GET: /Raza
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/raza");

            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"Error API: {(int)resp.StatusCode} - {resp.ReasonPhrase}";
                return View(new List<Raza>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var razas = JsonSerializer.Deserialize<List<Raza>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return View("Index", razas ?? new List<Raza>());
        }

        // GET: /Raza/Crear
        [HttpGet]
        public IActionResult Crear()
        {
            if (TempData["Ok"] is string ok)
                ViewBag.MensajeExito = ok;   //para mostrar el cartel en la vista

            return View(new SantaRamona.Backoffice.Models.Raza());
        }

        // POST: /Raza/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromForm] string raza)
        {
            Console.WriteLine($"[DEBUG] POST /Raza/Crear -> raza = '{raza}'");

            if (string.IsNullOrWhiteSpace(raza))
            {
                ModelState.AddModelError("raza", "La raza es obligatoria.");
                return View(new SantaRamona.Backoffice.Models.Raza { raza = raza });
            }

            var model = new SantaRamona.Backoffice.Models.Raza { raza = raza };

            var client = _http.CreateClient("Api");
            var json = System.Text.Json.JsonSerializer.Serialize(model);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/raza", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/raza -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            //Mostrar mensaje y quedarse en Crear para seguir cargando
            TempData["Ok"] = "Raza creada correctamente.";
            return RedirectToAction(nameof(Crear));
        }
        // GET: /Raza/Editar
        [HttpGet]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/raza/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/raza/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Raza>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (TempData["Ok"] is string ok)
                ViewBag.MensajeExito = ok;

            return View(model);
        }

        // POST: /Raza/Editar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Modificar([FromForm] int id_raza, [FromForm] string raza)
        {
            if (string.IsNullOrWhiteSpace(raza))
            {
                ModelState.AddModelError("raza", "La raza es obligatoria.");
                return View(new Raza { id_raza = id_raza, raza = raza ?? string.Empty });
            }

            var model = new Raza { id_raza = id_raza, raza = raza };

            var client = _http.CreateClient("Api");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/raza/{id_raza}", content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/raza/{id_raza} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            TempData["Ok"] = "Raza actualizada correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = id_raza });
        }
        // GET: /Raza/Eliminar/
        [HttpGet]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");

            var r = await client.GetAsync($"/api/raza/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = r.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "La raza no existe o ya fue eliminada."
                    : $"No se pudo obtener la raza (código {(int)r.StatusCode}).";
                return RedirectToAction(nameof(Index));
            }

            var model = await r.Content.ReadFromJsonAsync<Raza>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            //chequeo real: existe algún animal con esta raza
            bool enUso = false;
            try
            {
                int pagina = 1;
                const int pageSize = 50; // podés subir/bajar este tamaño si querés
                while (true)
                {
                    var pageResp = await client.GetAsync($"/api/animal?pagina={pagina}&pageSize={pageSize}");
                    if (!pageResp.IsSuccessStatusCode)
                    {
                        // si falla la consulta, no marcamos enUso para no bloquear indebidamente
                        break;
                    }

                    var animales = await pageResp.Content.ReadFromJsonAsync<List<AnimalMin>>();
                    if (animales == null || animales.Count == 0)
                    {
                        // no hay más páginas
                        break;
                    }

                    if (animales.Any(a => a.id_raza == id))
                    {
                        enUso = true;
                        break;
                    }

                    // si la página vino incompleta, asumimos fin
                    if (animales.Count < pageSize)
                        break;

                    pagina++;
                    if (pagina > 2000) break; // corta por las dudas
                }
            }
            catch
            {
                enUso = false; // ante error de red, no bloquear la UI
            }

            ViewBag.EnUso = enUso;
            return View(model!);
        }

        // Clase para leer solo lo necesario del JSON de Animal
        private class AnimalMin
        {
            public int id_animal { get; set; }
            public int id_raza { get; set; }
        }

        // POST: /Raza/Eliminar/5
        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"/api/raza/{id}");
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Raza eliminada correctamente.";
                return RedirectToAction(nameof(Index));
            }

            // Volvemos a cargar el modelo para quedarnos en la misma vista
            var r = await client.GetAsync($"/api/raza/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudo eliminar la raza. Intentalo nuevamente.";
                return RedirectToAction(nameof(Index));
            }
            var model = await r.Content.ReadFromJsonAsync<Raza>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            bool esFk = resp.StatusCode == System.Net.HttpStatusCode.Conflict
                        || (int)resp.StatusCode == 422
                        || ((int)resp.StatusCode == 500
                            && (body?.Contains("547") == true
                                || body?.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase) == true
                                || body?.Contains("FK__", StringComparison.OrdinalIgnoreCase) == true));

            if (esFk)
            {
                ViewBag.EnUso = true;
                TempData["Error"] = "No se puede eliminar la raza porque está en uso por uno o más animales.";
                return View("Eliminar", model!);
            }

            // Error no-FK: mostramos error pero dejamos visible el botón Eliminar
            ViewBag.EnUso = false;
            TempData["Error"] = "No se pudo eliminar la raza. Intentalo nuevamente.";
            return View("Eliminar", model!);
        }
    }
}