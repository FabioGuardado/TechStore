using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TechStore.Services;

namespace TechStore.Tests;

public class ImagenServiceTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true);
    }

    [Theory]
    [InlineData("foto.jpg")]
    [InlineData("foto.jpeg")]
    [InlineData("foto.png")]
    [InlineData("foto.webp")]
    [InlineData("foto.gif")]
    [InlineData("FOTO.PNG")]
    public async Task Guardar_ConExtensionPermitida_GuardaElArchivoEnMinusculas(string nombre)
    {
        var service = CreateService();

        var resultado = await service.GuardarAsync(CreateArchivo(nombre, 10).Object);

        Assert.True(resultado.Exito);
        Assert.EndsWith(Path.GetExtension(nombre).ToLowerInvariant(), resultado.NombreArchivo);
        Assert.True(File.Exists(Path.Combine(_webRoot, "images", resultado.NombreArchivo!)));
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("shell.php")]
    [InlineData("dibujo.svg")]
    [InlineData("sinextension")]
    public async Task Guardar_ConExtensionNoPermitida_DevuelveErrorYNoGuarda(string nombre)
    {
        var service = CreateService();

        var resultado = await service.GuardarAsync(CreateArchivo(nombre, 10).Object);

        Assert.False(resultado.Exito);
        Assert.NotNull(resultado.Error);
        Assert.False(Directory.Exists(Path.Combine(_webRoot, "images")));
    }

    [Fact]
    public async Task Guardar_ConArchivoMayorA2MB_DevuelveError()
    {
        var service = CreateService();

        var resultado = await service.GuardarAsync(CreateArchivo("grande.png", ImagenService.TamanoMaximoBytes + 1).Object);

        Assert.False(resultado.Exito);
        Assert.Equal("La imagen no puede superar los 2 MB.", resultado.Error);
    }

    [Fact]
    public async Task Guardar_CuandoLaCopiaFalla_DevuelveErrorSinLanzarYNoDejaArchivo()
    {
        var service = CreateService();
        var archivo = CreateArchivo("foto.png", 10);
        archivo.Setup(a => a.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("disco lleno"));

        var resultado = await service.GuardarAsync(archivo.Object);

        Assert.False(resultado.Exito);
        Assert.Equal("No se pudo guardar la imagen. Intenta de nuevo.", resultado.Error);
        Assert.Empty(Directory.GetFiles(Path.Combine(_webRoot, "images")));
    }

    [Fact]
    public void Eliminar_ConArchivoInexistente_DevuelveFalseSinLanzar()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "images"));

        Assert.False(CreateService().Eliminar("no-existe.png"));
    }

    [Fact]
    public void Eliminar_ConArchivoExistente_LoBorra()
    {
        var images = Path.Combine(_webRoot, "images");
        Directory.CreateDirectory(images);
        File.WriteAllText(Path.Combine(images, "a.png"), "x");

        Assert.True(CreateService().Eliminar("/images/a.png"));
        Assert.False(File.Exists(Path.Combine(images, "a.png")));
    }

    [Fact]
    public void Eliminar_CuandoElArchivoEstaEnUso_DevuelveFalseSinLanzar()
    {
        var images = Path.Combine(_webRoot, "images");
        Directory.CreateDirectory(images);
        var ruta = Path.Combine(images, "bloqueada.png");
        using var abierto = new FileStream(ruta, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        Assert.False(CreateService().Eliminar("bloqueada.png"));
    }

    private ImagenService CreateService()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(_webRoot);
        return new ImagenService(env.Object, NullLogger<ImagenService>.Instance);
    }

    private static Mock<IFormFile> CreateArchivo(string nombre, long length)
    {
        var archivo = new Mock<IFormFile>();
        archivo.Setup(a => a.FileName).Returns(nombre);
        archivo.Setup(a => a.Length).Returns(length);
        archivo.Setup(a => a.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return archivo;
    }
}
