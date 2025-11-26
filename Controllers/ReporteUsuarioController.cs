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
        private static List<EventoUsuarioViewModel> FiltrarPorFecha(
        List<EventoUsuarioViewModel> eventos,
        DateTime? fechaDesde,
        DateTime? fechaHasta)
        {
            var q = eventos.AsQueryable();

            if (fechaDesde.HasValue)
                q = q.Where(e => e.Fecha.Date >= fechaDesde.Value.Date);

            if (fechaHasta.HasValue)
                q = q.Where(e => e.Fecha.Date <= fechaHasta.Value.Date);

            return q
                .OrderBy(e => e.UsuarioNombre)
                .ThenByDescending(e => e.Fecha)
                .ToList();
        }


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

            // Traigo usuarios para obtener el nombre
            var usuarios = await ObtenerUsuariosAsync();
            var dicUsuarios = usuarios.ToDictionary(u => u.id_usuario, u => u.nombre);

            var nombreUsuario = dicUsuarios.TryGetValue(idUsuario, out var nom)
                ? nom
                : $"Usuario #{idUsuario}";

            // ========= PERSONAS =========
            var rPer = await client.GetAsync("/api/Persona/ConEliminadas");
            if (rPer.IsSuccessStatusCode)
            {
                var json = await rPer.Content.ReadAsStringAsync();
                var personas = JsonSerializer.Deserialize<IEnumerable<Persona>>(json, JOps)
                               ?? Enumerable.Empty<Persona>();

                // CREAR (ingreso)
                eventos.AddRange(personas
                    .Where(p => p.id_usuario == idUsuario)
                    .Select(p => new EventoUsuarioViewModel
                    {
                        IdUsuario = idUsuario,
                        UsuarioNombre = nombreUsuario,
                        Entidad = "Persona",
                        IdRegistro = p.id_persona,
                        NombreRegistro = $"{p.nombre} {p.apellido}",
                        Accion = "CREAR",
                        Fecha = p.fechaIngreso
                    }));

                // MODIFICAR (egreso)
                eventos.AddRange(personas
                    .Where(p => p.id_usuario == idUsuario && p.fechaEgreso.HasValue)
                    .Select(p => new EventoUsuarioViewModel
                    {
                        IdUsuario = idUsuario,
                        UsuarioNombre = nombreUsuario,
                        Entidad = "Persona",
                        IdRegistro = p.id_persona,
                        NombreRegistro = $"{p.nombre} {p.apellido}",
                        Accion = "MODIFICAR",
                        Fecha = p.fechaEgreso!.Value
                    }));

                // ELIMINAR 
                eventos.AddRange(personas
                    .Where(p => p.id_usuario == idUsuario && p.fechaEliminacion.HasValue)
                    .Select(p => new EventoUsuarioViewModel
                    {
                        IdUsuario = idUsuario,
                        UsuarioNombre = nombreUsuario,
                        Entidad = "Persona",
                        IdRegistro = p.id_persona,
                        NombreRegistro = $"{p.nombre} {p.apellido}",
                        Accion = "ELIMINAR",
                        Fecha = p.fechaEliminacion ?? p.fechaEgreso ?? p.fechaIngreso
                    }));
            }

            // ========= ANIMALES =========
            var rAni = await client.GetAsync("/api/Animal/ConEliminadas");
            if (rAni.IsSuccessStatusCode)
            {
                var json = await rAni.Content.ReadAsStringAsync();
                var animales = JsonSerializer.Deserialize<IEnumerable<Animal>>(json, JOps)
                               ?? Enumerable.Empty<Animal>();

                // CREAR (ingreso)
                eventos.AddRange(animales
                    .Where(a => a.id_usuario == idUsuario && a.fechaIngreso.HasValue)
                    .Select(a => new EventoUsuarioViewModel
                    {
                        IdUsuario = idUsuario,
                        UsuarioNombre = nombreUsuario,
                        Entidad = "Animal",
                        IdRegistro = a.id_animal,
                        NombreRegistro = a.nombre,
                        Accion = "CREAR",
                        Fecha = a.fechaIngreso!.Value
                    }));

                // MODIFICAR (modificación)
                eventos.AddRange(animales
                    .Where(a => a.id_usuario == idUsuario && a.fechaModificacion.HasValue)
                    .Select(a => new EventoUsuarioViewModel
                    {
                        IdUsuario = idUsuario,
                        UsuarioNombre = nombreUsuario,
                        Entidad = "Animal",
                        IdRegistro = a.id_animal,
                        NombreRegistro = a.nombre,
                        Accion = "MODIFICAR",
                        Fecha = a.fechaModificacion!.Value
                    }));

                //  ELIMINAR (solo del usuario seleccionado)
                eventos.AddRange(animales
                    .Where(a => a.id_usuario == idUsuario && a.fechaEliminacion.HasValue)
                    .Select(a => new EventoUsuarioViewModel
                    {
                        IdUsuario = idUsuario,
                        UsuarioNombre = nombreUsuario,
                        Entidad = "Animal",
                        IdRegistro = a.id_animal,
                        NombreRegistro = a.nombre,
                        Accion = "ELIMINAR",
                        Fecha = a.fechaEliminacion ?? a.fechaModificacion ?? a.fechaIngreso ?? DateTime.Now
                    }));
            }

            // ========= PENSIONES =========
            var rPen = await client.GetAsync("/api/Pension");
            if (rPen.IsSuccessStatusCode)
            {
                var json = await rPen.Content.ReadAsStringAsync();
                var pensiones = JsonSerializer.Deserialize<IEnumerable<Pension>>(json, JOps)
                                ?? Enumerable.Empty<Pension>();

                // CREAR (ingreso)
                eventos.AddRange(pensiones
                    .Where(pe => pe.id_usuario == idUsuario)
                    .Select(pe => new EventoUsuarioViewModel
                    {
                        IdUsuario = idUsuario,
                        UsuarioNombre = nombreUsuario,
                        Entidad = "Pensión",
                        IdRegistro = pe.id_pension,
                        NombreRegistro = pe.nombre,
                        Accion = "CREAR",
                        Fecha = pe.fechaIngreso
                    }));

                // MODIFICAR (egreso)
                eventos.AddRange(pensiones
                    .Where(pe => pe.id_usuario == idUsuario && pe.fechaEgreso.HasValue)
                    .Select(pe => new EventoUsuarioViewModel
                    {
                        IdUsuario = idUsuario,
                        UsuarioNombre = nombreUsuario,
                        Entidad = "Pensión",
                        IdRegistro = pe.id_pension,
                        NombreRegistro = pe.nombre,
                        Accion = "MODIFICAR",
                        Fecha = pe.fechaEgreso!.Value
                    }));

                // ELIMINAR (solo del usuario seleccionado)
                eventos.AddRange(pensiones
                    .Where(pe => pe.id_usuario == idUsuario && pe.fechaEliminacion.HasValue)
                    .Select(pe => new EventoUsuarioViewModel
                    {
                        IdUsuario = idUsuario,
                        UsuarioNombre = nombreUsuario,
                        Entidad = "Pensión",
                        IdRegistro = pe.id_pension,
                        NombreRegistro = pe.nombre,
                        Accion = "ELIMINAR",
                        Fecha = pe.fechaEliminacion ?? pe.fechaEgreso ?? pe.fechaIngreso
                    }));
            }

            return eventos
                .OrderByDescending(e => e.Fecha)
                .ToList();
        }

        // ===================== Construir eventos (TODOS los usuarios) =====================
        private async Task<List<EventoUsuarioViewModel>> ConstruirEventosTodosAsync()
        {
            var client = _http.CreateClient("Api");
            var eventos = new List<EventoUsuarioViewModel>();

            // Traigo usuarios para mapear Id -> Nombre (todos tienen id_usuario int)
            var usuarios = await ObtenerUsuariosAsync();
            var dicUsuarios = usuarios.ToDictionary(u => u.id_usuario, u => u.nombre);

            // ========= PERSONAS =========
            var rPer = await client.GetAsync("/api/Persona/ConEliminadas");
            if (rPer.IsSuccessStatusCode)
            {
                var json = await rPer.Content.ReadAsStringAsync();
                var personas = JsonSerializer.Deserialize<IEnumerable<Persona>>(json, JOps)
                               ?? Enumerable.Empty<Persona>();

                // CREAR (ingreso)
                eventos.AddRange(personas
                    .Where(p => p.id_usuario.HasValue && p.id_usuario.Value != 0)
                    .Select(p =>
                    {
                        var idUser = p.id_usuario!.Value;
                        var usuarioNombre = dicUsuarios.TryGetValue(idUser, out var nom)
                            ? nom
                            : $"Usuario #{idUser}";

                        return new EventoUsuarioViewModel
                        {
                            IdUsuario = idUser,
                            UsuarioNombre = usuarioNombre,
                            Entidad = "Persona",
                            IdRegistro = p.id_persona,
                            NombreRegistro = $"{p.nombre} {p.apellido}",
                            Accion = "CREAR",
                            Fecha = p.fechaIngreso
                        };
                    }));

                // MODIFICAR (egreso)
                eventos.AddRange(personas
                    .Where(p => p.id_usuario.HasValue && p.id_usuario.Value != 0 && p.fechaEgreso.HasValue)
                    .Select(p =>
                    {
                        var idUser = p.id_usuario!.Value;
                        var usuarioNombre = dicUsuarios.TryGetValue(idUser, out var nom)
                            ? nom
                            : $"Usuario #{idUser}";

                        return new EventoUsuarioViewModel
                        {
                            IdUsuario = idUser,
                            UsuarioNombre = usuarioNombre,
                            Entidad = "Persona",
                            IdRegistro = p.id_persona,
                            NombreRegistro = $"{p.nombre} {p.apellido}",
                            Accion = "MODIFICAR",
                            Fecha = p.fechaEgreso!.Value
                        };
                    }));
                
                // ELIMINAR
                eventos.AddRange(personas
                    .Where(p => p.id_usuario.HasValue && p.id_usuario.Value != 0 && p.fechaEliminacion.HasValue)
                    .Select(p =>
                    {
                        var idUser = p.id_usuario!.Value;
                        var usuarioNombre = dicUsuarios.TryGetValue(idUser, out var nom)
                            ? nom
                            : $"Usuario #{idUser}";

                        return new EventoUsuarioViewModel
                        {
                            IdUsuario = idUser,
                            UsuarioNombre = usuarioNombre,
                            Entidad = "Persona",
                            IdRegistro = p.id_persona,
                            NombreRegistro = $"{p.nombre} {p.apellido}",
                            Accion = "ELIMINAR",
                            // null-safe y coherente con las fechas
                            Fecha = p.fechaEliminacion ?? p.fechaEgreso ?? p.fechaIngreso
                        };
                    }));

            }

            // ========= ANIMALES =========
            var rAni = await client.GetAsync("/api/Animal/ConEliminadas");
            if (rAni.IsSuccessStatusCode)
            {
                var json = await rAni.Content.ReadAsStringAsync();
                var animales = JsonSerializer.Deserialize<IEnumerable<Animal>>(json, JOps)
                               ?? Enumerable.Empty<Animal>();

                // CREAR
                eventos.AddRange(animales
                    .Where(a => a.id_usuario != 0 && a.fechaIngreso.HasValue)
                    .Select(a =>
                    {
                        var idUser = a.id_usuario;
                        var usuarioNombre = dicUsuarios.TryGetValue(idUser, out var nom)
                            ? nom
                            : $"Usuario #{idUser}";

                        return new EventoUsuarioViewModel
                        {
                            IdUsuario = idUser,
                            UsuarioNombre = usuarioNombre,
                            Entidad = "Animal",
                            IdRegistro = a.id_animal,
                            NombreRegistro = a.nombre,
                            Accion = "CREAR",
                            Fecha = a.fechaIngreso!.Value
                        };
                    }));

                // MODIFICAR
                eventos.AddRange(animales
                    .Where(a => a.id_usuario != 0 && a.fechaModificacion.HasValue)
                    .Select(a =>
                    {
                        var idUser = a.id_usuario;
                        var usuarioNombre = dicUsuarios.TryGetValue(idUser, out var nom)
                            ? nom
                            : $"Usuario #{idUser}";

                        return new EventoUsuarioViewModel
                        {
                            IdUsuario = idUser,
                            UsuarioNombre = usuarioNombre,
                            Entidad = "Animal",
                            IdRegistro = a.id_animal,
                            NombreRegistro = a.nombre,
                            Accion = "MODIFICAR",
                            Fecha = a.fechaModificacion!.Value
                        };
                    }));

                // ELIMINAR
                eventos.AddRange(animales
                    .Where(a => a.id_usuario != 0 && a.fechaEliminacion.HasValue)
                    .Select(a =>
                    {
                        var idUser = a.id_usuario;
                        var usuarioNombre = dicUsuarios.TryGetValue(idUser, out var nom)
                            ? nom
                            : $"Usuario #{idUser}";

                        return new EventoUsuarioViewModel
                        {
                            IdUsuario = idUser,
                            UsuarioNombre = usuarioNombre,
                            Entidad = "Animal",
                            IdRegistro = a.id_animal,
                            NombreRegistro = a.nombre,
                            Accion = "ELIMINAR",
                            Fecha = a.fechaEliminacion ?? a.fechaModificacion ?? a.fechaIngreso ?? DateTime.Now
                        };
                    }));
            }


            // ========= PENSIONES =========
            var rPen = await client.GetAsync("/api/Pension");
            if (rPen.IsSuccessStatusCode)
            {
                var json = await rPen.Content.ReadAsStringAsync();
                var pensiones = JsonSerializer.Deserialize<IEnumerable<Pension>>(json, JOps)
                                ?? Enumerable.Empty<Pension>();

                // CREAR
                eventos.AddRange(pensiones
                    .Where(pe => pe.id_usuario != 0)
                    .Select(pe =>
                    {
                        var idUser = pe.id_usuario;   // int
                        var usuarioNombre = dicUsuarios.TryGetValue(idUser, out var nom)
                            ? nom
                            : $"Usuario #{idUser}";

                        return new EventoUsuarioViewModel
                        {
                            IdUsuario = idUser,
                            UsuarioNombre = usuarioNombre,
                            Entidad = "Pensión",
                            IdRegistro = pe.id_pension,
                            NombreRegistro = pe.nombre,
                            Accion = "CREAR",
                            Fecha = pe.fechaIngreso
                        };
                    }));

                // MODIFICAR (egreso)
                eventos.AddRange(pensiones
                    .Where(pe => pe.id_usuario != 0 && pe.fechaEgreso.HasValue)
                    .Select(pe =>
                    {
                        var idUser = pe.id_usuario;
                        var usuarioNombre = dicUsuarios.TryGetValue(idUser, out var nom)
                            ? nom
                            : $"Usuario #{idUser}";

                        return new EventoUsuarioViewModel
                        {
                            IdUsuario = idUser,
                            UsuarioNombre = usuarioNombre,
                            Entidad = "Pensión",
                            IdRegistro = pe.id_pension,
                            NombreRegistro = pe.nombre,
                            Accion = "MODIFICAR",
                            Fecha = pe.fechaEgreso!.Value
                        };
                    }));

                // ELIMINAR
                eventos.AddRange(pensiones
                    .Where(pe => pe.id_usuario != 0 && pe.fechaEliminacion.HasValue)
                    .Select(pe =>
                    {
                        var idUser = pe.id_usuario;
                        var usuarioNombre = dicUsuarios.TryGetValue(idUser, out var nom)
                            ? nom
                            : $"Usuario #{idUser}";

                        return new EventoUsuarioViewModel
                        {
                            IdUsuario = idUser,
                            UsuarioNombre = usuarioNombre,
                            Entidad = "Pensión",
                            IdRegistro = pe.id_pension,
                            NombreRegistro = pe.nombre,
                            Accion = "ELIMINAR",
                            Fecha = pe.fechaEliminacion!.Value
                        };
                        // ¿Hay auditoría manual? (se creó en EliminarConfirmado)
                        if (TempData.Peek("UltimaEliminacion") is string jsonEvt)
                        {
                            var evtMan = JsonSerializer.Deserialize<EventoUsuarioViewModel>(jsonEvt, JOps);
                            if (evtMan != null && evtMan.IdRegistro == pe.id_pension)
                            {
                                idUser = evtMan.IdUsuario;
                            }
                        }
                    }));
            }



            return eventos
                .OrderBy(e => e.UsuarioNombre)
                .ThenByDescending(e => e.Fecha)
                .ToList();
        }


        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index(int? idUsuario, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            var usuarios = await ObtenerUsuariosAsync();
            ViewBag.Usuarios = usuarios;

            List<EventoUsuarioViewModel> eventos;

            if (!idUsuario.HasValue)
            {
                // SIN usuario: traigo TODO
                ViewBag.IdUsuario = null;
                ViewBag.NombreUsuario = "Todos los usuarios";

                eventos = await ConstruirEventosTodosAsync();
            }
            else
            {
                // CON usuario seleccionado
                var sel = usuarios.FirstOrDefault(u => u.id_usuario == idUsuario.Value);
                ViewBag.IdUsuario = idUsuario.Value;
                ViewBag.NombreUsuario = sel?.nombre ?? $"Usuario #{idUsuario.Value}";

                eventos = await ConstruirEventosAsync(idUsuario.Value);
            }

            // === Filtro por fecha desde / hasta ===
            eventos = FiltrarPorFecha(eventos, fechaDesde, fechaHasta);

            // Para que los inputs type="date" sigan mostrando lo seleccionado
            ViewBag.FechaDesde = fechaDesde?.ToString("yyyy-MM-dd");
            ViewBag.FechaHasta = fechaHasta?.ToString("yyyy-MM-dd");

            //// Guardar lo YA filtrado para Excel
            //TempData["AccionesUsuario"] = JsonSerializer.Serialize(eventos);
            //TempData.Keep("AccionesUsuario");

            return View(eventos);
        }


        // ===================== EXPORTAR PDF =====================
        [HttpGet]
        public async Task<IActionResult> ExportarPdf(int? idUsuario, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            var usuarios = await ObtenerUsuariosAsync();
            List<EventoUsuarioViewModel> eventos;
            string tituloUsuario;

            if (idUsuario.HasValue)
            {
                eventos = await ConstruirEventosAsync(idUsuario.Value);
                var sel = usuarios.FirstOrDefault(u => u.id_usuario == idUsuario.Value);
                tituloUsuario = sel?.nombre ?? $"Usuario #{idUsuario.Value}";
            }
            else
            {
                eventos = await ConstruirEventosTodosAsync();
                tituloUsuario = "Todos los usuarios";
            }

            // Aplico filtro por fecha
            eventos = FiltrarPorFecha(eventos, fechaDesde, fechaHasta);

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
                        col.Item().Text($"Usuario: {tituloUsuario}").FontSize(11);
                        if (fechaDesde.HasValue || fechaHasta.HasValue)
                        {
                            var rango = $"{(fechaDesde.HasValue ? fechaDesde.Value.ToString("dd/MM/yyyy") : "...")} - {(fechaHasta.HasValue ? fechaHasta.Value.ToString("dd/MM/yyyy") : "...")}";
                            col.Item().Text($"Rango de fechas: {rango}")
                                .FontSize(10).FontColor(Colors.Grey.Darken2);
                        }
                        col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy}")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.3f); // Usuario
                            cols.RelativeColumn(1.0f); // Acción
                            cols.RelativeColumn(1.2f); // Entidad
                            cols.RelativeColumn(2.0f); // Registro
                            cols.RelativeColumn(1.0f); // Fecha
                        });

                        static IContainer Th(IContainer c) =>
                            c.Background("#2FA8A2").Padding(4).DefaultTextStyle(x => x.FontColor("#fff").Bold());

                        static IContainer Td(IContainer c) =>
                            c.Border(0.5f).BorderColor("#e5e7eb").Padding(3);

                        table.Header(h =>
                        {
                            h.Cell().Element(Th).Text("Usuario");
                            h.Cell().Element(Th).Text("Acción");
                            h.Cell().Element(Th).Text("Entidad");
                            h.Cell().Element(Th).Text("Registro");
                            h.Cell().Element(Th).Text("Fecha");
                        });

                        foreach (var e in eventos)
                        {
                            table.Cell().Element(Td).Text(e.UsuarioNombre);
                            table.Cell().Element(Td).Text(e.Accion);
                            table.Cell().Element(Td).Text(e.Entidad);
                            table.Cell().Element(Td).Text(e.NombreRegistro);
                            table.Cell().Element(Td).Text(e.Fecha.ToString("dd/MM/yyyy"));
                        }
                    });
                });
            }).GeneratePdf(stream);

            var nombreArchivo =
                $"reporte_acciones_usuario_{(idUsuario?.ToString() ?? "todos")}_{DateTime.Now:yyyyMMdd}.pdf";

            return File(stream.ToArray(), "application/pdf", nombreArchivo);
        }


        // ===================== EXPORTAR EXCEL (CSV) =====================
        [HttpGet]
        public async Task<IActionResult> ExportarExcel(int? idUsuario, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            // 1. Traer eventos frescos (igual que PDF)
            List<EventoUsuarioViewModel> eventos;
            var usuarios = await ObtenerUsuariosAsync();

            if (idUsuario.HasValue)
                eventos = await ConstruirEventosAsync(idUsuario.Value);
            else
                eventos = await ConstruirEventosTodosAsync();

            // 2. Aplicar filtro
            eventos = FiltrarPorFecha(eventos, fechaDesde, fechaHasta);

            // 3. Armar CSV
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("sep=;");
            sb.AppendLine("Usuario;Fecha;Acción;Entidad;Registro");

            foreach (var e in eventos)
            {
                sb.AppendLine($"{e.UsuarioNombre};{e.Fecha:dd/MM/yyyy};{e.Accion};{e.Entidad};{e.NombreRegistro}");
            }

            var bytes = System.Text.Encoding.Unicode.GetBytes(sb.ToString());
            var nombreArchivo =
                $"reporte_acciones_{(idUsuario?.ToString() ?? "todos")}_{DateTime.Now:yyyyMMdd}.csv";

            return File(bytes, "text/csv; charset=utf-16", nombreArchivo);
        }

    }
}
