using Microsoft.AspNetCore.Mvc;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    [Authorize(Policy = "Activo")]
    public class EstadoController : Controller
    {
        private readonly IHttpClientFactory _http;
        public EstadoController(IHttpClientFactory http) => _http = http;

        // GET: /Estado Animal
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/estadoAnimal");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET /api/estadoAnimal -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(Enumerable.Empty<Estado_Animal>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<IEnumerable<Estado_Animal>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Estado_Animal>();

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(data);
        }

        // GET: /Estado Animal/Crear/
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public IActionResult Crear() => View(new Estado_Animal());

        // POST: /Estado Aniaml/Crear
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear([FromForm] string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                ModelState.AddModelError(nameof(Estado_Animal.estado), "El estado es obligatoria.");
                return View(new Estado_Animal { estado = estado ?? string.Empty });
            }

            var model = new Estado_Animal { estado = estado.Trim() };
            var client = _http.CreateClient("Api");
            var content = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/estadoAnimal", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"POST /api/estadoAnimal -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            ViewBag.Ok = "Estado creado correctamente.";
            ModelState.Clear();
            return View(new Estado_Animal());
        }

        // GET: /Estado Animal/Modificar/
        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/estadoAnimal/{id}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "El estado no existe.";
                return RedirectToAction(nameof(Index));
            }
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"GET /api/estadoAnimal/{id} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Index));
            }

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Estado_Animal>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model == null)
            {
                TempData["Error"] = "No se pudo deserializar el estado.";
                return RedirectToAction(nameof(Index));
            }

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            return View(model); // Vista Modificar.cshtml
        }

        // GET: /Estado Animal/Eliminar/5
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar([FromForm] Estado_Animal model)
        {
            if (model == null || model.id_estadoAnimal <= 0)
            {
                ModelState.AddModelError("", "Identificador inválido.");
                return View(model ?? new Estado_Animal());
            }

            if (string.IsNullOrWhiteSpace(model.estado))
            {
                ModelState.AddModelError(nameof(Estado_Animal.estado), "El estado es obligatoria.");
                return View(model);
            }

            var client = _http.CreateClient("Api");
            var content = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/estadoAnimal/{model.id_estadoAnimal}", content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"PUT /api/estadoAnimal/{model.id_estadoAnimal} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                return View(model);
            }

            TempData["Ok"] = "Estado actualizado correctamente.";
            return RedirectToAction(nameof(Modificar), new { id = model.id_estadoAnimal });
        }

        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var client = _http.CreateClient("Api");

            // Traer el estado a eliminar
            var r = await client.GetAsync($"/api/estadoAnimal/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = r.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "El estado no existe o ya fue eliminado."
                    : $"No se pudo obtener el estado (código {(int)r.StatusCode}).";
                return RedirectToAction(nameof(Index));
            }

            var model = await r.Content.ReadFromJsonAsync<Estado_Animal>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // ¿Hay algún animal con este estado? 
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

                    if (animales.Any(x => x.id_estado == id))
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

        // Solo para leer la respuesta de /api/animal como lista con los campos necesarios
        private class AnimalMin
        {
            public int id_animal { get; set; }
            public int id_estado { get; set; }
        }

        // POST: /Especie/Eliminar/5
        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.DeleteAsync($"/api/estadoAnimal/{id}");
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = "Estado eliminado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            // Volvemos a cargar el modelo para quedarnos en la misma vista
            var r = await client.GetAsync($"/api/estadoAnimal/{id}");
            if (!r.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudo eliminar el estado. Intentalo nuevamente.";
                return RedirectToAction(nameof(Index));
            }
            var model = await r.Content.ReadFromJsonAsync<Estado_Animal>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Si la API devolvió error mostramos bloqueo
            bool esFk = resp.StatusCode == System.Net.HttpStatusCode.Conflict
                        || (int)resp.StatusCode == 422
                        || ((int)resp.StatusCode == 500
                            && (body?.Contains("547") == true
                                || body?.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase) == true
                                || body?.Contains("FK__", StringComparison.OrdinalIgnoreCase) == true));

            if (esFk)
            {
                ViewBag.EnUso = true;
                TempData["Error"] = "No se puede eliminar el estado porque está en uso por uno o más animales.";
                return View("Eliminar", model!);
            }

            // Error no relacionado con FK → dejamos visible el botón
            ViewBag.EnUso = false;
            TempData["Error"] = "No se pudo eliminar el estado. Intentalo nuevamente.";
            return View("Eliminar", model!);
        }
    }
}
