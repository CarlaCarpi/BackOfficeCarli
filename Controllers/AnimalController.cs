using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SantaRamona.Backoffice.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Security.Claims;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]/{id?}")]
    [Authorize(Policy = "Activo")]
    public class AnimalController : Controller
    {
        private readonly IHttpClientFactory _http;
        public AnimalController(IHttpClientFactory http) => _http = http;

        // ====== Rutas API ======
        private const string RUTA_ANIMAL = "/api/Animal";

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20, string? q = null)
        {
            var client = _http.CreateClient("Api");

            // Normalizar y guardar búsqueda
            q = (q ?? "").Trim();
            ViewBag.Query = q;

            // === 1) Animales (API con paginación server-side) ===
            var url = $"{RUTA_ANIMAL}?pagina={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(q))
                url += $"&q={Uri.EscapeDataString(q)}";   // si la API implementa algo básico

            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                ViewBag.ApiError = $"GET {url} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Respuesta: {body}";
                ViewBag.Especies = new Dictionary<int, string>();
                ViewBag.Tamanos = new Dictionary<int, string>();
                ViewBag.Estados = new Dictionary<int, string>();

                ViewBag.Page = 1;
                ViewBag.PageSize = pageSize;
                ViewBag.HasMore = false;
                ViewBag.Query = q ?? "";

                return View(Enumerable.Empty<Animal>());
            }

            if (TempData["OkAnimal"] is string ok) ViewBag.Ok = ok;
            if (TempData["ErrorAnimal"] is string err) ViewBag.Error = err;

            var json = await resp.Content.ReadAsStringAsync();
            var animals = JsonSerializer.Deserialize<IEnumerable<Animal>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Animal>();

            // === 2) Catálogos para la vista (texto de especie/tamaño/estado) ===
            var tEsp = client.GetAsync("/api/Especie");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            await Task.WhenAll(tEsp, tTam, tEst);

            var especiesDict = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            var tamanosDict = await ToDict<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano);
            var estadosDict = await ToDict<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado);

            ViewBag.Especies = especiesDict;
            ViewBag.Tamanos = tamanosDict;
            ViewBag.Estados = estadosDict;

            // === 3) Filtro local POR TODO (id, nombre, especie, tamaño, estado) ===
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q; // tal cual, usamos Contains con OrdinalIgnoreCase

                animals = animals.Where(a =>
                {
                    bool porId = false;
                    if (int.TryParse(term, out int idBuscado))
                        porId = a.id_animal == idBuscado;

                    bool porNombre = !string.IsNullOrWhiteSpace(a.nombre) &&
                                     a.nombre.Contains(term, StringComparison.OrdinalIgnoreCase);

                    string espTxt = (a.id_especie != 0 && especiesDict.TryGetValue(a.id_especie, out var esp))
                        ? esp : "";
                    bool porEspecie = !string.IsNullOrWhiteSpace(espTxt) &&
                                      espTxt.Contains(term, StringComparison.OrdinalIgnoreCase);

                    string tamTxt = (a.id_tamano != 0 && tamanosDict.TryGetValue(a.id_tamano, out var tam))
                        ? tam : "";
                    bool porTamano = !string.IsNullOrWhiteSpace(tamTxt) &&
                                     tamTxt.Contains(term, StringComparison.OrdinalIgnoreCase);

                    string estTxt = (a.id_estadoAnimal != 0 && estadosDict.TryGetValue(a.id_estadoAnimal, out var est))
                        ? est : "";
                    bool porEstado = !string.IsNullOrWhiteSpace(estTxt) &&
                                     estTxt.Contains(term, StringComparison.OrdinalIgnoreCase);

                    return porId || porNombre || porEspecie || porTamano || porEstado;
                });
            }

            // === 4) HasMore por header o por sondeo ===
            int total = 0;
            bool hasHeader = resp.Headers.TryGetValues("X-Total-Count", out var vals);
            if (hasHeader) int.TryParse(vals!.FirstOrDefault(), out total);

            bool hasMore;
            if (total > 0)
            {
                hasMore = (page * pageSize) < total;
            }
            else
            {
                var probeUrl = $"{RUTA_ANIMAL}?pagina={page + 1}&pageSize=1";
                if (!string.IsNullOrWhiteSpace(q))
                    probeUrl += $"&q={Uri.EscapeDataString(q)}";

                var probe = await client.GetAsync(probeUrl);
                if (probe.IsSuccessStatusCode)
                {
                    var pj = await probe.Content.ReadAsStringAsync();
                    var next = JsonSerializer.Deserialize<IEnumerable<Animal>>(pj,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Animal>();
                    hasMore = next.Any();
                }
                else hasMore = false;
            }

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.HasMore = hasMore;
            ViewBag.Query = q ?? "";

            // Opcional: ordenar
            animals = animals.OrderByDescending(a => a.id_animal);

            return View(animals);
        }


        // === Acción AJAX para "Ver más" ===
        // Devuelve sólo las filas/cards (partial) y marca X-HasMore para que el JS sepa si ocultar el botón.
        [HttpGet]
        public async Task<IActionResult> Mas(int page = 2, int pageSize = 20, string? q = null)
        {
            var client = _http.CreateClient("Api");

            q = (q ?? "").Trim();

            var url = $"{RUTA_ANIMAL}?pagina={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(q))
                url += $"&q={Uri.EscapeDataString(q)}";

            var resp = await client.GetAsync(url);

            // Si falla la API → devolvemos 204 y HasMore=false para que el front corte prolijo
            if (!resp.IsSuccessStatusCode)
            {
                Response.Headers["X-HasMore"] = "false";
                return StatusCode(204); // No Content
            }

            var json = await resp.Content.ReadAsStringAsync();
            var animals = JsonSerializer.Deserialize<IEnumerable<Animal>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Animal>();

            // 🔍 Filtro local por q
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q;
                if (int.TryParse(term, out int idBuscado))
                {
                    animals = animals.Where(a => a.id_animal == idBuscado);
                }
                else
                {
                    animals = animals.Where(a =>
                        (!string.IsNullOrWhiteSpace(a.nombre) &&
                         a.nombre.Contains(term, StringComparison.OrdinalIgnoreCase))
                    );
                }
            }

            // Si después del filtro no queda nada → no hay más
            if (!animals.Any())
            {
                Response.Headers["X-HasMore"] = "false";
                return StatusCode(204); // No Content
            }

            // Catálogos (para que el partial pueda mostrar textos)
            var tEsp = client.GetAsync("/api/Especie");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            await Task.WhenAll(tEsp, tTam, tEst);

            ViewBag.Especies = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            ViewBag.Tamanos = await ToDict<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano);
            ViewBag.Estados = await ToDict<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado);

            // === HasMore igual que en Index ===
            int total = 0;
            bool hasHeader = resp.Headers.TryGetValues("X-Total-Count", out var vals);
            bool hasMore;

            if (hasHeader && int.TryParse(vals!.FirstOrDefault(), out total) && total > 0)
            {
                hasMore = (page * pageSize) < total;
            }
            else
            {
                var probeUrl = $"{RUTA_ANIMAL}?pagina={page + 1}&pageSize=1";
                if (!string.IsNullOrWhiteSpace(q))
                    probeUrl += $"&q={Uri.EscapeDataString(q)}";

                var probe = await client.GetAsync(probeUrl);
                if (probe.IsSuccessStatusCode)
                {
                    var pj = await probe.Content.ReadAsStringAsync();
                    var next = JsonSerializer.Deserialize<IEnumerable<Animal>>(pj,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Animal>();
                    hasMore = next.Any();
                }
                else hasMore = false;
            }

            Response.Headers["X-HasMore"] = hasMore ? "true" : "false";

            // Orden por prolijidad
            animals = animals.OrderByDescending(a => a.id_animal);

            // Partial con las filas o cards (lo que uses en el Index)
            return PartialView("_AnimalRows", animals);
        }


        // ===================== CREAR =====================

        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear()
        {
            await CargarSelects();

            var model = new Animal
            {
                // acá seteamos la fecha por defecto
                fechaIngreso = DateTime.Today
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Crear([FromForm] Animal model, IFormFile? imagenFile)
        {
            // 1) Tomamos el id_usuario desde las claims del usuario logueado
            var idUsuarioClaim = User.FindFirst("IdUsuario")
                               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

            int idUsuario = 0;
            if (idUsuarioClaim != null && int.TryParse(idUsuarioClaim.Value, out var parsed))
                idUsuario = parsed;

            // 2) Mantengo la idea de fallback a 1 si algo falla, como en Modificar
            ModelState.Remove(nameof(Animal.id_usuario));
            model.id_usuario = idUsuario > 0 ? idUsuario : 1;
            ModelState.Remove(nameof(Animal.imagen));
            ModelState.Remove(nameof(Animal.edadValor));
            ModelState.Remove(nameof(Animal.id_especie));
            ModelState.Remove(nameof(Animal.id_tamano));
            ModelState.Remove(nameof(Animal.id_estadoAnimal));
            // (opcionales: normalmente no hace falta mostrar error)
            ModelState.Remove(nameof(Animal.id_persona));
            ModelState.Remove(nameof(Animal.id_pension));

            if (string.IsNullOrWhiteSpace(model.nombre))
                ModelState.AddModelError(nameof(Animal.nombre), "El nombre es obligatorio.");
            if (model.edadValor <= 0)
                ModelState.AddModelError(nameof(Animal.edadValor), "La edad es obligatoria.");
            if (model.id_especie <= 0) ModelState.AddModelError(nameof(Animal.id_especie), "La especie es obligatorio.");
            if (model.id_tamano <= 0) ModelState.AddModelError(nameof(Animal.id_tamano), "Seleccione un tamaño válido.");
            if (model.id_estadoAnimal <= 0) ModelState.AddModelError(nameof(Animal.id_estadoAnimal), "Seleccione un estado válido.");

            if (model.id_persona.HasValue && model.id_persona <= 0) model.id_persona = null;
            if (model.id_pension.HasValue && model.id_pension <= 0) model.id_pension = null;

            // agregamos la validación de fecha futura
            if (model.fechaIngreso.HasValue && model.fechaIngreso.Value.Date > DateTime.Today)
                ModelState.AddModelError(nameof(Animal.fechaIngreso), "La fecha de ingreso no puede ser futura.");
            // ---- FECHA DE ADOPCIÓN ----
            if (model.fechaAdopcion.HasValue)
            {
                if (model.fechaAdopcion.Value.Date > DateTime.Today)
                    ModelState.AddModelError(nameof(Animal.fechaAdopcion), "La fecha de adopción no puede ser futura.");

                if (model.fechaIngreso.HasValue &&
                    model.fechaAdopcion.Value.Date < model.fechaIngreso.Value.Date)
                    ModelState.AddModelError(nameof(Animal.fechaAdopcion), "La fecha de adopción no puede ser anterior a la fecha de ingreso.");
            }

            if (imagenFile == null || imagenFile.Length == 0)
                ModelState.AddModelError(nameof(Animal.imagen), "La imagen es obligatoria.");

            // Si hay errores detiene el flujo
            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_especie, model.id_tamano, model.id_estadoAnimal);
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
                await CargarSelects(model.id_especie, model.id_tamano, model.id_estadoAnimal);
                return View(model);
            }

            ViewBag.Ok = "Animal creado correctamente.";
            ModelState.Clear();
            await CargarSelects();
            return View(new Animal());
        }

        // ===================== MODIFICAR =====================

        [HttpGet]
        [Authorize(Policy = "AdminOrColab")]
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

            await CargarSelects(model.id_especie, model.id_tamano, model.id_estadoAnimal);
            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrColab")]
        public async Task<IActionResult> Modificar([FromForm] Animal model, IFormFile? imagenFile)
        {
            // Tomamos el id_usuario desde las claims del usuario logueado
            var idUsuarioClaim = User.FindFirst("IdUsuario")
                               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

            int idUsuario = 0;
            if (idUsuarioClaim != null && int.TryParse(idUsuarioClaim.Value, out var parsed))
                idUsuario = parsed;

            // Mantengo la lógica anterior: si algo falla, sigo usando 1 como fallback
            ModelState.Remove(nameof(Animal.id_usuario));
            model.id_usuario = idUsuario > 0 ? idUsuario : 1;
            ModelState.Remove(nameof(Animal.imagen));
            ModelState.Remove(nameof(Animal.edadValor));
            ModelState.Remove(nameof(Animal.id_especie));
            ModelState.Remove(nameof(Animal.id_tamano));
            ModelState.Remove(nameof(Animal.id_estadoAnimal));
            // (opcionales: normalmente no hace falta mostrar error)
            ModelState.Remove(nameof(Animal.id_persona));
            ModelState.Remove(nameof(Animal.id_pension));


            if (string.IsNullOrWhiteSpace(model.nombre))
                ModelState.AddModelError(nameof(Animal.nombre), "El nombre es obligatorio.");
            if (model.edadValor <= 0)
                ModelState.AddModelError(nameof(Animal.edadValor), "La edad es obligatoria.");
            if (model.id_especie <= 0) ModelState.AddModelError(nameof(Animal.id_especie), "La especie es obligatoria.");
            if (model.id_tamano <= 0) ModelState.AddModelError(nameof(Animal.id_tamano), "Seleccione un tamaño válido.");
            if (model.id_estadoAnimal <= 0) ModelState.AddModelError(nameof(Animal.id_estadoAnimal), "Seleccione un estado válido.");
            // Nueva validación: no permitir fechas futuras
            if (model.fechaIngreso.HasValue && model.fechaIngreso.Value.Date > DateTime.Today)
                ModelState.AddModelError(nameof(Animal.fechaIngreso), "La fecha de ingreso no puede ser futura.");
            // ---- FECHA DE ADOPCIÓN ----
            if (model.fechaAdopcion.HasValue)
            {
                if (model.fechaAdopcion.Value.Date > DateTime.Today)
                    ModelState.AddModelError(nameof(Animal.fechaAdopcion), "La fecha de adopción no puede ser futura.");

                if (model.fechaIngreso.HasValue &&
                    model.fechaAdopcion.Value.Date < model.fechaIngreso.Value.Date)
                    ModelState.AddModelError(nameof(Animal.fechaAdopcion), "La fecha de adopción no puede ser anterior a la fecha de ingreso.");
            }

            // Si hay errores detiene el flujo
            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_especie, model.id_tamano, model.id_estadoAnimal);
                return View(model);
            }

            var client = _http.CreateClient("Api");

            // 1) Traer el actual para conservar su imagen si no se sube una nueva
            var respGet = await client.GetAsync($"/api/Animal/{model.id_animal}");
            if (!respGet.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"GET /api/Animal/{model.id_animal} -> {(int)respGet.StatusCode} {respGet.ReasonPhrase}";
                await CargarSelects(model.id_especie, model.id_tamano, model.id_estadoAnimal);
                return View(model);
            }

            var actualJson = await respGet.Content.ReadAsStringAsync();
            var actual = JsonSerializer.Deserialize<Animal>(actualJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (actual is null)
            {
                ViewBag.ApiError = "No se pudo deserializar el animal actual.";
                await CargarSelects(model.id_especie, model.id_tamano, model.id_estadoAnimal);
                return View(model);
            }

            //  si NO hay imagen en la BD y tampoco se sube una nueva → error
            if ((actual.imagen == null || actual.imagen.Length == 0) &&
                (imagenFile == null || imagenFile.Length == 0))
            {
                ModelState.AddModelError(nameof(Animal.imagen), "La imagen es obligatoria.");
            }

            // para volver a la vista de modificar
            if (!ModelState.IsValid)
            {
                await CargarSelects(model.id_especie, model.id_tamano, model.id_estadoAnimal);
                return View(model);   // Vuelve a la misma vista Modificar
            }

            // 2) Resolver imagen final
            byte[]? imagenFinal = actual.imagen; // conservar por defecto
            if (imagenFile is { Length: > 0 })
            {
                using var ms = new MemoryStream();
                await imagenFile.CopyToAsync(ms);
                imagenFinal = ms.ToArray();
            }

            // 3) Armar payload y PUT
            var payload = new
            {
                id_animal = model.id_animal,
                nombre = model.nombre,
                sexo = model.sexo,
                edadValor = model.edadValor,
                edadUnidad = model.edadUnidad,
                imagen = imagenFinal, // <- clave
                id_especie = model.id_especie,
                id_tamano = model.id_tamano,
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
                await CargarSelects(model.id_especie, model.id_tamano, model.id_estadoAnimal);
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

        // ===================== ELIMINAR =====================

        [HttpGet]
        [Authorize(Roles = "Administrador")]
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
            var model = JsonSerializer.Deserialize<Animal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Para mostrar nombres en lugar de IDs (como ya hacés en otros lados)
            await CargarDiccionariosBasicos();

            // Bloqueo visual si el animal está “en uso” (persona/pensión)
            if (model != null && ((model.id_persona.HasValue && model.id_persona.Value > 0) ||
                                  (model.id_pension.HasValue && model.id_pension.Value > 0)))
            {
                ViewBag.Bloqueado = true;
                ViewBag.Motivo = "tiene Persona o Pensión asociada";
            }

            if (TempData["Ok"] is string ok) ViewBag.Ok = ok;
            if (TempData["Error"] is string err) ViewBag.Error = err;

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("Eliminar")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            // Mismo patrón que el “EsAdministradorAsync” de usuarios,
            // pero para animales: si tiene vínculos, no se elimina.
            if (await AnimalEnUsoAsync(id))
            {
                TempData["Error"] = "No se puede eliminar el animal porque está en uso.";
                return RedirectToAction(nameof(Eliminar), new { id });
            }

            var client = _http.CreateClient("Api");

            var respDel = await client.DeleteAsync($"/api/Animal/{id}");
            if (!respDel.IsSuccessStatusCode)
            {
                var body = await respDel.Content.ReadAsStringAsync();

                // Mantengo el mismo manejo de códigos que en Usuario (conflict/badrequest/422)
                if (respDel.StatusCode == System.Net.HttpStatusCode.Conflict ||
                    respDel.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                    (int)respDel.StatusCode == 422)
                {
                    TempData["Error"] = "No se puede eliminar el animal porque está en uso.";
                    if (!string.IsNullOrWhiteSpace(body)) TempData["ApiDetail"] = body;
                    return RedirectToAction(nameof(Eliminar), new { id });
                }

                TempData["ErrorAnimal"] = $"DELETE /api/Animal/{id} -> {(int)respDel.StatusCode} {respDel.ReasonPhrase}. Respuesta: {body}";
                return RedirectToAction(nameof(Eliminar), new { id });
            }

            TempData["OkAnimal"] = "Animal eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===== Helper local (mismo espíritu que EsAdministradorAsync, sin clases nuevas)
        private async Task<bool> AnimalEnUsoAsync(int id)
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync($"/api/Animal/{id}");
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<Animal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return model != null &&
                   ((model.id_persona.HasValue && model.id_persona.Value > 0) ||
                    (model.id_pension.HasValue && model.id_pension.Value > 0));
        }


        // ===================== HELPERS =====================
        private async Task CargarSelects(int? espSel = null, int? razaSel = null, int? tamSel = null, int? estSel = null)
        {
            var client = _http.CreateClient("Api");

            var tEsp = client.GetAsync("/api/Especie");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            // NUEVO: Personas y Pensiones para selects
            var tPer = client.GetAsync("/api/Persona");
            var tPens = client.GetAsync("/api/Pension");

            await Task.WhenAll(tEsp, tTam, tEst, tPer, tPens);

            ViewBag.Especies = await ToSelectList<Especie>(tEsp.Result, x => x.id_especie, x => x.especie, espSel);
            ViewBag.Tamanos = await ToSelectList<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano, tamSel);
            ViewBag.Estados = await ToSelectList<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado, estSel);

            // Estos dos NO necesitan "selected" porque el Tag Helper selecciona por el valor del modelo
            ViewBag.Personas = await ToSelectList<Persona>(tPer.Result, x => x.id_persona, x => $"{x.apellido}, {x.nombre}");
            ViewBag.Pensiones = await ToSelectList<Pension>(tPens.Result, x => x.id_pension, x => x.nombre);
        }

        private async Task CargarDiccionariosBasicos()
        {
            var client = _http.CreateClient("Api");

            var tEsp = client.GetAsync("/api/Especie");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");

            // 👇 Agregamos las llamadas nuevas
            var tPer = client.GetAsync("/api/Persona");
            var tPen = client.GetAsync("/api/Pension");
            var tUsu = client.GetAsync("/api/Usuario");

            await Task.WhenAll(tEsp, tTam, tEst, tPer, tPen);

            ViewBag.Especies = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            ViewBag.Tamanos = await ToDict<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano);
            ViewBag.Estados = await ToDict<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado);

            // 👇 NUEVO: agregamos diccionarios para mostrar texto en lugar de IDs
            ViewBag.Personas = await ToDict<Persona>(
                tPer.Result,
                x => x.id_persona,
                x => $"{(x.nombre ?? "").Trim()} {(x.apellido ?? "").Trim()}".Trim()
            );

            // 👇 NUEVO: Diccionario de usuarios (id → "Nombre Apellido")
            ViewBag.Usuarios = await ToDict<Usuario>(
                tUsu.Result,
                x => x.id_usuario,
                x => $"{(x.nombre ?? "").Trim()} {(x.apellido ?? "").Trim()}".Trim()
            );

            ViewBag.Pensiones = await ToDict<Pension>(
                tPen.Result,
                x => x.id_pension,
                x => x.nombre
            );
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