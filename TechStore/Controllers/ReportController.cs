using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TechStore.Models;
using TechStore.Services.Interfaces;

namespace TechStore.Controllers
{
    [Authorize(Roles = "Administrador,Vendedor")]
    public class ReportController : Controller
    {
        private readonly IProductoService _productoService;
        private readonly ILogger<ReportController> _logger;

        static ReportController()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public ReportController(
            IProductoService productoService,
            ILogger<ReportController> logger)
        {
            _productoService = productoService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ProductosPdf()
        {
            try
            {
                return await GenerarPdfAsync();
            }
            catch (Exception ex)
            {
                return ErrorDeReporte(ex, "PDF");
            }
        }

        private async Task<IActionResult> GenerarPdfAsync()
        {
            var productos = (await _productoService.ObtenerTodosAsync()).ToList();
            var usuario = User.Identity?.Name ?? "desconocido";

            _logger.LogInformation(
                "El usuario {Usuario} solicitó un reporte PDF de {Cantidad} productos.",
                usuario,
                productos.Count);

            var documento = Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(style => style.FontSize(9));

                    page.Header().Column(column =>
                    {
                        column.Item().Text("Reporte de productos")
                            .FontSize(20)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        column.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingTop(15).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(35);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                            columns.ConstantColumn(75);
                            columns.ConstantColumn(55);
                            columns.RelativeColumn(1.5f);
                        });

                        table.Header(header =>
                        {
                            HeaderCell(header.Cell(), "ID");
                            HeaderCell(header.Cell(), "Nombre");
                            HeaderCell(header.Cell(), "Categoría");
                            HeaderCell(header.Cell(), "Descripción");
                            HeaderCell(header.Cell(), "Precio");
                            HeaderCell(header.Cell(), "Stock");
                            HeaderCell(header.Cell(), "Estado");
                        });

                        foreach (var producto in productos)
                        {
                            BodyCell(table.Cell(), producto.ID.ToString());
                            BodyCell(table.Cell(), producto.Nombre);
                            BodyCell(table.Cell(), producto.Categoria?.Nombre ?? "Sin categoría");
                            BodyCell(table.Cell(), producto.Descripcion);
                            BodyCell(table.Cell(), producto.Precio.ToString("C2", CultureInfo.CurrentCulture));
                            BodyCell(table.Cell(), producto.Stock.ToString());
                            BodyCell(table.Cell(), producto.Estado);
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Página ");
                        text.CurrentPageNumber();
                        text.Span(" de ");
                        text.TotalPages();
                    });
                });
            });

            var contenido = documento.GeneratePdf();
            return File(contenido, "application/pdf", "reporte-productos.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> ProductosExcel()
        {
            try
            {
                return await GenerarExcelAsync();
            }
            catch (Exception ex)
            {
                return ErrorDeReporte(ex, "Excel");
            }
        }

        private async Task<IActionResult> GenerarExcelAsync()
        {
            var productos = (await _productoService.ObtenerTodosAsync()).ToList();
            var usuario = User.Identity?.Name ?? "desconocido";

            _logger.LogInformation(
                "El usuario {Usuario} solicitó un reporte Excel de {Cantidad} productos.",
                usuario,
                productos.Count);

            using var libro = new XLWorkbook();
            var hoja = libro.Worksheets.Add("Productos");

            hoja.Cell(1, 1).Value = "Reporte de productos";
            hoja.Range(1, 1, 1, 7).Merge();
            hoja.Cell(1, 1).Style.Font.Bold = true;
            hoja.Cell(1, 1).Style.Font.FontSize = 16;
            hoja.Cell(1, 1).Style.Font.FontColor = XLColor.White;
            hoja.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
            hoja.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            hoja.Cell(2, 1).Value = "Generado";
            hoja.Cell(2, 2).Value = DateTime.Now;
            hoja.Cell(2, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";

            var filaEncabezado = 4;
            var encabezados = new[]
            {
                "ID", "Nombre", "Categoría", "Descripción", "Precio", "Stock", "Estado"
            };

            for (var columna = 0; columna < encabezados.Length; columna++)
            {
                var celda = hoja.Cell(filaEncabezado, columna + 1);
                celda.Value = encabezados[columna];
                celda.Style.Font.Bold = true;
                celda.Style.Font.FontColor = XLColor.White;
                celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (var indice = 0; indice < productos.Count; indice++)
            {
                var producto = productos[indice];
                var fila = filaEncabezado + indice + 1;

                hoja.Cell(fila, 1).Value = producto.ID;
                hoja.Cell(fila, 2).Value = producto.Nombre;
                hoja.Cell(fila, 3).Value = producto.Categoria?.Nombre ?? "Sin categoría";
                hoja.Cell(fila, 4).Value = producto.Descripcion;
                hoja.Cell(fila, 5).Value = producto.Precio;
                hoja.Cell(fila, 5).Style.NumberFormat.Format = "$#,##0.00";
                hoja.Cell(fila, 6).Value = producto.Stock;
                hoja.Cell(fila, 7).Value = producto.Estado;
            }

            var ultimaFila = filaEncabezado + productos.Count;
            if (productos.Count > 0)
            {
                var tabla = hoja.Range(filaEncabezado, 1, ultimaFila, encabezados.Length)
                    .CreateTable("ProductosTable");
                tabla.Theme = XLTableTheme.TableStyleMedium2;
            }

            hoja.Columns().AdjustToContents();
            hoja.Column(4).Width = 45;
            hoja.Column(4).Style.Alignment.WrapText = true;
            hoja.SheetView.FreezeRows(filaEncabezado);

            using var memoria = new MemoryStream();
            libro.SaveAs(memoria);

            return File(
                memoria.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "reporte-productos.xlsx");
        }

        // Un fallo al consultar o generar el archivo no debe terminar en un 500: se informa y se vuelve al listado.
        private IActionResult ErrorDeReporte(Exception ex, string tipo)
        {
            _logger.LogError(
                ex,
                "Falló la generación del reporte {Tipo} solicitado por {Usuario}.",
                tipo,
                User.Identity?.Name ?? "desconocido");

            TempData["ErrorMessage"] = $"No se pudo generar el reporte {tipo}. Intenta de nuevo.";
            return RedirectToAction("Index", "Products");
        }

        private static void HeaderCell(IContainer container, string texto)
        {
            container
                .Background(Colors.Blue.Darken2)
                .Padding(5)
                .Text(texto)
                .FontColor(Colors.White)
                .Bold();
        }

        private static void BodyCell(IContainer container, string texto)
        {
            container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(4)
                .Text(texto);
        }
    }
}
