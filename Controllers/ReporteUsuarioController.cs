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

        // ===================== Obtener usuarios =====================
        private async Task<List<Usuario>> ObtenerUsuariosAsync()
        {
            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/Usuario?pagina=1&pageSize=1000");

            if (!resp.IsSuccessStatusCode)
                return new();

            var json = await resp.Content.ReadAsStringAsync();
            var lista = JsonSerializer.Deserialize<List<Usuario>>(json, JOps) ?? new();

            return lista.OrderBy(u => u.id_usuario).ToList();
        }

        // ===================== Construir eventos =====================
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

                eventos.AddRange(personas
                    .Where(p => p.id_usuario == idUsuario)
                    .Select(p => new EventoUsuarioViewModel
                    {
                        Entidad = "Persona",
                        IdRegistro = p.id_persona,
                        NombreRegistro = $"{p.nombre} {p.apellido}",
                        Accion = "CREAR",
                        Fecha = p.fechaIngreso
                    }));

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

            return eventos.OrderByDescending(e => e.Fecha).ToList();
        }

        // ===================== INDEX =====================
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
            ViewBag.NombreUsuario = sel?.nombre ?? $"Usuario #{idUsuario.Value}";

            var eventos = await ConstruirEventosAsync(idUsuario.Value);

            // ⭐ Guardar para Excel
            TempData["AccionesUsuario"] = JsonSerializer.Serialize(eventos);
            TempData.Keep("AccionesUsuario");

            return View(eventos);
        }

        // ===================== EXPORTAR PDF =====================
        [HttpGet]
        public async Task<IActionResult> ExportarPdf(int idUsuario)
        {
            var eventos = await ConstruirEventosAsync(idUsuario);

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

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Reporte de auditoría por usuario")
                            .SemiBold().FontSize(16).FontColor("#2FA8A2");
                        col.Item().Text($"Usuario: {nombreUsuario}").FontSize(11);
                        col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.4f);
                            cols.RelativeColumn(2.0f);
                            cols.RelativeColumn(1.2f);
                        });

                        static IContainer Th(IContainer c) =>
                            c.Background("#2FA8A2").Padding(4).DefaultTextStyle(x => x.FontColor("#fff").Bold());

                        static IContainer Td(IContainer c) =>
                            c.Border(0.5f).BorderColor("#e5e7eb").Padding(3);

                        table.Header(h =>
                        {
                            h.Cell().Element(Th).Text("Acción");
                            h.Cell().Element(Th).Text("Entidad");
                            h.Cell().Element(Th).Text("Registro");
                            h.Cell().Element(Th).Text("Fecha");
                        });

                        foreach (var e in eventos)
                        {
                            table.Cell().Element(Td).Text(e.Accion);
                            table.Cell().Element(Td).Text(e.Entidad);
                            table.Cell().Element(Td).Text(e.NombreRegistro);
                            table.Cell().Element(Td).Text(e.Fecha.ToString("dd/MM/yyyy HH:mm"));
                        }
                    });
                });
            }).GeneratePdf(stream);

            return File(stream.ToArray(), "application/pdf",
                $"reporte_acciones_usuario_{idUsuario}_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // ===================== EXPORTAR EXCEL (CSV) =====================
        [HttpGet]
        public IActionResult ExportarExcel(int idUsuario, string nombreUsuario)
        {
            var accionesJson = TempData["AccionesUsuario"] as string;

            if (accionesJson == null)
                return Content("No hay datos para exportar.", "text/plain");

            var datos = JsonSerializer.Deserialize<List<EventoUsuarioViewModel>>(accionesJson, JOps)
                        ?? new();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("sep=;");
            sb.AppendLine("Fecha;Acción;Entidad;Registro");

            foreach (var e in datos)
            {
                sb.AppendLine($"{e.Fecha:dd/MM/yyyy HH:mm};{e.Accion};{e.Entidad};{e.NombreRegistro}");
            }

            var bytes = System.Text.Encoding.Unicode.GetBytes(sb.ToString());
            var nombreArchivo =
                $"reporte_acciones_{nombreUsuario}_{DateTime.Now:yyyyMMdd_HHmm}.csv";

            return File(bytes, "text/csv; charset=utf-16", nombreArchivo);
        }
    }
}
