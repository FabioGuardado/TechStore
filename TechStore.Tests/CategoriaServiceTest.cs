using Microsoft.EntityFrameworkCore;
using TechStore.Data;
using TechStore.Models;
using TechStore.Services;

namespace TechStore.Tests;

public class CategoriaServiceTests
{
    [Fact]
    public async Task ObtenerTodas_ConCategorias_DevuelveTodasOrdenadasPorNombre()
    {
        await using var context = CreateContext();
        context.Categorias.AddRange(
            new Categoria { Nombre = "Tablets" },
            new Categoria { Nombre = "Audio" },
            new Categoria { Nombre = "Consolas" });
        await context.SaveChangesAsync();
        var service = new CategoriaService(context);

        var result = (await service.ObtenerTodasAsync()).ToList();

        Assert.Equal(new[] { "Audio", "Consolas", "Tablets" }, result.Select(c => c.Nombre));
    }

    [Fact]
    public async Task ObtenerTodas_SinCategorias_DevuelveListaVacia()
    {
        await using var context = CreateContext();
        var service = new CategoriaService(context);

        var result = await service.ObtenerTodasAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ObtenerConProductos_ConCategorias_IncluyeSusProductos()
    {
        await using var context = CreateContext();
        var audio = new Categoria { Nombre = "Audio" };
        audio.Productos.Add(CreateProducto("Auriculares"));
        audio.Productos.Add(CreateProducto("Parlante"));
        context.Categorias.AddRange(audio, new Categoria { Nombre = "Tablets" });
        await context.SaveChangesAsync();
        var service = new CategoriaService(context);

        var result = (await service.ObtenerConProductosAsync()).ToList();

        Assert.Equal(2, result[0].Productos.Count);
        Assert.Empty(result[1].Productos);
    }

    [Fact]
    public async Task ObtenerPorId_ConCategoriaExistente_DevuelveLaCategoriaConProductos()
    {
        await using var context = CreateContext();
        var categoria = new Categoria { Nombre = "Audio" };
        categoria.Productos.Add(CreateProducto("Auriculares"));
        context.Categorias.Add(categoria);
        await context.SaveChangesAsync();
        var service = new CategoriaService(context);

        var result = await service.ObtenerPorIdAsync(categoria.ID);

        Assert.NotNull(result);
        Assert.Equal("Audio", result.Nombre);
        Assert.Single(result.Productos);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task ObtenerPorId_ConCategoriaInexistente_DevuelveNull(int id)
    {
        await using var context = CreateContext();
        var service = new CategoriaService(context);

        var result = await service.ObtenerPorIdAsync(id);

        Assert.Null(result);
    }

    private static TechStoreContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<TechStoreContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new TechStoreContext(options);
    }

    private static Producto CreateProducto(string nombre)
    {
        return new Producto
        {
            Nombre = nombre,
            Descripcion = $"Descripción de {nombre}.",
            Precio = 100m,
            Stock = 5,
            Estado = "Disponible"
        };
    }
}
