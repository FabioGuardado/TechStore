using Microsoft.EntityFrameworkCore;
using TechStore.Data;
using TechStore.Models;
using TechStore.Services;

namespace TechStore.Tests;

public class ProductoServiceTests
{
    [Fact]
    public async Task ObtenerTodos_ConProductos_DevuelveTodosOrdenadosPorNombre()
    {
        await using var context = CreateContext();
        var categoria = CreateCategoria("Audio");
        context.Productos.AddRange(
            CreateProducto("Zeta", categoria),
            CreateProducto("Alfa", categoria),
            CreateProducto("Medio", categoria));
        await context.SaveChangesAsync();
        var service = new ProductoService(context);

        var result = (await service.ObtenerTodosAsync()).ToList();

        Assert.Equal(new[] { "Alfa", "Medio", "Zeta" }, result.Select(p => p.Nombre));
    }

    [Fact]
    public async Task ObtenerTodos_ConProductos_IncluyeLaCategoria()
    {
        await using var context = CreateContext();
        context.Productos.Add(CreateProducto("Auriculares", CreateCategoria("Audio")));
        await context.SaveChangesAsync();
        var service = new ProductoService(context);

        var result = (await service.ObtenerTodosAsync()).ToList();

        Assert.Equal("Audio", Assert.Single(result).Categoria!.Nombre);
    }

    [Fact]
    public async Task ObtenerTodos_SinProductos_DevuelveListaVacia()
    {
        await using var context = CreateContext();
        var service = new ProductoService(context);

        var result = await service.ObtenerTodosAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ObtenerPorId_ConProductoExistente_DevuelveElProductoConCategoria()
    {
        await using var context = CreateContext();
        var producto = CreateProducto("Steam Deck", CreateCategoria("Consolas"));
        context.Productos.Add(producto);
        await context.SaveChangesAsync();
        var service = new ProductoService(context);

        var result = await service.ObtenerPorIdAsync(producto.ID);

        Assert.NotNull(result);
        Assert.Equal("Steam Deck", result.Nombre);
        Assert.Equal("Consolas", result.Categoria!.Nombre);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task ObtenerPorId_ConProductoInexistente_DevuelveNull(int id)
    {
        await using var context = CreateContext();
        var service = new ProductoService(context);

        var result = await service.ObtenerPorIdAsync(id);

        Assert.Null(result);
    }

    [Fact]
    public async Task Agregar_ConProductoValido_LoGuardaEnLaBaseDeDatos()
    {
        await using var context = CreateContext();
        var categoria = CreateCategoria("Tablets");
        context.Categorias.Add(categoria);
        await context.SaveChangesAsync();
        var service = new ProductoService(context);
        var producto = CreateProducto("iPad Air", categoria);

        await service.AgregarAsync(producto);

        var guardado = await context.Productos.SingleAsync();
        Assert.Equal("iPad Air", guardado.Nombre);
        Assert.True(guardado.ID > 0);
    }

    [Fact]
    public async Task Editar_ConProductoExistente_ActualizaTodosLosCampos()
    {
        var databaseName = Guid.NewGuid().ToString();
        int productoId;
        int otraCategoriaId;
        await using (var seedContext = CreateContext(databaseName))
        {
            var original = CreateProducto("Original", CreateCategoria("Audio"));
            var otraCategoria = CreateCategoria("Notebooks");
            seedContext.Productos.Add(original);
            seedContext.Categorias.Add(otraCategoria);
            await seedContext.SaveChangesAsync();
            productoId = original.ID;
            otraCategoriaId = otraCategoria.ID;
        }

        await using var context = CreateContext(databaseName);
        var service = new ProductoService(context);
        var cambios = new Producto
        {
            ID = productoId,
            Nombre = "Editado",
            Descripcion = "Descripción editada",
            ImagenUrl = "nueva.jpg",
            Precio = 99.5m,
            Stock = 3,
            Estado = "Agotado",
            CategoriaId = otraCategoriaId
        };

        var result = await service.EditarAsync(cambios);

        Assert.True(result);
        var persistido = await context.Productos.AsNoTracking().SingleAsync();
        Assert.Equal("Editado", persistido.Nombre);
        Assert.Equal("Descripción editada", persistido.Descripcion);
        Assert.Equal("nueva.jpg", persistido.ImagenUrl);
        Assert.Equal(99.5m, persistido.Precio);
        Assert.Equal(3, persistido.Stock);
        Assert.Equal("Agotado", persistido.Estado);
        Assert.Equal(otraCategoriaId, persistido.CategoriaId);
    }

    [Fact]
    public async Task Editar_ConProductoInexistente_DevuelveFalse()
    {
        await using var context = CreateContext();
        var service = new ProductoService(context);

        var result = await service.EditarAsync(new Producto { ID = 999, Nombre = "Fantasma" });

        Assert.False(result);
        Assert.Empty(context.Productos);
    }

    [Fact]
    public async Task Eliminar_ConProductoExistente_LoEliminaYDevuelveTrue()
    {
        var databaseName = Guid.NewGuid().ToString();
        int productoId;
        await using (var seedContext = CreateContext(databaseName))
        {
            var producto = CreateProducto("Borrar", CreateCategoria("Audio"));
            seedContext.Productos.Add(producto);
            await seedContext.SaveChangesAsync();
            productoId = producto.ID;
        }

        await using var context = CreateContext(databaseName);
        var service = new ProductoService(context);

        var result = await service.EliminarAsync(productoId);

        Assert.True(result);
        Assert.Empty(context.Productos);
    }

    [Fact]
    public async Task Eliminar_ConProductoInexistente_DevuelveFalse()
    {
        await using var context = CreateContext();
        var service = new ProductoService(context);

        var result = await service.EliminarAsync(999);

        Assert.False(result);
    }

    private static TechStoreContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<TechStoreContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new TechStoreContext(options);
    }

    private static Categoria CreateCategoria(string nombre)
    {
        return new Categoria { Nombre = nombre, Descripcion = $"Categoría de {nombre}." };
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
