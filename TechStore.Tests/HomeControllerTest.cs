using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TechStore.Controllers;
using TechStore.Models;
using TechStore.Services.Interfaces;

namespace TechStore.Tests;

public class HomeControllerTests
{
    [Fact]
    public async Task Index_SinProductos_DevuelveListaVacia()
    {
        var service = new Mock<IProductoService>();
        service.Setup(s => s.ObtenerTodosAsync()).ReturnsAsync(new List<Producto>());
        var controller = new HomeController(service.Object);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var cards = Assert.IsAssignableFrom<IEnumerable<CardViewModel>>(view.Model);
        Assert.Empty(cards);
    }

    [Fact]
    public async Task Index_ConVariosProductosPorCategoria_DevuelveUnoPorCategoria()
    {
        var service = new Mock<IProductoService>();
        service.Setup(s => s.ObtenerTodosAsync()).ReturnsAsync(new List<Producto>
        {
            CreateProducto(1, "A1", categoriaId: 1, stock: 10),
            CreateProducto(2, "A2", categoriaId: 1, stock: 3),
            CreateProducto(3, "B1", categoriaId: 2, stock: 7)
        });
        var controller = new HomeController(service.Object);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var cards = Assert.IsAssignableFrom<IEnumerable<CardViewModel>>(view.Model).ToList();
        Assert.Equal(2, cards.Count);
    }

    [Fact]
    public async Task Index_ConVariosProductosPorCategoria_ElijeElDeMenorStock()
    {
        var service = new Mock<IProductoService>();
        service.Setup(s => s.ObtenerTodosAsync()).ReturnsAsync(new List<Producto>
        {
            CreateProducto(1, "Mucho stock", categoriaId: 1, stock: 10),
            CreateProducto(2, "Poco stock", categoriaId: 1, stock: 3)
        });
        var controller = new HomeController(service.Object);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var card = Assert.Single(Assert.IsAssignableFrom<IEnumerable<CardViewModel>>(view.Model));
        Assert.Equal("Poco stock", card.Title);
    }

    [Fact]
    public async Task Index_ConStockIgual_DesempataPorIdMenor()
    {
        var service = new Mock<IProductoService>();
        service.Setup(s => s.ObtenerTodosAsync()).ReturnsAsync(new List<Producto>
        {
            CreateProducto(5, "Segundo", categoriaId: 1, stock: 4),
            CreateProducto(2, "Primero", categoriaId: 1, stock: 4)
        });
        var controller = new HomeController(service.Object);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var card = Assert.Single(Assert.IsAssignableFrom<IEnumerable<CardViewModel>>(view.Model));
        Assert.Equal("Primero", card.Title);
    }

    [Fact]
    public async Task Index_ConProductoSinCategoriaNiImagen_UsaCadenasVacias()
    {
        var service = new Mock<IProductoService>();
        var producto = CreateProducto(1, "Huérfano", categoriaId: 1, stock: 2);
        producto.Categoria = null;
        producto.ImagenUrl = null;
        service.Setup(s => s.ObtenerTodosAsync()).ReturnsAsync(new List<Producto> { producto });
        var controller = new HomeController(service.Object);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var card = Assert.Single(Assert.IsAssignableFrom<IEnumerable<CardViewModel>>(view.Model));
        Assert.Equal(string.Empty, card.Category);
        Assert.Equal(string.Empty, card.ImageUrl);
    }

    [Fact]
    public async Task Index_ConProducto_MapeaLosCamposDeLaTarjeta()
    {
        var service = new Mock<IProductoService>();
        var producto = CreateProducto(7, "iPhone 16", categoriaId: 1, stock: 10);
        producto.Precio = 1250.5m;
        service.Setup(s => s.ObtenerTodosAsync()).ReturnsAsync(new List<Producto> { producto });
        var controller = new HomeController(service.Object);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var card = Assert.Single(Assert.IsAssignableFrom<IEnumerable<CardViewModel>>(view.Model));
        Assert.Equal(7, card.Id);
        Assert.Equal("iPhone 16", card.Title);
        Assert.Equal(1250.5f, card.Price);
        Assert.Equal("Celulares", card.Category);
        Assert.Equal("Disponible", card.State);
        Assert.Equal(10, card.Stock);
    }

    [Fact]
    public void Error_ConTraceIdentifier_DevuelveElRequestId()
    {
        var controller = new HomeController(new Mock<IProductoService>().Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-123" }
            }
        };

        var result = controller.Error();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(view.Model);
        Assert.Equal("trace-123", model.RequestId);
    }

    private static Producto CreateProducto(int id, string nombre, int categoriaId, int stock)
    {
        return new Producto
        {
            ID = id,
            Nombre = nombre,
            Descripcion = $"Descripción de {nombre}.",
            Precio = 100m,
            Stock = stock,
            Estado = "Disponible",
            CategoriaId = categoriaId,
            ImagenUrl = $"{nombre}.jpg",
            Categoria = new Categoria { ID = categoriaId, Nombre = "Celulares" }
        };
    }
}
