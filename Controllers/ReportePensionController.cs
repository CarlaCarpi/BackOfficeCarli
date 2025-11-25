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
    public class ReportePensionController : Controller
    {
        private readonly IHttpClientFactory _http;
        public ReportePensionController(IHttpClientFactory http) => _http = http;

        private static readonly JsonSerializerOptions JOps = new() { PropertyNameCaseInsensitive = true };

        // ===================== INDEX =====================
        [HttpGet]
        public async Task<IActionResult> Index(
            int? id_provincia,
            int? id_localidad,
            int? id_estadoPension,
            DateTime? fechaIngresoDesde,
            DateTime? fechaIngresoHasta,
            bool incluirRedes = false,
            bool incluirMonto = false)
        {
            await CargarSelects(id_provincia, id_localidad, id_estadoPension);

            var client = _http.CreateClient("Api");

            var rPen = await client.GetAsync("/api/Pension");
            if (!rPen.IsSuccessStatusCode)
            {
                ViewBag.ApiError = $"GET /api/Pension -> {(int)rPen.StatusCode} {rPen.ReasonPhrase}";
                return View(Enumerable.Empty<Pension>());
            }

            var pensiones = JsonSerializer.Deserialize<IEnumerable<Pension>>(
                await rPen.Content.ReadAsStringAsync(), JOps) ?? Enumerable.Empty<Pension>();

            // NO mostrar pensiones eliminadas
            pensiones = pensiones.Where(p => p.fechaEliminacion == null);

            var q = pensiones.AsQueryable();

            if (id_provincia.HasValue && id_provincia > 0) q = q.Where(p => p.id_provincia == id_provincia);
            if (id_localidad.HasValue && id_localidad > 0) q = q.Where(p => p.id_localidad == id_localidad);
            if (id_estadoPension.HasValue && id_estadoPension > 0) q = q.Where(p => p.id_estadoPension == id_estadoPension);

            if (fechaIngresoDesde.HasValue) q = q.Where(p => p.fechaIngreso.Date >= fechaIngresoDesde.Value.Date);
            if (fechaIngresoHasta.HasValue) q = q.Where(p => p.fechaIngreso.Date <= fechaIngresoHasta.Value.Date);

            var resultado = q.OrderBy(p => p.nombre).ThenBy(p => p.id_pension).ToList();

            // Paso flags a la vista para el render condicional de columnas
            ViewBag.IncluirRedes = incluirRedes;
            ViewBag.IncluirMonto = incluirMonto;

            return View(resultado);
        }

        // ===================== CSV =====================
        [HttpGet]
        public async Task<IActionResult> ExportarCsv(
            int? id_provincia,
            int? id_localidad,
            int? id_estadoPension,
            DateTime? fechaIngresoDesde,
            DateTime? fechaIngresoHasta,
            bool incluirRedes = false,
            bool incluirMonto = false)
        {
            var client = _http.CreateClient("Api");

            var rPen = await client.GetAsync("/api/Pension");
            if (!rPen.IsSuccessStatusCode)
                return Content("No se pudo obtener pensiones.", "text/plain");

            var pensiones = JsonSerializer.Deserialize<IEnumerable<Pension>>(
                await rPen.Content.ReadAsStringAsync(), JOps) ?? Enumerable.Empty<Pension>();

            // NO mostrar pensiones eliminadas
            pensiones = pensiones.Where(p => p.fechaEliminacion == null);

            var provincias = await ToDict<Provincia, int>(client, "/api/Provincia", x => x.id_provincia, x => x.nombre);
            var localidades = await ToDict<Localidad, int>(client, "/api/Localidad", x => x.id_localidad, x => x.nombre);
            var estados = await ToDict<Estado_Pension, int>(client, "/api/EstadoPension", x => x.id_estadoPension, x => x.descripcion);

            var q = pensiones.AsQueryable();
            if (id_provincia > 0) q = q.Where(p => p.id_provincia == id_provincia);
            if (id_localidad > 0) q = q.Where(p => p.id_localidad == id_localidad);
            if (id_estadoPension > 0) q = q.Where(p => p.id_estadoPension == id_estadoPension);
            if (fechaIngresoDesde.HasValue) q = q.Where(p => p.fechaIngreso.Date >= fechaIngresoDesde.Value.Date);
            if (fechaIngresoHasta.HasValue) q = q.Where(p => p.fechaIngreso.Date <= fechaIngresoHasta.Value.Date);

            var lista = q.OrderBy(p => p.nombre).ThenBy(p => p.id_pension).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("sep=;");

            // Header base
            var headers = new List<string>
            {
                "Nombre","Email","Teléfono 1","Calle","Altura","Depto",
                "Provincia","Localidad","Estado","Ingreso","Egreso"
            };
            if (incluirRedes) headers.Add("Redes");
            if (incluirMonto) headers.Add("Monto Día");
            sb.AppendLine(string.Join(";", headers));

            foreach (var p in lista)
            {
                provincias.TryGetValue(p.id_provincia, out var prov);
                localidades.TryGetValue(p.id_localidad, out var loc);
                estados.TryGetValue(p.id_estadoPension, out var est);

                var cols = new List<string>
                {
                    Esc(p.nombre),
                    Esc(p.email),
                    Esc(p.telefono1),
                    Esc(p.calle),
                    p.altura.ToString(),
                    Esc(p.departamento),
                    Esc(prov),
                    Esc(loc),
                    Esc(est),
                    p.fechaIngreso.ToString("yyyy-MM-dd"),
                    p.fechaEgreso?.ToString("yyyy-MM-dd") ?? ""
                };
                if (incluirRedes) cols.Add(Esc(p.redesSociales));
                if (incluirMonto) cols.Add(p.montoDia.HasValue ? p.montoDia.Value.ToString("0.##") : "");

                sb.AppendLine(string.Join(";", cols));
            }

            var bytes = Encoding.Unicode.GetBytes(sb.ToString());
            var nombreArchivo = $"reporte_pensiones_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            return File(bytes, "text/csv; charset=utf-16", nombreArchivo);
        }

        // ===================== PDF =====================
        [HttpGet]
        public async Task<IActionResult> ExportarPdf(
            int? id_provincia,
            int? id_localidad,
            int? id_estadoPension,
            DateTime? fechaIngresoDesde,
            DateTime? fechaIngresoHasta,
            bool incluirRedes = false,
            bool incluirMonto = false)
        {
            var client = _http.CreateClient("Api");

            var rPen = await client.GetAsync("/api/Pension");
            if (!rPen.IsSuccessStatusCode)
                return Content("No se pudo obtener pensiones.", "text/plain");

            var pensiones = JsonSerializer.Deserialize<IEnumerable<Pension>>(
                await rPen.Content.ReadAsStringAsync(), JOps) ?? Enumerable.Empty<Pension>();

            // NO mostrar pensiones eliminadas
            pensiones = pensiones.Where(p => p.fechaEliminacion == null);

            var provincias = await ToDict<Provincia, int>(client, "/api/Provincia", x => x.id_provincia, x => x.nombre);
            var localidades = await ToDict<Localidad, int>(client, "/api/Localidad", x => x.id_localidad, x => x.nombre);
            var estados = await ToDict<Estado_Pension, int>(client, "/api/EstadoPension", x => x.id_estadoPension, x => x.descripcion);

            var q = pensiones.AsQueryable();
            if (id_provincia > 0) q = q.Where(p => p.id_provincia == id_provincia);
            if (id_localidad > 0) q = q.Where(p => p.id_localidad == id_localidad);
            if (id_estadoPension > 0) q = q.Where(p => p.id_estadoPension == id_estadoPension);
            if (fechaIngresoDesde.HasValue) q = q.Where(p => p.fechaIngreso.Date >= fechaIngresoDesde.Value.Date);
            if (fechaIngresoHasta.HasValue) q = q.Where(p => p.fechaIngreso.Date <= fechaIngresoHasta.Value.Date);

            var lista = q.OrderBy(p => p.nombre).ThenBy(p => p.id_pension).ToList();

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
                        r.RelativeItem().Text("Reporte Pensiones").SemiBold().FontSize(16);
                        r.ConstantItem(120).AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.6f); // Nombre
                            cols.RelativeColumn(1.8f); // Email
                            cols.RelativeColumn(1.2f); // Tel1
                            cols.RelativeColumn(1.8f); // Calle
                            cols.RelativeColumn(0.8f); // Altura
                            cols.RelativeColumn(0.9f); // Dpto
                            cols.RelativeColumn(1.6f); // Provincia
                            cols.RelativeColumn(1.6f); // Localidad
                            cols.RelativeColumn(1.2f); // Estado
                            cols.RelativeColumn(1.1f); // Ingreso
                            cols.RelativeColumn(1.1f); // Egreso
                            if (incluirRedes) cols.RelativeColumn(1.8f); // Redes (opcional)
                            if (incluirMonto) cols.RelativeColumn(1.1f); // Monto Día (opcional)
                        });

                        static IContainer CellHeader(IContainer c) =>
                            c.Background("#2FA8A2").Padding(4).DefaultTextStyle(x => x.FontColor("#FFFFFF").Bold());
                        static IContainer Cell(IContainer c) =>
                            c.Border(0.5f).BorderColor("#e5e7eb").Padding(3);

                        table.Header(h =>
                        {
                            h.Cell().Element(CellHeader).Text("Nombre");
                            h.Cell().Element(CellHeader).Text("Email");
                            h.Cell().Element(CellHeader).Text("Tel. 1");
                            h.Cell().Element(CellHeader).Text("Calle");
                            h.Cell().Element(CellHeader).Text("Altura");
                            h.Cell().Element(CellHeader).Text("Depto");
                            h.Cell().Element(CellHeader).Text("Provincia");
                            h.Cell().Element(CellHeader).Text("Localidad");
                            h.Cell().Element(CellHeader).Text("Estado");
                            h.Cell().Element(CellHeader).Text("Ingreso");
                            h.Cell().Element(CellHeader).Text("Egreso");
                            if (incluirRedes) h.Cell().Element(CellHeader).Text("Redes");
                            if (incluirMonto) h.Cell().Element(CellHeader).Text("Monto Día");
                        });

                        foreach (var p in lista)
                        {
                            provincias.TryGetValue(p.id_provincia, out var prov);
                            localidades.TryGetValue(p.id_localidad, out var loc);
                            estados.TryGetValue(p.id_estadoPension, out var est);

                            table.Cell().Element(Cell).Text(p.nombre ?? "—");
                            table.Cell().Element(Cell).Text(p.email ?? "—");
                            table.Cell().Element(Cell).Text(p.telefono1 ?? "—");
                            table.Cell().Element(Cell).Text(p.calle ?? "—");
                            table.Cell().Element(Cell).Text(p.altura);
                            table.Cell().Element(Cell).Text(p.departamento ?? "—");
                            table.Cell().Element(Cell).Text(prov ?? $"#{p.id_provincia}");
                            table.Cell().Element(Cell).Text(loc ?? $"#{p.id_localidad}");
                            table.Cell().Element(Cell).Text(est ?? $"#{p.id_estadoPension}");
                            table.Cell().Element(Cell).Text(p.fechaIngreso.ToString("dd/MM/yyyy"));
                            table.Cell().Element(Cell).Text(p.fechaEgreso.HasValue ? p.fechaEgreso.Value.ToString("dd/MM/yyyy") : "—");
                            if (incluirRedes) table.Cell().Element(Cell).Text(p.redesSociales ?? "—");
                            if (incluirMonto) table.Cell().Element(Cell).Text(p.montoDia.HasValue ? p.montoDia.Value.ToString("0.##") : "—");
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
            var nombreArchivo = $"reporte_pensiones_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
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

        private async Task CargarSelects(int? provSel, int? locSel, int? estSel)
        {
            var client = _http.CreateClient("Api");

            var tProv = client.GetAsync("/api/Provincia");
            var tLoc = client.GetAsync("/api/Localidad");
            var tEst = client.GetAsync("/api/EstadoPension");
            await Task.WhenAll(tProv, tLoc, tEst);

            ViewBag.Provincias = await ToSelectList(tProv.Result, (Provincia x) => x.id_provincia, x => x.nombre, provSel);

            var locItems = new List<SelectListItem> { new SelectListItem { Text = "Seleccione...", Value = "" } };
            if (tLoc.Result is not null && tLoc.Result.IsSuccessStatusCode)
            {
                var json = await tLoc.Result.Content.ReadAsStringAsync();
                var locs = JsonSerializer.Deserialize<IEnumerable<Localidad>>(json, JOps) ?? Enumerable.Empty<Localidad>();
                if (provSel.HasValue && provSel > 0)
                    locs = locs.Where(l => l.id_provincia == provSel.Value);

                locItems.AddRange(locs.Select(l => new SelectListItem
                {
                    Value = l.id_localidad.ToString(),
                    Text = l.nombre,
                    Selected = locSel.HasValue && l.id_localidad == locSel.Value
                }));
            }
            ViewBag.Localidades = locItems;

            ViewBag.Estados = await ToSelectList(tEst.Result, (Estado_Pension x) => x.id_estadoPension, x => x.descripcion, estSel);
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

        private class Provincia { public int id_provincia { get; set; } public string nombre { get; set; } = ""; }
        private class Localidad { public int id_localidad { get; set; } public int id_provincia { get; set; } public string nombre { get; set; } = ""; }
        private class Estado_Pension { public int id_estadoPension { get; set; } public string descripcion { get; set; } = ""; }
    }
}
