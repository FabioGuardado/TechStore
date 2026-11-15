using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using TechStore.Controllers;
using TechStore.Models;
using TechStore.Services.Interfaces;

namespace TechStore.Tests;

public class ReportControllerTests
{
    [Fact]
    public async Task ProductosExcel_ConProductos_DevuelveElArchivo()
    {
        var servicio = new Mock<IProductoService>();
        servicio.Setup(s => s.ObtenerTodosAsync()).ReturnsAsync(new List<Producto> { CreateProducto() });
        var controller = CreateController(servicio);

        var result = await controller.ProductosExcel();

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("reporte-productos.xlsx", file.FileDownloadName);
    }

    [Fact]
    public async Task ProductosPdf_CuandoElServicioFalla_RedirigeConErrorYRegistraLaExcepcion()
    {
        var logger = new Mock<ILogger<ReportController>>();
        var servicio = new Mock<IProductoService>();
        var fallo = new InvalidOperationException("BD caída");
        servicio.Setup(s => s.ObtenerTodosAsync()).ThrowsAsync(fallo);
        var controller = CreateController(servicio, logger);

        var result = await controller.ProductosPdf();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Products", redirect.ControllerName);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
        VerifyError(logger, fallo);
    }

    [Fact]
    public async Task ProductosExcel_CuandoElServicioFalla_RedirigeConErrorYRegistraLaExcepcion()
    {
        var logger = new Mock<ILogger<ReportController>>();
        var servicio = new Mock<IProductoService>();
        var fallo = new InvalidOperationException("BD caída");
        servicio.Setup(s => s.ObtenerTodosAsync()).ThrowsAsync(fallo);
        var controller = CreateController(servicio, logger);

        var result = await controller.ProductosExcel();

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
        VerifyError(logger, fallo);
    }

    private static void VerifyError(Mock<ILogger<ReportController>> logger, Exception fallo)
    {
        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            fallo,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    private static ReportController CreateController(Mock<IProductoService> servicio, Mock<ILogger<ReportController>>? logger = null)
    {
        logger ??= new Mock<ILogger<ReportController>>();
        var httpContext = new DefaultHttpContext();

        return new ReportController(servicio.Object, logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };
    }

    private static Producto CreateProducto()
    {
        return new Producto
        {
            ID = 1,
            Nombre = "Auriculares",
            Descripcion = "Descripción.",
            Precio = 100m,
            Stock = 5,
            Estado = "Disponible",
            CategoriaId = 1,
            Categoria = new Categoria { ID = 1, Nombre = "Audio" }
        };
    }
}
