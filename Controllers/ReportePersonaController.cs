using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SantaRamona.Backoffice.Models;
using System.Text;
using System.Text.Json;

namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]")]
    [Authorize(Policy = "Activo")]
    public class ReportePersonaController : Controller
    {
        private readonly IHttpClientFactory _http;
        public ReportePersonaController(IHttpClientFactory http) => _http = http;

        private static readonly JsonSerializerOptions JOps = new() { PropertyNameCaseInsensitive = true };

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index(
            int? id_provincia,
            int? id_localidad,
            int? id_estadoPersona,
            int? id_tipoFormulario,
            DateTime? fechaIngresoDesde,
            DateTime? fechaIngresoHasta,
            bool incluirMotivoEgreso = false) // se mantiene el nombre del flag para no tocar la vista
        {
            await CargarSelects(id_provincia, id_localidad, id_estadoPersona, id_tipoFormulario);

            var client = _http.CreateClient("Api");

            // Personas
            var rPer = await client.GetAsync("/api/Persona");
            if (!rPer.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"GET /api/Persona -> {(int)rPer.StatusCode} {rPer.ReasonPhrase}";
                return View(Enumerable.Empty<Persona>());
            }

            var personas = JsonSerializer.Deserialize<IEnumerable<Persona>>(
                await rPer.Content.ReadAsStringAsync(), JOps) ?? Enumerable.Empty<Persona>();

            // Formularios (para filtrar por tipo y mostrar chip/CSV/PDF)
            var rForm = await client.GetAsync("/api/Formulario");
            var formularios = rForm.IsSuccessStatusCode
                ? JsonSerializer.Deserialize<IEnumerable<FormularioMin>>(await rForm.Content.ReadAsStringAsync(), JOps) ?? Enumerable.Empty<FormularioMin>()
                : Enumerable.Empty<FormularioMin>();

            // personaId -> tipos (texto) (también lo manda a la vista)
            var tiposDict = await CargarTiposDictAsync(client);
            var formTiposByPersona = formularios
                .GroupBy(f => f.id_persona)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(f => tiposDict.TryGetValue(f.id_tipoFormulario, out var nom) ? nom : $"Tipo {f.id_tipoFormulario}")
                          .Where(s => !string.IsNullOrWhiteSpace(s))
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .OrderBy(s => s)
                          .ToList()
                );
            ViewBag.FormTiposByPersona = formTiposByPersona;

            // --- Filtros en memoria ---
            var q = personas.AsQueryable();

            if (id_provincia.HasValue && id_provincia > 0) q = q.Where(p => p.id_provincia == id_provincia);
            if (id_localidad.HasValue && id_localidad > 0) q = q.Where(p => p.id_localidad == id_localidad);
            if (id_estadoPersona.HasValue && id_estadoPersona > 0) q = q.Where(p => p.id_estadoPersona == id_estadoPersona);

            if (id_tipoFormulario.HasValue && id_tipoFormulario > 0)
            {
                // personas que tengan al menos 1 formulario de ese tipo
                var personaIds = new HashSet<int>(
                    formularios.Where(f => f.id_tipoFormulario == id_tipoFormulario.Value)
                               .Select(f => f.id_persona)
                               .Where(pid => pid > 0));
                q = q.Where(p => personaIds.Contains(p.id_persona));
            }

            // ---- Filtros de fecha de ingreso ----
            if (fechaIngresoDesde.HasValue)
                q = q.Where(p => p.fechaIngreso.Date >= fechaIngresoDesde.Value.Date);

            if (fechaIngresoHasta.HasValue)
                q = q.Where(p => p.fechaIngreso.Date <= fechaIngresoHasta.Value.Date);

            var resultado = q
                .OrderBy(p => p.apellido)
                .ThenBy(p => p.nombre)
                .ToList();

            return View(resultado);
        }

        // ===================== CSV =====================
        [HttpGet]
        public async Task<IActionResult> ExportarCsv(
            int? id_provincia,
            int? id_localidad,
            int? id_estadoPersona,
            int? id_tipoFormulario,
            DateTime? fechaIngresoDesde,
            DateTime? fechaIngresoHasta,
            bool incluirMotivoEgreso = false)
        {
            var client = _http.CreateClient("Api");

            // Personas
            var rPer = await client.GetAsync("/api/Persona");
            if (!rPer.IsSuccessStatusCode)
                return Content("No se pudo obtener personas.", "text/plain");

            var personas = JsonSerializer.Deserialize<IEnumerable<Persona>>(
                await rPer.Content.ReadAsStringAsync(), JOps) ?? Enumerable.Empty<Persona>();

            // Catálogos
            var provincias = await ToDict<Provincia, int>(client, "/api/Provincia", x => x.id_provincia, x => x.nombre);
            var localidades = await ToDict<Localidad, int>(client, "/api/Localidad", x => x.id_localidad, x => x.nombre);
            var estados = await ToDict<Estado_Persona, int>(client, "/api/EstadoPersona", x => x.id_estadoPersona, x => x.descripcion);

            // Formularios y tipos
            var rForm = await client.GetAsync("/api/Formulario");
            var formularios = rForm.IsSuccessStatusCode
                ? JsonSerializer.Deserialize<IEnumerable<FormularioMin>>(await rForm.Content.ReadAsStringAsync(), JOps) ?? Enumerable.Empty<FormularioMin>()
                : Enumerable.Empty<FormularioMin>();

            var tiposDict = await CargarTiposDictAsync(client);
            var tiposByPersona = formularios
                .GroupBy(f => f.id_persona)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(f => tiposDict.TryGetValue(f.id_tipoFormulario, out var nom) ? nom : $"Tipo {f.id_tipoFormulario}")
                          .Where(s => !string.IsNullOrWhiteSpace(s))
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .OrderBy(s => s)
                          .ToList()
                );

            // Filtros
            var q = personas.AsQueryable();
            if (id_provincia > 0) q = q.Where(p => p.id_provincia == id_provincia);
            if (id_localidad > 0) q = q.Where(p => p.id_localidad == id_localidad);
            if (id_estadoPersona > 0) q = q.Where(p => p.id_estadoPersona == id_estadoPersona);
            if (id_tipoFormulario > 0)
            {
                var personaIds = new HashSet<int>(
                    formularios.Where(f => f.id_tipoFormulario == id_tipoFormulario)
                               .Select(f => f.id_persona)
                               .Where(pid => pid > 0));
                q = q.Where(p => personaIds.Contains(p.id_persona));
            }

            if (fechaIngresoDesde.HasValue)
                q = q.Where(p => p.fechaIngreso.Date >= fechaIngresoDesde.Value.Date);

            if (fechaIngresoHasta.HasValue)
                q = q.Where(p => p.fechaIngreso.Date <= fechaIngresoHasta.Value.Date);

            var lista = q.OrderBy(p => p.apellido).ThenBy(p => p.nombre).ToList();

            // CSV
            var sb = new StringBuilder();
            sb.AppendLine("sep=;");

            // Header SIN ID y SIN Egreso. “Observaciones” opcional.
            var header = "Apellido;Nombre;DNI;Provincia;Localidad;Estado;Ingreso;TiposFormulario";
            if (incluirMotivoEgreso)
                header += ";Observaciones";
            sb.AppendLine(header);

            foreach (var p in lista)
            {
                provincias.TryGetValue(p.id_provincia ?? 0, out var prov);
                localidades.TryGetValue(p.id_localidad ?? 0, out var loc);
                estados.TryGetValue(p.id_estadoPersona ?? 0, out var est);

                var tipos = tiposByPersona.TryGetValue(p.id_persona, out var lst) ? string.Join(", ", lst) : "";

                var cols = new List<string>
                {
                    Esc(p.apellido),
                    Esc(p.nombre),
                    p.dni.ToString(),
                    Esc(prov),
                    Esc(loc),
                    Esc(est),
                    p.fechaIngreso.ToString("yyyy-MM-dd"),
                    Esc(tipos)
                };

                if (incluirMotivoEgreso)
                    cols.Add(Esc(p.motivoEgreso)); // ahora se exporta como "Observaciones" en el header

                sb.AppendLine(string.Join(";", cols));
            }

            var bytes = Encoding.Unicode.GetBytes(sb.ToString());
            var nombreArchivo = $"reporte_personas_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            return File(bytes, "text/csv; charset=utf-16", nombreArchivo);
        }

        // ===================== PDF =====================
        [HttpGet]
        public async Task<IActionResult> ExportarPdf(
            int? id_provincia,
            int? id_localidad,
            int? id_estadoPersona,
            int? id_tipoFormulario,
            DateTime? fechaIngresoDesde,
            DateTime? fechaIngresoHasta,
            bool incluirMotivoEgreso = false)
        {
            var client = _http.CreateClient("Api");

            var rPer = await client.GetAsync("/api/Persona");
            if (!rPer.IsSuccessStatusCode)
                return Content("No se pudo obtener personas.", "text/plain");

            var personas = JsonSerializer.Deserialize<IEnumerable<Persona>>(
                await rPer.Content.ReadAsStringAsync(), JOps) ?? Enumerable.Empty<Persona>();

            var provincias = await ToDict<Provincia, int>(client, "/api/Provincia", x => x.id_provincia, x => x.nombre);
            var localidades = await ToDict<Localidad, int>(client, "/api/Localidad", x => x.id_localidad, x => x.nombre);
            var estados = await ToDict<Estado_Persona, int>(client, "/api/EstadoPersona", x => x.id_estadoPersona, x => x.descripcion);

            var rForm = await client.GetAsync("/api/Formulario");
            var formularios = rForm.IsSuccessStatusCode
                ? JsonSerializer.Deserialize<IEnumerable<FormularioMin>>(await rForm.Content.ReadAsStringAsync(), JOps) ?? Enumerable.Empty<FormularioMin>()
                : Enumerable.Empty<FormularioMin>();
            var tiposDict = await CargarTiposDictAsync(client);
            var tiposByPersona = formularios
                .GroupBy(f => f.id_persona)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(f => tiposDict.TryGetValue(f.id_tipoFormulario, out var nom) ? nom : $"Tipo {f.id_tipoFormulario}")
                          .Where(s => !string.IsNullOrWhiteSpace(s))
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .OrderBy(s => s)
                          .ToList()
                );

            // Filtros
            var q = personas.AsQueryable();
            if (id_provincia > 0) q = q.Where(p => p.id_provincia == id_provincia);
            if (id_localidad > 0) q = q.Where(p => p.id_localidad == id_localidad);
            if (id_estadoPersona > 0) q = q.Where(p => p.id_estadoPersona == id_estadoPersona);
            if (id_tipoFormulario > 0)
            {
                var personaIds = new HashSet<int>(
                    formularios.Where(f => f.id_tipoFormulario == id_tipoFormulario)
                               .Select(f => f.id_persona)
                               .Where(pid => pid > 0));
                q = q.Where(p => personaIds.Contains(p.id_persona));
            }

            if (fechaIngresoDesde.HasValue)
                q = q.Where(p => p.fechaIngreso.Date >= fechaIngresoDesde.Value.Date);

            if (fechaIngresoHasta.HasValue)
                q = q.Where(p => p.fechaIngreso.Date <= fechaIngresoHasta.Value.Date);

            var lista = q.OrderBy(p => p.apellido).ThenBy(p => p.nombre).ToList();

            // PDF
            using var stream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Row(r =>
                    {
                        r.RelativeItem().Text("Reporte Personas").SemiBold().FontSize(16);
                        r.ConstantItem(120).AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            // SIN ID y SIN Egreso
                            cols.RelativeColumn(1.6f); // Apellido
                            cols.RelativeColumn(1.6f); // Nombre
                            cols.RelativeColumn(1.2f); // DNI
                            cols.RelativeColumn(1.8f); // Provincia
                            cols.RelativeColumn(1.8f); // Localidad
                            cols.RelativeColumn(1.4f); // Estado
                            cols.RelativeColumn(1.2f); // Ingreso
                            cols.RelativeColumn(2.0f); // Formularios
                            if (incluirMotivoEgreso)
                                cols.RelativeColumn(2.0f); // Observaciones (opcional)
                            
                        });

                        static IContainer CellHeader(IContainer c) =>
                            c.Background("#2FA8A2").Padding(4).DefaultTextStyle(x => x.FontColor("#FFFFFF").Bold());

                        static IContainer Cell(IContainer c) =>
                            c.Border(0.5f).BorderColor("#e5e7eb").Padding(3);

                        table.Header(h =>
                        {
                            // SIN ID y SIN Egreso
                            h.Cell().Element(CellHeader).Text("Apellido");
                            h.Cell().Element(CellHeader).Text("Nombre");
                            h.Cell().Element(CellHeader).Text("DNI");
                            h.Cell().Element(CellHeader).Text("Provincia");
                            h.Cell().Element(CellHeader).Text("Localidad");
                            h.Cell().Element(CellHeader).Text("Estado");
                            h.Cell().Element(CellHeader).Text("Ingreso");
                            h.Cell().Element(CellHeader).Text("Formularios");
                            if (incluirMotivoEgreso)
                                h.Cell().Element(CellHeader).Text("Observaciones");
                            
                        });

                        foreach (var p in lista)
                        {
                            provincias.TryGetValue(p.id_provincia ?? 0, out var prov);
                            localidades.TryGetValue(p.id_localidad ?? 0, out var loc);
                            estados.TryGetValue(p.id_estadoPersona ?? 0, out var est);
                            var tipos = tiposByPersona.TryGetValue(p.id_persona, out var lst) ? string.Join(", ", lst) : "";

                            // SIN ID y SIN Egreso
                            table.Cell().Element(Cell).Text(p.apellido);
                            table.Cell().Element(Cell).Text(p.nombre);
                            table.Cell().Element(Cell).Text(p.dni);
                            table.Cell().Element(Cell).Text(prov ?? $"#{p.id_provincia}");
                            table.Cell().Element(Cell).Text(loc ?? $"#{p.id_localidad}");
                            table.Cell().Element(Cell).Text(est ?? $"#{p.id_estadoPersona}");
                            table.Cell().Element(Cell).Text(p.fechaIngreso.ToString("dd/MM/yyyy"));
                            if (incluirMotivoEgreso)
                                table.Cell().Element(Cell).Text(string.IsNullOrWhiteSpace(p.motivoEgreso) ? "—" : p.motivoEgreso);
                            table.Cell().Element(Cell).Text(tipos);
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
            var nombreArchivo = $"reporte_personas_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return File(bytes, "application/pdf", nombreArchivo);
        }

        // ===================== Helpers =====================
        private static string Esc(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var t = s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            if (t.Contains(';') || t.Contains('"')) return "\"" + t.Replace("\"", "\"\"") + "\"";
            return t;
        }

        private async Task CargarSelects(int? provSel, int? locSel, int? estSel, int? tipoSel)
        {
            var client = _http.CreateClient("Api");

            var tProv = client.GetAsync("/api/Provincia");
            var tLoc = client.GetAsync("/api/Localidad");
            var tEst = client.GetAsync("/api/EstadoPersona");
            var tTipo = client.GetAsync("/api/TipoFormulario");
            await Task.WhenAll(tProv, tLoc, tEst, tTipo);

            ViewBag.Provincias = await ToSelectList(tProv.Result, (Provincia x) => x.id_provincia, x => x.nombre, provSel);
            ViewBag.Localidades = await ToSelectList(tLoc.Result, (Localidad x) => x.id_localidad, x => x.nombre, locSel);
            ViewBag.Estados = await ToSelectList(tEst.Result, (Estado_Persona x) => x.id_estadoPersona, x => x.descripcion, estSel);
            ViewBag.TiposFormulario = await ToSelectList(tTipo.Result, (TipoFormulario x) => x.id_tipoFormulario, x => x.nombre ?? x.tipo ?? $"Tipo {x.id_tipoFormulario}", tipoSel);
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
            var list = JsonSerializer.Deserialize<IEnumerable<T>>(json, JOps) ?? Enumerable.Empty<T>();

            items.AddRange(list.Select(x => new SelectListItem
            {
                Value = keySel(x).ToString(),
                Text = textSel(x),
                Selected = selected.HasValue && keySel(x) == selected.Value
            }));

            return items;
        }

        private static async Task<Dictionary<int, string>> ToDict<T, TKey>(
            HttpClient client, string url,
            Func<T, TKey> keySel, Func<T, string> valSel)
            where TKey : notnull
        {
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return new();

            var json = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<IEnumerable<T>>(json, JOps) ?? Enumerable.Empty<T>();
            return list.GroupBy(keySel).ToDictionary(g => Convert.ToInt32(g.Key), g => valSel(g.First()));
        }

        private async Task<Dictionary<int, string>> CargarTiposDictAsync(HttpClient client)
        {
            var dict = new Dictionary<int, string>();
            var resp = await client.GetAsync("/api/TipoFormulario");
            if (!resp.IsSuccessStatusCode) return dict;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return dict;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (!el.TryGetProperty("id_tipoFormulario", out var idProp) || idProp.ValueKind != JsonValueKind.Number) continue;
                var id = idProp.GetInt32();
                string nombre =
                    el.TryGetProperty("nombre", out var n) ? (n.GetString() ?? "") :
                    el.TryGetProperty("tipo", out var t) ? (t.GetString() ?? "") :
                    $"Tipo {id}";
                dict[id] = string.IsNullOrWhiteSpace(nombre) ? $"Tipo {id}" : nombre;
            }
            return dict;
        }

        // DTOs mínimos
        private class FormularioMin
        {
            public int id_formulario { get; set; }
            public int id_persona { get; set; }
            public int id_tipoFormulario { get; set; }
            public DateTime? fechaEnvio { get; set; }
            public string? estado { get; set; }
        }

        private class Provincia { public int id_provincia { get; set; } public string nombre { get; set; } = ""; }
        private class Localidad { public int id_localidad { get; set; } public int id_provincia { get; set; } public string nombre { get; set; } = ""; }
        private class Estado_Persona { public int id_estadoPersona { get; set; } public string descripcion { get; set; } = ""; }
        private class TipoFormulario { public int id_tipoFormulario { get; set; } public string? nombre { get; set; } public string? tipo { get; set; } }
    }
}
