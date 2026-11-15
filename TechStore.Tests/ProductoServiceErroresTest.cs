using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TechStore.Data;
using TechStore.Models;
using TechStore.Services;

namespace TechStore.Tests;

public class ProductoServiceErroresTests
{
    [Fact]
    public async Task Agregar_CuandoLaBaseDeDatosFalla_DevuelveFalseYRegistraElError()
    {
        var logger = new Mock<ILogger<ProductoService>>();
        await using var context = CreateContext(new DbUpdateException("FK"));
        var service = new ProductoService(context, logger.Object);

        var resultado = await service.AgregarAsync(CreateProducto("Falla"));

        Assert.False(resultado);
        VerifyError(logger);
    }

    [Fact]
    public async Task Editar_CuandoLaBaseDeDatosFalla_DevuelveFalse()
    {
        var logger = new Mock<ILogger<ProductoService>>();
        var nombre = Guid.NewGuid().ToString();
        await Sembrar(nombre);
        await using var context = CreateContext(new DbUpdateException("FK"), nombre);
        var service = new ProductoService(context, logger.Object);
        var editado = CreateProducto("Editado");
        editado.ID = 1;

        var resultado = await service.EditarAsync(editado);

        Assert.False(resultado);
        VerifyError(logger);
    }

    [Fact]
    public async Task Editar_CuandoElProductoSeBorraEnMedio_DevuelveFalse()
    {
        var logger = new Mock<ILogger<ProductoService>>();
        var nombre = Guid.NewGuid().ToString();
        await Sembrar(nombre);
        await using var context = CreateContext(new DbUpdateConcurrencyException("borrado"), nombre);
        var service = new ProductoService(context, logger.Object);
        var editado = CreateProducto("Editado");
        editado.ID = 1;

        var resultado = await service.EditarAsync(editado);

        Assert.False(resultado);
        VerifyError(logger);
    }

    [Fact]
    public async Task Eliminar_CuandoLaBaseDeDatosFalla_DevuelveFalse()
    {
        var logger = new Mock<ILogger<ProductoService>>();
        var nombre = Guid.NewGuid().ToString();
        await Sembrar(nombre);
        await using var context = CreateContext(new DbUpdateException("FK"), nombre);
        var service = new ProductoService(context, logger.Object);

        var resultado = await service.EliminarAsync(1);

        Assert.False(resultado);
        VerifyError(logger);
    }

    private static async Task Sembrar(string baseDeDatos)
    {
        await using var context = CreateContext(null, baseDeDatos);
        context.Productos.Add(CreateProducto("Original"));
        await context.SaveChangesAsync();
    }

    private static void VerifyError(Mock<ILogger<ProductoService>> logger)
    {
        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    private static Producto CreateProducto(string nombre)
    {
        return new Producto
        {
            Nombre = nombre,
            Descripcion = $"Descripción de {nombre}.",
            Precio = 100m,
            Stock = 5,
            Estado = "Disponible",
            Categoria = new Categoria { Nombre = "Audio" }
        };
    }

    private static TechStoreContext CreateContext(Exception? errorAlGuardar, string? baseDeDatos = null)
    {
        var options = new DbContextOptionsBuilder<TechStoreContext>()
            .UseInMemoryDatabase(baseDeDatos ?? Guid.NewGuid().ToString())
            .Options;

        return errorAlGuardar == null ? new TechStoreContext(options) : new ContextoQueFalla(options, errorAlGuardar);
    }

    // Simula un fallo de la BD (FK, concurrencia) al guardar, algo que EF InMemory no produce.
    private sealed class ContextoQueFalla : TechStoreContext
    {
        private readonly Exception _error;

        public ContextoQueFalla(DbContextOptions<TechStoreContext> options, Exception error) : base(options)
        {
            _error = error;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw _error;
        }
    }
}
