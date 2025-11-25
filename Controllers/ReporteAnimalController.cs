using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuestPDF.Fluent;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SantaRamona.Backoffice.Models;
using System.Reflection.PortableExecutable;
using System.Text.Json;


namespace SantaRamona.Backoffice.Controllers
{
    [Route("admin/santa/back/[controller]/[action]")]
    [Authorize(Policy = "Activo")]
    public class ReporteAnimalController : Controller
    {
        private readonly IHttpClientFactory _http;
        public ReporteAnimalController(IHttpClientFactory http) => _http = http;

        // ====== Filtros (GET con querystring) ======
        [HttpGet]
        public async Task<IActionResult> Index(
            int? id_especie,
            int? id_tamano,
            int? id_estadoAnimal,
            int? id_persona,
            int? id_pension,
            DateTime? fechaIngresoDesde,
            DateTime? fechaIngresoHasta,
            DateTime? fechaAdopcionDesde,
            DateTime? fechaAdopcionHasta)
        {
            await CargarSelects(id_especie, id_tamano, id_estadoAnimal, id_persona, id_pension);

            var client = _http.CreateClient("Api");
            var resp = await client.GetAsync("/api/Animal");
            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"GET /api/Animal -> {(int)resp.StatusCode} {resp.ReasonPhrase}";
                return View((IEnumerable<Animal>)Enumerable.Empty<Animal>());
            }

            var json = await resp.Content.ReadAsStringAsync();
            var todos = JsonSerializer.Deserialize<IEnumerable<Animal>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? Enumerable.Empty<Animal>();

            //  NO mostrar animales eliminados en el reporte
            todos = todos.Where(a => a.fechaEliminacion == null);

            // Filtros en memoria (sin tocar API)
            var q = todos.AsQueryable();

            if (id_especie.HasValue && id_especie > 0) q = q.Where(a => a.id_especie == id_especie);
            if (id_tamano.HasValue && id_tamano > 0) q = q.Where(a => a.id_tamano == id_tamano);
            if (id_estadoAnimal.HasValue && id_estadoAnimal > 0) q = q.Where(a => a.id_estadoAnimal == id_estadoAnimal);
            if (id_persona.HasValue && id_persona > 0) q = q.Where(a => a.id_persona == id_persona);
            if (id_pension.HasValue && id_pension > 0) q = q.Where(a => a.id_pension == id_pension);

            if (fechaIngresoDesde.HasValue) q = q.Where(a => a.fechaIngreso.HasValue && a.fechaIngreso.Value.Date >= fechaIngresoDesde.Value.Date);
            if (fechaIngresoHasta.HasValue) q = q.Where(a => a.fechaIngreso.HasValue && a.fechaIngreso.Value.Date <= fechaIngresoHasta.Value.Date);
            if (fechaAdopcionDesde.HasValue) q = q.Where(a => a.fechaAdopcion.HasValue && a.fechaAdopcion.Value.Date >= fechaAdopcionDesde.Value.Date);
            if (fechaAdopcionHasta.HasValue) q = q.Where(a => a.fechaAdopcion.HasValue && a.fechaAdopcion.Value.Date <= fechaAdopcionHasta.Value.Date);

            var resultado = q
                .OrderBy(a => a.id_animal)
                .ToList();

            return View(resultado);
        }

        // ====== Exportación a CSV (Excel-friendly) ======
        [HttpGet]
        public async Task<IActionResult> ExportarCsv(
           int? id_especie, int? id_tamano,
           int? id_estadoAnimal,
           int? id_persona,
           int? id_pension,
           DateTime? fechaIngresoDesde, DateTime? fechaIngresoHasta,
           DateTime? fechaAdopcionDesde, DateTime? fechaAdopcionHasta,
           bool incluirHistoria = false,
           bool incluirSeguimiento = false)
        {
            var client = _http.CreateClient("Api");

            // 1) Animales
            var resp = await client.GetAsync("/api/Animal");
            if (!resp.IsSuccessStatusCode)
                return Content("No se pudo obtener animales.", "text/plain");

            var json = await resp.Content.ReadAsStringAsync();
            var todos = JsonSerializer.Deserialize<IEnumerable<Animal>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Animal>();

            // NO mostrar animales eliminados en el reporte
            todos = todos.Where(a => a.fechaEliminacion == null);


            // 2) Filtros
            var q = todos.AsQueryable();
            if (id_especie > 0) q = q.Where(a => a.id_especie == id_especie);
            if (id_tamano > 0) q = q.Where(a => a.id_tamano == id_tamano);
            if (id_estadoAnimal > 0) q = q.Where(a => a.id_estadoAnimal == id_estadoAnimal);
            if (id_persona > 0) q = q.Where(a => a.id_persona == id_persona);
            if (id_pension > 0) q = q.Where(a => a.id_pension == id_pension);

            if (fechaIngresoDesde.HasValue) q = q.Where(a => a.fechaIngreso.HasValue && a.fechaIngreso.Value.Date >= fechaIngresoDesde.Value.Date);
            if (fechaIngresoHasta.HasValue) q = q.Where(a => a.fechaIngreso.HasValue && a.fechaIngreso.Value.Date <= fechaIngresoHasta.Value.Date);
            if (fechaAdopcionDesde.HasValue) q = q.Where(a => a.fechaAdopcion.HasValue && a.fechaAdopcion.Value.Date >= fechaAdopcionDesde.Value.Date);
            if (fechaAdopcionHasta.HasValue) q = q.Where(a => a.fechaAdopcion.HasValue && a.fechaAdopcion.Value.Date <= fechaAdopcionHasta.Value.Date);

            var lista = q.OrderBy(a => a.id_animal).ToList();

            // 3) Catálogos para mostrar textos
            var tEsp = client.GetAsync("/api/Especie");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            var tPer = client.GetAsync("/api/Persona");
            var tPen = client.GetAsync("/api/Pension");
            await Task.WhenAll(tEsp, tTam, tEst, tPer, tPen);

            var dEsp = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            var dTam = await ToDict<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano);
            var dEst = await ToDict<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado);
            var dPer = await ToDict<Persona>(tPer.Result, x => x.id_persona, x => $"{x.apellido?.Trim()} {x.nombre?.Trim()}".Trim());
            var dPen = await ToDict<Pension>(tPen.Result, x => x.id_pension, x => x.nombre);

            string SexoTxt(string? s) => s == "M" ? "Macho" : s == "H" ? "Hembra" : "";
            string UnidadEdadTxt(string? u) => u == "A" ? "Años" : u == "M" ? "Meses" : "";

            string Esc(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return "";
                var t = s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                if (t.Contains(';') || t.Contains('"'))
                    return "\"" + t.Replace("\"", "\"\"") + "\"";
                return t;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("sep=;"); // ayuda a Excel a usar ';'

            // ENCABEZADOS (condicionales para Historia / Seguimiento)
            var headers = new List<string>
    {
        "Nombre","Sexo","EdadValor","EdadUnidad",
        "Especie","Tamano","Estado","Persona","Pension",
        "FechaIngreso","FechaAdopcion"
    };
            if (incluirHistoria) headers.Add("Historia");
            if (incluirSeguimiento) headers.Add("Seguimiento");

            sb.AppendLine(string.Join(";", headers));

            // FILAS
            foreach (var a in lista)
            {
                var especieTxt = dEsp.TryGetValue(a.id_especie, out var _esp) ? _esp : $"#{a.id_especie}";
                var tamanoTxt = dTam.TryGetValue(a.id_tamano, out var _tam) ? _tam : $"#{a.id_tamano}";
                var estadoTxt = dEst.TryGetValue(a.id_estadoAnimal, out var _est) ? _est : $"#{a.id_estadoAnimal}";
                var personaTxt = a.id_persona.HasValue && dPer.TryGetValue(a.id_persona.Value, out var _per) ? _per : "";
                var pensionTxt = a.id_pension.HasValue && dPen.TryGetValue(a.id_pension.Value, out var _p) ? _p : "";
                var fi = a.fechaIngreso?.ToString("yyyy-MM-dd") ?? "";
                var fa = a.fechaAdopcion?.ToString("yyyy-MM-dd") ?? "";

                var cols = new List<string>
        {

            Esc(a.nombre),
            SexoTxt(a.sexo),
            a.edadValor.ToString(),
            UnidadEdadTxt(a.edadUnidad),
            Esc(especieTxt),
            Esc(tamanoTxt),
            Esc(estadoTxt),
            Esc(personaTxt),
            Esc(pensionTxt),
            fi,
            fa
        };

                if (incluirHistoria) cols.Add(Esc(a.historia));
                if (incluirSeguimiento) cols.Add(Esc(a.seguimiento));

                sb.AppendLine(string.Join(";", cols));
            }

            // UTF-16 LE para ñ/acentos en Excel
            var bytes = System.Text.Encoding.Unicode.GetBytes(sb.ToString());
            var nombreArchivo = $"reporte_animales_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            return File(bytes, "text/csv; charset=utf-16", nombreArchivo);
        }

        //---- PDF----//

        [HttpGet]
        public async Task<IActionResult> ExportarPdf(
            int? id_especie,
            int? id_tamano,
            int? id_estadoAnimal,
            int? id_persona,
            int? id_pension,
            DateTime? fechaIngresoDesde, DateTime? fechaIngresoHasta,
            DateTime? fechaAdopcionDesde, DateTime? fechaAdopcionHasta,
            bool incluirHistoria = false,
            bool incluirSeguimiento = false)

        {
            var client = _http.CreateClient("Api");

            // traer animales
            var resp = await client.GetAsync("/api/Animal");
            if (!resp.IsSuccessStatusCode)
                return Content("No se pudo obtener animales.", "text/plain");

            var json = await resp.Content.ReadAsStringAsync();
            var todos = JsonSerializer.Deserialize<IEnumerable<Animal>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Animal>();

            // NO mostrar animales eliminados en el reporte
            todos = todos.Where(a => a.fechaEliminacion == null);


            // filtros
            var q = todos.AsQueryable();
            if (id_especie.HasValue && id_especie > 0) q = q.Where(a => a.id_especie == id_especie);
            if (id_tamano.HasValue && id_tamano > 0) q = q.Where(a => a.id_tamano == id_tamano);
            if (id_estadoAnimal.HasValue && id_estadoAnimal > 0) q = q.Where(a => a.id_estadoAnimal == id_estadoAnimal);
            if (id_persona.HasValue && id_persona > 0) q = q.Where(a => a.id_persona == id_persona);
            if (id_pension.HasValue && id_pension > 0) q = q.Where(a => a.id_pension == id_pension);
            if (fechaIngresoDesde.HasValue) q = q.Where(a => a.fechaIngreso.HasValue && a.fechaIngreso.Value.Date >= fechaIngresoDesde.Value.Date);
            if (fechaIngresoHasta.HasValue) q = q.Where(a => a.fechaIngreso.HasValue && a.fechaIngreso.Value.Date <= fechaIngresoHasta.Value.Date);
            if (fechaAdopcionDesde.HasValue) q = q.Where(a => a.fechaAdopcion.HasValue && a.fechaAdopcion.Value.Date >= fechaAdopcionDesde.Value.Date);
            if (fechaAdopcionHasta.HasValue) q = q.Where(a => a.fechaAdopcion.HasValue && a.fechaAdopcion.Value.Date <= fechaAdopcionHasta.Value.Date);

            var lista = q.OrderBy(a => a.id_animal).ToList();

            // catálogos
            var tEsp = client.GetAsync("/api/Especie");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            var tPer = client.GetAsync("/api/Persona");
            var tPen = client.GetAsync("/api/Pension");
            await Task.WhenAll(tEsp, tTam, tEst, tPer, tPen);

            var dEsp = await ToDict<Especie>(tEsp.Result, x => x.id_especie, x => x.especie);
            var dTam = await ToDict<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano);
            var dEst = await ToDict<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado);
            var dPer = await ToDict<Persona>(tPer.Result, x => x.id_persona, x => $"{x.apellido?.Trim()} {x.nombre?.Trim()}".Trim());
            var dPen = await ToDict<Pension>(tPen.Result, x => x.id_pension, x => x.nombre);

            string SexoTxt(string? s) => s == "M" ? "Macho" : s == "H" ? "Hembra" : "";
            string UnidadEdadTxt(string? u) => u == "A" ? "Años" : u == "M" ? "Meses" : "";

            // helpers para celdas (¡OJO! usan Infrastructure.IContainer)
            static IContainer CellHeader(IContainer c) =>
                c.Background("#2FA8A2").Padding(4).DefaultTextStyle(x => x.FontColor("#FFFFFF").Bold());

            static IContainer Cell(IContainer c) =>
                c.Border(0.5f).BorderColor("#e5e7eb").Padding(3);

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
                        r.RelativeItem().Text("Reporte Animales").SemiBold().FontSize(16);
                        r.ConstantItem(120).AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(0.7f);  // ID
                            cols.RelativeColumn(1.6f);  // Nombre
                            cols.RelativeColumn(1.0f);  // Sexo
                            cols.RelativeColumn(0.9f);  // Edad
                            cols.RelativeColumn(1.2f);  // Especie
                            cols.RelativeColumn(1.2f);  // Tamaño
                            cols.RelativeColumn(1.2f);  // Estado
                            cols.RelativeColumn(1.8f);  // Persona
                            cols.RelativeColumn(1.4f);  // Pensión
                            cols.RelativeColumn(1.2f);  // Ingreso
                            cols.RelativeColumn(1.2f);  // Adopción

                            if (incluirHistoria)
                                cols.RelativeColumn(2.0f); // Historia

                            if (incluirSeguimiento)
                                cols.RelativeColumn(2.0f); // Seguimiento

                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(CellHeader).Text("ID");
                            h.Cell().Element(CellHeader).Text("Nombre");
                            h.Cell().Element(CellHeader).Text("Sexo");
                            h.Cell().Element(CellHeader).Text("Edad");
                            h.Cell().Element(CellHeader).Text("Especie");
                            h.Cell().Element(CellHeader).Text("Tamaño");
                            h.Cell().Element(CellHeader).Text("Estado");
                            h.Cell().Element(CellHeader).Text("Persona");
                            h.Cell().Element(CellHeader).Text("Pensión");
                            h.Cell().Element(CellHeader).Text("Ingreso");
                            h.Cell().Element(CellHeader).Text("Adopción");
                            if (incluirHistoria)
                                h.Cell().Element(CellHeader).Text("Historia");

                            if (incluirSeguimiento)
                                h.Cell().Element(CellHeader).Text("Seguimiento");

                        });

                        foreach (var a in lista)
                        {
                            var especieTxt = dEsp.TryGetValue(a.id_especie, out var _esp) ? _esp : $"#{a.id_especie}";
                            var tamanoTxt = dTam.TryGetValue(a.id_tamano, out var _tam) ? _tam : $"#{a.id_tamano}";
                            var estadoTxt = dEst.TryGetValue(a.id_estadoAnimal, out var _est) ? _est : $"#{a.id_estadoAnimal}";
                            var personaTxt = a.id_persona.HasValue && dPer.TryGetValue(a.id_persona.Value, out var _per) ? _per : "";
                            var pensionTxt = a.id_pension.HasValue && dPen.TryGetValue(a.id_pension.Value, out var _p) ? _p : "";

                            table.Cell().Element(Cell).Text(a.id_animal);
                            table.Cell().Element(Cell).Text(a.nombre);
                            table.Cell().Element(Cell).Text(SexoTxt(a.sexo));
                            table.Cell().Element(Cell).Text($"{a.edadValor} {UnidadEdadTxt(a.edadUnidad)}");
                            table.Cell().Element(Cell).Text(especieTxt);
                            table.Cell().Element(Cell).Text(tamanoTxt);
                            table.Cell().Element(Cell).Text(estadoTxt);
                            table.Cell().Element(Cell).Text(personaTxt);
                            table.Cell().Element(Cell).Text(pensionTxt);
                            table.Cell().Element(Cell).Text(a.fechaIngreso?.ToString("dd/MM/yyyy") ?? "");
                            table.Cell().Element(Cell).Text(a.fechaAdopcion?.ToString("dd/MM/yyyy") ?? "");
                            if (incluirHistoria)
                                table.Cell().Element(Cell).Text(a.historia ?? "—");

                            if (incluirSeguimiento)
                                table.Cell().Element(Cell).Text(a.seguimiento ?? "—");

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
            var nombreArchivo = $"reporte_animales_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return File(bytes, "application/pdf", nombreArchivo);
        }


        // Escapa ; y comillas, y normaliza saltos de línea para que Excel no rompa filas
        private static string Esc(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var t = s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            if (t.Contains(';') || t.Contains('"'))
                return "\"" + t.Replace("\"", "\"\"") + "\"";
            return t;
        }

        // ===== Helpers de mapeo/escape =====
        private static string SexoTxt(string? s)
            => s == "M" ? "Macho" : s == "H" ? "Hembra" : "";

        private static string UnidadEdadTxt(string? u)
            => u == "A" ? "Años" : u == "M" ? "Meses" : "";

        // Genérico: arma diccionario ID -> Texto desde una respuesta de la API
        private static async Task<Dictionary<int, string>> ToDict<T>(
            HttpResponseMessage resp,
            Func<T, int> keySel,
            Func<T, string> valSel)
        {
            if (resp is null || !resp.IsSuccessStatusCode) return new Dictionary<int, string>();

            var json = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<IEnumerable<T>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                       ?? Enumerable.Empty<T>();

            // Si hay duplicados de ID, me quedo con el primero
            return list.GroupBy(keySel).ToDictionary(g => g.Key, g => valSel(g.First()));
        }

        private async Task CargarSelects(int? espSel, int? tamSel, int? estSel, int? perSel, int? penSel)
        {
            var client = _http.CreateClient("Api");

            var tEsp = client.GetAsync("/api/Especie");
            var tTam = client.GetAsync("/api/Tamano");
            var tEst = client.GetAsync("/api/estadoAnimal");
            var tPer = client.GetAsync("/api/Persona");
            var tPen = client.GetAsync("/api/Pension");
            await Task.WhenAll(tEsp, tTam, tEst, tPer, tPen);

            ViewBag.Especies = await ToSelectList<Especie>(tEsp.Result, x => x.id_especie, x => x.especie, espSel);
            ViewBag.Tamanos = await ToSelectList<Tamano>(tTam.Result, x => x.id_tamano, x => x.tamano, tamSel);
            ViewBag.Estados = await ToSelectList<Estado_Animal>(tEst.Result, x => x.id_estadoAnimal, x => x.estado, estSel);
            // Persona: “Apellido, Nombre” (o lo que tengas)
            ViewBag.Personas = await ToSelectList<Persona>(tPer.Result, x => x.id_persona,
                x => $"{x.apellido?.Trim()} {x.nombre?.Trim()}".Trim(), perSel);
            // Pensión: “nombre”
            ViewBag.Pensiones = await ToSelectList<Pension>(tPen.Result, x => x.id_pension, x => x.nombre, penSel);
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
    }
}