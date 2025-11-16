using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SantaRamona.Backoffice.Models;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]")]
    [Authorize(Policy = "Activo")]
    public class ReporteAccionesUsuarioController : Controller
    {
        private readonly IHttpClientFactory _http;
        public ReporteAccionesUsuarioController(IHttpClientFactory http) => _http = http;

        private static readonly JsonSerializerOptions JOps = new()
        {
            PropertyNameCaseInsensitive = true
        };

        //// ===== Modelo simple de usuario para el combo =====
        //public class UsuarioSimple
        //{
        //    public int id_usuario { get; set; }
        //    public string usuario { get; set; } = "";   // ajustá si en tu API se llama distinto
        //}

        //// ===================== OBTENER USUARIOS =====================
        //private async Task<List<UsuarioSimple>> ObtenerUsuariosAsync()
        //{
        //    var client = _http.CreateClient("Api");
        //    var resp = await client.GetAsync("/api/Usuario");
        //    if (!resp.IsSuccessStatusCode)
        //        return new();

        //    var json = await resp.Content.ReadAsStringAsync();
        //    var lista = JsonSerializer.Deserialize<List<UsuarioSimple>>(json, JOps) ?? new();

        //    return lista
        //        .OrderBy(u => u.usuario)
        //        .ToList();
        //}
        private async Task<List<Usuario>> ObtenerUsuariosAsync()
        {
            var client = _http.CreateClient("Api");

            // 🔹 Usá el mismo endpoint que uses en tu pantalla de Usuarios
            // si allí llamás /api/Usuario?pagina=1&pageSize=20, copiá ese:
            var resp = await client.GetAsync("/api/Usuario?pagina=1&pageSize=1000");

            if (!resp.IsSuccessStatusCode)
            {
                // opcional: mostrar error en la vista
                ViewBag.ApiErrorUsuarios = $"GET /api/Usuario -> {(int)resp.StatusCode} {resp.ReasonPhrase}";
                return new();
            }

            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<List<Usuario>>(json, JOps) ?? new();

            // ajustá el campo por el que quieras ordenar (usuario, nombre, email, etc.)
            return lista
                .OrderBy(u => u.id_usuario)   // si tu modelo tiene otra propiedad, cámbiala acá
                .ToList();
        }

        // ===================== ARMAR EVENTOS POR USUARIO =====================
        private async Task<List<EventoUsuarioViewModel>> ConstruirEventosAsync(int idUsuario)
        {
            var client = _http.CreateClient("Api");
            var eventos = new List<EventoUsuarioViewModel>();

            // ========= PERSONAS =========
            var rPer = await client.GetAsync("/api/Persona?pagina=1&pageSize=1000");
            if (rPer.IsSuccessStatusCode)
            {
                var json = await rPer.Content.ReadAsStringAsync();
                var personas = JsonSerializer.Deserialize<IEnumerable<Persona>>(json, JOps) ?? Enumerable.Empty<Persona>();

                // CREAR (uso fechaIngreso como fecha de alta)
                eventos.AddRange(personas
                    .Where(p => p.id_usuario == idUsuario)
                    .Select(p => new EventoUsuarioViewModel
                    {
                        Entidad = "Persona",
                        IdRegistro = p.id_persona,
                        NombreRegistro = $"{p.nombre} {p.apellido}",
                        Accion = "CREAR",
                        Fecha = p.fechaIngreso      // DateTime normal
                    }));

                // MODIFICAR (si usan fechaEgreso como última acción / modificación)
                eventos.AddRange(personas
                    .Where(p => p.id_usuario == idUsuario && p.fechaEgreso.HasValue)
                    .Select(p => new EventoUsuarioViewModel
                    {
                        Entidad = "Persona",
                        IdRegistro = p.id_persona,
                        NombreRegistro = $"{p.nombre} {p.apellido}",
                        Accion = "MODIFICAR",
                        Fecha = p.fechaEgreso!.Value
                    }));
            }

            // ========= ANIMALES =========
            var rAni = await client.GetAsync("/api/Animal");
            if (rAni.IsSuccessStatusCode)
            {
                var json = await rAni.Content.ReadAsStringAsync();
                var animales = JsonSerializer.Deserialize<IEnumerable<Animal>>(json, JOps) ?? Enumerable.Empty<Animal>();

                // CREAR
                eventos.AddRange(animales
                    .Where(a => a.id_usuario == idUsuario && a.fechaIngreso.HasValue)
                    .Select(a => new EventoUsuarioViewModel
                    {
                        Entidad = "Animal",
                        IdRegistro = a.id_animal,
                        NombreRegistro = a.nombre,
                        Accion = "CREAR",
                        Fecha = a.fechaIngreso!.Value
                    }));

                // MODIFICAR
                eventos.AddRange(animales
                    .Where(a => a.id_usuario == idUsuario && a.fechaModificacion.HasValue)
                    .Select(a => new EventoUsuarioViewModel
                    {
                        Entidad = "Animal",
                        IdRegistro = a.id_animal,
                        NombreRegistro = a.nombre,
                        Accion = "MODIFICAR",
                        Fecha = a.fechaModificacion!.Value
                    }));
            }

            // ========= PENSIONES =========
            var rPen = await client.GetAsync("/api/Pension");
            if (rPen.IsSuccessStatusCode)
            {
                var json = await rPen.Content.ReadAsStringAsync();
                var pensiones = JsonSerializer.Deserialize<IEnumerable<Pension>>(json, JOps) ?? Enumerable.Empty<Pension>();

                // CREAR (uso fechaIngreso como fecha de alta)
                eventos.AddRange(pensiones
                    .Where(pe => pe.id_usuario == idUsuario)
                    .Select(pe => new EventoUsuarioViewModel
                    {
                        Entidad = "Pensión",
                        IdRegistro = pe.id_pension,
                        NombreRegistro = pe.nombre,
                        Accion = "CREAR",
                        Fecha = pe.fechaIngreso
                    }));

                // MODIFICAR (uso fechaEgreso como última acción)
                eventos.AddRange(pensiones
                    .Where(pe => pe.id_usuario == idUsuario && pe.fechaEgreso.HasValue)
                    .Select(pe => new EventoUsuarioViewModel
                    {
                        Entidad = "Pensión",
                        IdRegistro = pe.id_pension,
                        NombreRegistro = pe.nombre,
                        Accion = "MODIFICAR",
                        Fecha = pe.fechaEgreso!.Value
                    }));
            }

            return eventos
                .OrderByDescending(e => e.Fecha)
                .ToList();
        }

        // ===================== INDEX (FILTRO + LISTA) =====================
        [HttpGet]
        public async Task<IActionResult> Index(int? idUsuario)
        {
            var usuarios = await ObtenerUsuariosAsync();
            ViewBag.Usuarios = usuarios;

            if (!idUsuario.HasValue)
            {
                ViewBag.IdUsuario = null;
                ViewBag.NombreUsuario = "Seleccione un usuario";
                return View(new List<EventoUsuarioViewModel>());
            }

            var sel = usuarios.FirstOrDefault(u => u.id_usuario == idUsuario.Value);
            ViewBag.IdUsuario = idUsuario.Value;

            // 👇 CAMBIÁ "usuario" por la propiedad real de tu modelo Usuario
            ViewBag.NombreUsuario = sel != null
                ? sel.nombre                     // ej: sel.nombreUsuario, sel.email, etc.
                : $"Usuario #{idUsuario.Value}";

            var eventos = await ConstruirEventosAsync(idUsuario.Value);
            return View(eventos);
        }
        // ===================== PDF =====================
        [HttpGet]
        public async Task<IActionResult> ExportarPdf(int idUsuario)
        {
            var eventos = await ConstruirEventosAsync(idUsuario);

            // Nombre del usuario para el encabezado
            var usuarios = await ObtenerUsuariosAsync();
            var sel = usuarios.FirstOrDefault(u => u.id_usuario == idUsuario);
            var nombreUsuario = sel?.nombre ?? $"Usuario #{idUsuario}";

            using var stream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Reporte de auditoría por usuario")
                            .SemiBold().FontSize(16).FontColor("#2FA8A2");

                        col.Item().Text($"Usuario: {nombreUsuario}")
                            .FontSize(11);

                        col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy}")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.2f); // Acción
                            cols.RelativeColumn(1.6f); // Entidad
                            cols.RelativeColumn(1.0f); // Id
                            cols.RelativeColumn(1.6f); // Fecha
                        });

                        static IContainer CellHeader(IContainer c) =>
                            c.Background("#2FA8A2").Padding(4)
                             .DefaultTextStyle(x => x.FontColor("#FFFFFF").Bold());

                        static IContainer Cell(IContainer c) =>
                            c.Border(0.5f).BorderColor("#e5e7eb").Padding(3);

                        table.Header(h =>
                        {
                            h.Cell().Element(CellHeader).Text("Acción");
                            h.Cell().Element(CellHeader).Text("Entidad");
                            h.Cell().Element(CellHeader).Text("Identificación");
                            h.Cell().Element(CellHeader).Text("Fecha");
                        });

                        foreach (var e in eventos)
                        {
                            table.Cell().Element(Cell).Text(e.Accion);
                            table.Cell().Element(Cell).Text(e.Entidad);
                            table.Cell().Element(Cell).Text(e.NombreRegistro);
                            table.Cell().Element(Cell).Text(e.Fecha.ToString("dd/MM/yyyy"));
                        }
                    });

                    page.Footer().AlignRight().Text(x =>
                    {
                        x.Span("Página ");
                        x.CurrentPageNumber();
                        x.Span(" de ");
                        x.TotalPages();
                    });
                });
            }).GeneratePdf(stream);

            var bytes = stream.ToArray();
            var nombreArchivo = $"reporte_acciones_usuario_{idUsuario}_{DateTime.Now:yyyyMMdd}.pdf";
            return File(bytes, "application/pdf", nombreArchivo);
        }
    }
}
