using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace SantaRamona.Controllers
{
    [Route("admin/santa/back/[controller]/[action]")]
    [Authorize(Policy = "Activo")]
    public class ReportesController : Controller
    {
        private readonly IHttpClientFactory _http;
        private static readonly JsonSerializerOptions JOps = new() { PropertyNameCaseInsensitive = true };

        public ReportesController(IHttpClientFactory http) => _http = http;

        public async Task<IActionResult> Index()
        {
            var api = _http.CreateClient("Api");

            // Disparar en paralelo
            var tAnimales = api.GetAsync("/api/Animal");
            var tFormularios = api.GetAsync("/api/Formulario");
            var tPersonas = api.GetAsync("/api/Persona");
            var tPensiones = api.GetAsync("/api/Pension");

            await Task.WhenAll(tAnimales, tFormularios, tPersonas, tPensiones);

            // Leer contenido
            var animales = await DeserializeOrEmpty<Animal>(tAnimales.Result);
            var formularios = await DeserializeOrEmpty<Formulario>(tFormularios.Result);
            var personas = await DeserializeOrEmpty<Persona>(tPersonas.Result);
            var pensiones = await DeserializeOrEmpty<Pension>(tPensiones.Result);

            // ----- Totales de animales -----
            var totalAnimales = animales.Count;
            var totalPerros = animales.Count(a => a.id_especie == 1);
            var totalGatos = animales.Count(a => a.id_especie == 2);
            var totalOtros = animales.Count(a => a.id_especie > 2);

            // Estados (ajustá IDs si en tu API difieren)
            var totalEnTransito = animales.Count(a => a.id_estadoAnimal == 2);
            var totalEnPension = animales.Count(a => a.id_estadoAnimal == 3);
            var totalAdoptados = animales.Count(a => a.id_estadoAnimal == 4);
            var totalEnAdopcion = animales.Count(a => a.id_estadoAnimal == 1 || a.id_estadoAnimal == 2 || a.id_estadoAnimal == 3);

            // ----- Formularios -----
            var totalFormularios = formularios.Count;
            var totalAdopciones = formularios.Count(f => f.id_tipoFormulario == 1);
            var totalVoluntariados = formularios.Count(f => f.id_tipoFormulario == 2);
            var totalTransitos = formularios.Count(f => f.id_tipoFormulario == 3);

            // ----- Personas y Pensiones -----
            var totalPersonas = personas.Count;
            var totalPensiones = pensiones.Count;

            // ViewBags para tu vista
            ViewBag.TotalAnimales = totalAnimales;
            ViewBag.TotalPerros = totalPerros;
            ViewBag.TotalGatos = totalGatos;
            ViewBag.TotalOtros = totalOtros;

            ViewBag.TotalEnAdopcion = totalEnAdopcion;
            ViewBag.TotalEnTransito = totalEnTransito;
            ViewBag.TotalEnPension = totalEnPension;
            ViewBag.TotalAdoptados = totalAdoptados;

            ViewBag.TotalFormularios = totalFormularios;
            ViewBag.TotalAdopciones = totalAdopciones;
            ViewBag.TotalTransitos = totalTransitos;
            ViewBag.TotalVoluntariados = totalVoluntariados;

            ViewBag.TotalPersonas = totalPersonas;
            ViewBag.TotalPensiones = totalPensiones;

            return View();
        }

        // ===== Helpers =====
        private async Task<List<T>> DeserializeOrEmpty<T>(HttpResponseMessage resp)
        {
            if (resp is null || !resp.IsSuccessStatusCode) return new List<T>();
            var json = await resp.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<List<T>>(json, JOps);
            return data ?? new List<T>();
        }

        // ===== DTOs mínimos (solo campos necesarios para contar) =====
        private sealed class Animal
        {
            public int id_especie { get; set; }
            public int id_estadoAnimal { get; set; }
        }

        private sealed class Formulario
        {
            public int id_tipoFormulario { get; set; }
        }

        private sealed class Persona { public int id_persona { get; set; } }
        private sealed class Pension { public int id_pension { get; set; } }
    }
}
