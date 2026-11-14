using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechStore.Controllers;
using TechStore.Data;
using TechStore.Models;

namespace TechStore.Tests;

public class CategoriesControllerTests
{
    [Fact]
    public void Index_SinProductos_DevuelveVistaConGruposVacios()
    {
        using var context = CreateContext();
        var controller = new CategoriesController(context);

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var groups = Assert.IsAssignableFrom<IEnumerable<IGrouping<string, CardViewModel>>>(view.Model);
        Assert.Empty(groups);
    }

    [Fact]
    public void Index_ConProductos_AgrupaPorCategoriaOrdenadaPorNombre()
    {
        using var context = CreateContext();
        var tablets = new Categoria { Nombre = "Tablets" };
        var audio = new Categoria { Nombre = "Audio" };
        context.Productos.AddRange(
            CreateProducto("iPad Air", tablets),
            CreateProducto("Auriculares", audio),
            CreateProducto("Parlante", audio));
        context.SaveChanges();
        var controller = new CategoriesController(context);

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var groups = Assert.IsAssignableFrom<IEnumerable<IGrouping<string, CardViewModel>>>(view.Model).ToList();
        Assert.Equal(new[] { "Audio", "Tablets" }, groups.Select(g => g.Key));
        Assert.Equal(2, groups[0].Count());
        Assert.Single(groups[1]);
    }

    [Fact]
    public void Index_ConProducto_MapeaLosCamposDeLaTarjeta()
    {
        using var context = CreateContext();
        var producto = CreateProducto("Steam Deck", new Categoria { Nombre = "Consolas" });
        producto.ImagenUrl = "steam.jpg";
        producto.Precio = 980.5m;
        producto.Stock = 4;
        context.Productos.Add(producto);
        context.SaveChanges();
        var controller = new CategoriesController(context);

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var card = Assert.Single(Assert.Single(Assert.IsAssignableFrom<IEnumerable<IGrouping<string, CardViewModel>>>(view.Model)));
        Assert.Equal(producto.ID, card.Id);
        Assert.Equal("Steam Deck", card.Title);
        Assert.Equal("steam.jpg", card.ImageUrl);
        Assert.Equal(980.5f, card.Price);
        Assert.Equal("Consolas", card.Category);
        Assert.Equal("Disponible", card.State);
        Assert.Equal(4, card.Stock);
    }

    [Fact]
    public void Index_ConProductoSinImagen_UsaCadenaVacia()
    {
        using var context = CreateContext();
        context.Productos.Add(CreateProducto("Sin imagen", new Categoria { Nombre = "Audio" }));
        context.SaveChanges();
        var controller = new CategoriesController(context);

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var card = Assert.Single(Assert.Single(Assert.IsAssignableFrom<IEnumerable<IGrouping<string, CardViewModel>>>(view.Model)));
        Assert.Equal(string.Empty, card.ImageUrl);
    }

    private static TechStoreContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<TechStoreContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new TechStoreContext(options);
    }

    private static Producto CreateProducto(string nombre, Categoria categoria)
    {
        return new Producto
        {
            Nombre = nombre,
            Descripcion = $"Descripción de {nombre}.",
            Precio = 100m,
            Stock = 5,
            Estado = "Disponible",
            Categoria = categoria
        };
    }
}
