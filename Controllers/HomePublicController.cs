using System.Diagnostics;
using FrontSantaRamona.AdopcionModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PruebaFront.Models;
using santa_ramona_BackOffice.Models;


namespace SantaRamona.BackOffice.Controllers
{
    public class HomePublicController : Controller
    {
        private readonly ILogger<HomePublicController> _logger;

        public HomePublicController(ILogger<HomePublicController> logger)
        {
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
        public IActionResult Adopcion()
        {
            return View();
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
