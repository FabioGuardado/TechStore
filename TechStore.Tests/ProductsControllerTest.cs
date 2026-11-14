using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using TechStore.Controllers;
using TechStore.Models;
using TechStore.Services.Interfaces;

namespace TechStore.Tests;

public class ProductsControllerTests
{
    [Fact]
    public async Task Index_ConProductos_DevuelveLaListaDelServicio()
    {
        var productos = new List<Producto> { CreateProducto(1, "Alfa"), CreateProducto(2, "Beta") };
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerTodosAsync()).ReturnsAsync(productos);
        var controller = CreateController(productoService: productoService);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Producto>>(view.Model);
        Assert.Equal(2, model.Count());
    }

    [Fact]
    public async Task Index_SinProductos_DevuelveListaVacia()
    {
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerTodosAsync()).ReturnsAsync(new List<Producto>());
        var controller = CreateController(productoService: productoService);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<Producto>>(view.Model));
    }

    [Fact]
    public async Task Details_ConIdNulo_DevuelveNotFound()
    {
        var controller = CreateController();

        var result = await controller.Details(null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task Details_ConProductoInexistente_DevuelveNotFound(int id)
    {
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerPorIdAsync(id)).ReturnsAsync((Producto?)null);
        var controller = CreateController(productoService: productoService);

        var result = await controller.Details(id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ConProductoExistente_DevuelveCardViewModelMapeado()
    {
        var producto = CreateProducto(3, "Steam Deck OLED");
        producto.ImagenUrl = "steam.jpg";
        producto.Precio = 980.5m;
        producto.Stock = 4;
        producto.Categoria = new Categoria { ID = 1, Nombre = "Consolas" };
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerPorIdAsync(3)).ReturnsAsync(producto);
        var controller = CreateController(productoService: productoService);

        var result = await controller.Details(3);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CardViewModel>(view.Model);
        Assert.Equal(3, model.Id);
        Assert.Equal("Steam Deck OLED", model.Title);
        Assert.Equal(producto.Descripcion, model.Description);
        Assert.Equal("steam.jpg", model.ImageUrl);
        Assert.Equal(980.5f, model.Price);
        Assert.Equal("Consolas", model.Category);
        Assert.Equal(producto.Estado, model.State);
        Assert.Equal(4, model.Stock);
    }

    [Fact]
    public async Task Details_ConProductoSinImagenNiCategoria_UsaCadenasVacias()
    {
        var producto = CreateProducto(3, "Sin datos");
        producto.ImagenUrl = null;
        producto.Categoria = null;
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerPorIdAsync(3)).ReturnsAsync(producto);
        var controller = CreateController(productoService: productoService);

        var result = await controller.Details(3);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CardViewModel>(view.Model);
        Assert.Equal(string.Empty, model.ImageUrl);
        Assert.Equal(string.Empty, model.Category);
    }

    [Fact]
    public async Task AgregarGet_DevuelveVistaConCategoriasOrdenadasPorNombre()
    {
        var categoriaService = new Mock<ICategoriaService>();
        categoriaService.Setup(s => s.ObtenerTodasAsync()).ReturnsAsync(new List<Categoria>
        {
            new() { ID = 1, Nombre = "Tablets" },
            new() { ID = 2, Nombre = "Audio" }
        });
        var controller = CreateController(categoriaService: categoriaService);

        var result = await controller.Agregar();

        Assert.IsType<ViewResult>(result);
        var categorias = Assert.IsAssignableFrom<IEnumerable<Categoria>>((object)controller.ViewBag.Categorias);
        Assert.Equal(new[] { "Audio", "Tablets" }, categorias.Select(c => c.Nombre));
    }

    [Fact]
    public async Task AgregarPost_ConProductoValido_GuardaYRedirigeAlIndice()
    {
        var productoService = new Mock<IProductoService>();
        var controller = CreateController(productoService: productoService);
        var producto = CreateProducto(0, "iPad Air");

        var result = await controller.Agregar(producto, null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProductsController.Index), redirect.ActionName);
        productoService.Verify(s => s.AgregarAsync(producto), Times.Once);
        Assert.Equal("Producto agregado exitosamente.", controller.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData(nameof(Producto.Nombre))]
    [InlineData(nameof(Producto.Descripcion))]
    [InlineData(nameof(Producto.Precio))]
    [InlineData(nameof(Producto.Stock))]
    [InlineData(nameof(Producto.Estado))]
    [InlineData(nameof(Producto.CategoriaId))]
    public async Task AgregarPost_ConModeloInvalido_NoGuardaYDevuelveLaVista(string campo)
    {
        var productoService = new Mock<IProductoService>();
        var controller = CreateController(productoService: productoService);
        var producto = CreateProducto(0, "Incompleto");
        controller.ModelState.AddModelError(campo, "Campo inválido.");

        var result = await controller.Agregar(producto, null);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(producto, view.Model);
        Assert.NotNull(controller.ViewBag.Categorias);
        productoService.Verify(s => s.AgregarAsync(It.IsAny<Producto>()), Times.Never);
    }

    [Fact]
    public async Task AgregarPost_ConImagen_GuardaElArchivoYAsignaImagenUrl()
    {
        var webRoot = CreateTempWebRoot();
        try
        {
            var productoService = new Mock<IProductoService>();
            var controller = CreateController(productoService: productoService, webRootPath: webRoot);
            var producto = CreateProducto(0, "Con imagen");
            var imagen = CreateImagen("foto.png", length: 10);

            var result = await controller.Agregar(producto, imagen.Object);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.EndsWith(".png", producto.ImagenUrl);
            Assert.True(File.Exists(Path.Combine(webRoot, "images", producto.ImagenUrl!)));
            imagen.Verify(i => i.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
            productoService.Verify(s => s.AgregarAsync(producto), Times.Once);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AgregarPost_ConImagenVacia_NoGuardaArchivoNiAsignaImagenUrl()
    {
        var webRoot = CreateTempWebRoot();
        try
        {
            var controller = CreateController(webRootPath: webRoot);
            var producto = CreateProducto(0, "Imagen vacía");
            var imagen = CreateImagen("vacia.png", length: 0);

            var result = await controller.Agregar(producto, imagen.Object);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Null(producto.ImagenUrl);
            imagen.Verify(i => i.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.False(Directory.Exists(Path.Combine(webRoot, "images")));
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task EditarGet_ConProductoExistente_DevuelveLaVistaConElProducto()
    {
        var producto = CreateProducto(5, "Existente");
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerPorIdAsync(5)).ReturnsAsync(producto);
        var controller = CreateController(productoService: productoService);

        var result = await controller.Editar(5);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(producto, view.Model);
        Assert.NotNull(controller.ViewBag.Categorias);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task EditarGet_ConProductoInexistente_DevuelveNotFound(int id)
    {
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerPorIdAsync(id)).ReturnsAsync((Producto?)null);
        var controller = CreateController(productoService: productoService);

        var result = await controller.Editar(id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditarPost_ConIdDistintoAlDelProducto_DevuelveNotFound()
    {
        var productoService = new Mock<IProductoService>();
        var controller = CreateController(productoService: productoService);

        var result = await controller.Editar(1, CreateProducto(2, "Otro"), null);

        Assert.IsType<NotFoundResult>(result);
        productoService.Verify(s => s.EditarAsync(It.IsAny<Producto>()), Times.Never);
    }

    [Fact]
    public async Task EditarPost_ConModeloInvalido_NoEditaYDevuelveLaVista()
    {
        var productoService = new Mock<IProductoService>();
        var controller = CreateController(productoService: productoService);
        var producto = CreateProducto(1, "Inválido");
        controller.ModelState.AddModelError(nameof(Producto.Nombre), "El nombre es obligatorio.");

        var result = await controller.Editar(1, producto, null);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(producto, view.Model);
        productoService.Verify(s => s.EditarAsync(It.IsAny<Producto>()), Times.Never);
    }

    [Fact]
    public async Task EditarPost_ConProductoInexistente_DevuelveNotFound()
    {
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerPorIdAsync(1)).ReturnsAsync((Producto?)null);
        var controller = CreateController(productoService: productoService);

        var result = await controller.Editar(1, CreateProducto(1, "Fantasma"), null);

        Assert.IsType<NotFoundResult>(result);
        productoService.Verify(s => s.EditarAsync(It.IsAny<Producto>()), Times.Never);
    }

    [Fact]
    public async Task EditarPost_SinImagenNueva_ConservaLaImagenAnteriorYRedirigeAlIndice()
    {
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerPorIdAsync(1)).ReturnsAsync(CreateProducto(1, "Original", "anterior.jpg"));
        productoService.Setup(s => s.EditarAsync(It.IsAny<Producto>())).ReturnsAsync(true);
        var controller = CreateController(productoService: productoService);
        var editado = CreateProducto(1, "Editado");

        var result = await controller.Editar(1, editado, null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProductsController.Index), redirect.ActionName);
        productoService.Verify(s => s.EditarAsync(It.Is<Producto>(p => p.ImagenUrl == "anterior.jpg")), Times.Once);
        Assert.Equal("Producto editado exitosamente.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task EditarPost_ConImagenNueva_ReemplazaYBorraLaImagenAnterior()
    {
        var webRoot = CreateTempWebRoot();
        try
        {
            var imagesPath = Path.Combine(webRoot, "images");
            Directory.CreateDirectory(imagesPath);
            var anterior = Path.Combine(imagesPath, "anterior.jpg");
            await File.WriteAllTextAsync(anterior, "contenido");
            var productoService = new Mock<IProductoService>();
            productoService.Setup(s => s.ObtenerPorIdAsync(1)).ReturnsAsync(CreateProducto(1, "Original", "anterior.jpg"));
            productoService.Setup(s => s.EditarAsync(It.IsAny<Producto>())).ReturnsAsync(true);
            var controller = CreateController(productoService: productoService, webRootPath: webRoot);
            var editado = CreateProducto(1, "Editado");
            var imagen = CreateImagen("nueva.webp", length: 10);

            var result = await controller.Editar(1, editado, imagen.Object);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.False(File.Exists(anterior));
            Assert.EndsWith(".webp", editado.ImagenUrl);
            Assert.NotEqual("anterior.jpg", editado.ImagenUrl);
            Assert.True(File.Exists(Path.Combine(imagesPath, editado.ImagenUrl!)));
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task EditarPost_CuandoElServicioNoEncuentraElProducto_DevuelveNotFound()
    {
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerPorIdAsync(1)).ReturnsAsync(CreateProducto(1, "Original"));
        productoService.Setup(s => s.EditarAsync(It.IsAny<Producto>())).ReturnsAsync(false);
        var controller = CreateController(productoService: productoService);

        var result = await controller.Editar(1, CreateProducto(1, "Editado"), null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task Eliminar_ConProductoInexistente_DevuelveNotFound(int id)
    {
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerPorIdAsync(id)).ReturnsAsync((Producto?)null);
        var controller = CreateController(productoService: productoService);

        var result = await controller.Eliminar(id);

        Assert.IsType<NotFoundResult>(result);
        productoService.Verify(s => s.EliminarAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Eliminar_ConProductoExistente_EliminaElProductoYRedirigeAlIndice()
    {
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerPorIdAsync(1)).ReturnsAsync(CreateProducto(1, "Borrar"));
        productoService.Setup(s => s.EliminarAsync(1)).ReturnsAsync(true);
        var controller = CreateController(productoService: productoService);

        var result = await controller.Eliminar(1);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProductsController.Index), redirect.ActionName);
        productoService.Verify(s => s.EliminarAsync(1), Times.Once);
        Assert.Equal("Producto eliminado exitosamente.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task Eliminar_ConProductoConImagen_BorraElArchivoDeImagen()
    {
        var webRoot = CreateTempWebRoot();
        try
        {
            var imagesPath = Path.Combine(webRoot, "images");
            Directory.CreateDirectory(imagesPath);
            var imagen = Path.Combine(imagesPath, "borrar.jpg");
            await File.WriteAllTextAsync(imagen, "contenido");
            var productoService = new Mock<IProductoService>();
            productoService.Setup(s => s.ObtenerPorIdAsync(1)).ReturnsAsync(CreateProducto(1, "Borrar", "borrar.jpg"));
            productoService.Setup(s => s.EliminarAsync(1)).ReturnsAsync(true);
            var controller = CreateController(productoService: productoService, webRootPath: webRoot);

            var result = await controller.Eliminar(1);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.False(File.Exists(imagen));
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Eliminar_CuandoElServicioNoEliminaElProducto_DevuelveNotFound()
    {
        var productoService = new Mock<IProductoService>();
        productoService.Setup(s => s.ObtenerPorIdAsync(1)).ReturnsAsync(CreateProducto(1, "Borrar"));
        productoService.Setup(s => s.EliminarAsync(1)).ReturnsAsync(false);
        var controller = CreateController(productoService: productoService);

        var result = await controller.Eliminar(1);

        Assert.IsType<NotFoundResult>(result);
    }

    private static ProductsController CreateController(
        Mock<IProductoService>? productoService = null,
        Mock<ICategoriaService>? categoriaService = null,
        string? webRootPath = null)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.WebRootPath).Returns(webRootPath ?? Path.GetTempPath());
        productoService ??= new Mock<IProductoService>();
        categoriaService ??= new Mock<ICategoriaService>();

        return new ProductsController(productoService.Object, categoriaService.Object, environment.Object)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
    }

    private static Producto CreateProducto(int id, string nombre, string? imagenUrl = null)
    {
        return new Producto
        {
            ID = id,
            Nombre = nombre,
            Descripcion = $"Descripción de {nombre}.",
            ImagenUrl = imagenUrl,
            Precio = 100m,
            Stock = 5,
            Estado = "Disponible",
            CategoriaId = 1
        };
    }

    private static Mock<IFormFile> CreateImagen(string fileName, long length)
    {
        var imagen = new Mock<IFormFile>();
        imagen.Setup(i => i.FileName).Returns(fileName);
        imagen.Setup(i => i.Length).Returns(length);
        imagen.Setup(i => i.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return imagen;
    }

    private static string CreateTempWebRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }
}
