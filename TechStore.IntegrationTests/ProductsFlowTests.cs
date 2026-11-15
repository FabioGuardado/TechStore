using System.Net;
using Microsoft.EntityFrameworkCore;

namespace TechStore.IntegrationTests;

public class ProductsFlowTests : IClassFixture<TechStoreFactory>
{
    private const string Password = "Clave123!";
    private readonly TechStoreFactory _factory;

    public ProductsFlowTests(TechStoreFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Home_ConProductos_MuestraElProductoEnLaPaginaDeInicio()
    {
        var nombre = $"Destacado{Guid.NewGuid():N}"[..20];
        await _factory.CrearProductoAsync(nombre, categoria: $"Cat{Guid.NewGuid():N}"[..10]);
        var client = _factory.CreateClientSinRedirecciones();

        var html = await client.GetStringAsync("/");

        Assert.Contains(nombre, html);
    }

    [Fact]
    public async Task Index_ConProductosYSesion_ListaLosProductos()
    {
        var nombre = $"Listado{Guid.NewGuid():N}"[..20];
        await _factory.CrearProductoAsync(nombre);
        var client = await CreateClienteAutenticadoAsync("Cliente");

        var html = await client.GetStringAsync("/Products");

        Assert.Contains(nombre, html);
    }

    [Fact]
    public async Task Index_ConRolCliente_NoMuestraElBotonAgregarProducto()
    {
        var client = await CreateClienteAutenticadoAsync("Cliente");

        var html = await client.GetStringAsync("/Products");

        Assert.DoesNotContain("Agregar producto", html);
    }

    [Fact]
    public async Task Index_ConRolVendedor_MuestraElBotonAgregarProducto()
    {
        var client = await CreateClienteAutenticadoAsync("Vendedor");

        var html = await client.GetStringAsync("/Products");

        Assert.Contains("Agregar producto", html);
    }

    [Fact]
    public async Task Details_ConProductoInexistente_DevuelveNotFound()
    {
        var client = await CreateClienteAutenticadoAsync("Cliente");

        var response = await client.GetAsync("/Products/Details/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Details_ConProductoExistente_MuestraSuDetalle()
    {
        var nombre = $"Detalle{Guid.NewGuid():N}"[..20];
        var productoId = await _factory.CrearProductoAsync(nombre);
        var client = await CreateClienteAutenticadoAsync("Cliente");

        var html = await client.GetStringAsync($"/Products/Details/{productoId}");

        Assert.Contains(nombre, html);
    }

    [Fact]
    public async Task Agregar_ConDatosValidos_GuardaElProductoYRedirigeAlIndice()
    {
        var nombre = $"Nuevo{Guid.NewGuid():N}"[..20];
        var categoriaId = await _factory.CrearCategoriaAsync($"Cat{Guid.NewGuid():N}"[..10]);
        var client = await CreateClienteAutenticadoAsync("Vendedor");

        var response = await client.PostFormularioAsync("/Products/Agregar", "/Products/Agregar", new Dictionary<string, string>
        {
            ["Nombre"] = nombre,
            ["Descripcion"] = "Producto creado desde una prueba de integración.",
            ["Precio"] = "250.50",
            ["Stock"] = "7",
            ["Estado"] = "Disponible",
            ["CategoriaId"] = categoriaId.ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Products", response.Ruta());
        var guardado = await _factory.ConsultarAsync(c => c.Productos.SingleAsync(p => p.Nombre == nombre));
        Assert.Equal(250.50m, guardado.Precio);
        Assert.Equal(7, guardado.Stock);
        Assert.Equal(categoriaId, guardado.CategoriaId);
    }

    [Fact]
    public async Task Agregar_ConDatosInvalidos_NoGuardaYMuestraLosErroresDeValidacion()
    {
        var nombre = $"Invalido{Guid.NewGuid():N}"[..20];
        var client = await CreateClienteAutenticadoAsync("Vendedor");

        var response = await client.PostFormularioAsync("/Products/Agregar", "/Products/Agregar", new Dictionary<string, string>
        {
            ["Nombre"] = nombre,
            ["Descripcion"] = "",
            ["Precio"] = "0",
            ["Stock"] = "-1",
            ["Estado"] = "Disponible",
            ["CategoriaId"] = "1"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.LeerHtmlAsync();
        Assert.Contains("La descripción es obligatoria.", html);
        Assert.Contains("El precio debe ser mayor a 0.", html);
        Assert.Contains("El stock no puede ser negativo.", html);
        Assert.Equal(0, await _factory.ConsultarAsync(c => Task.FromResult(c.Productos.Count(p => p.Nombre == nombre))));
    }

    [Fact]
    public async Task Editar_ConDatosValidos_ActualizaElProductoYRedirigeAlIndice()
    {
        var productoId = await _factory.CrearProductoAsync($"Original{Guid.NewGuid():N}"[..20]);
        var original = await _factory.ConsultarAsync(c => c.Productos.AsNoTracking().SingleAsync(p => p.ID == productoId));
        var nombreEditado = $"Editado{Guid.NewGuid():N}"[..20];
        var client = await CreateClienteAutenticadoAsync("Vendedor");

        var response = await client.PostFormularioAsync($"/Products/Editar/{productoId}", $"/Products/Editar/{productoId}", new Dictionary<string, string>
        {
            ["ID"] = productoId.ToString(),
            ["Nombre"] = nombreEditado,
            ["Descripcion"] = "Descripción editada.",
            ["Precio"] = "999.99",
            ["Stock"] = "1",
            ["Estado"] = "Agotado",
            ["CategoriaId"] = original.CategoriaId.ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var editado = await _factory.ConsultarAsync(c => c.Productos.AsNoTracking().SingleAsync(p => p.ID == productoId));
        Assert.Equal(nombreEditado, editado.Nombre);
        Assert.Equal(999.99m, editado.Precio);
        Assert.Equal("Agotado", editado.Estado);
    }

    [Fact]
    public async Task Categories_ConProductos_AgrupaLosProductosPorCategoria()
    {
        var categoria = $"Grupo{Guid.NewGuid():N}"[..12];
        var nombre = $"EnGrupo{Guid.NewGuid():N}"[..20];
        await _factory.CrearProductoAsync(nombre, categoria);
        var client = await CreateClienteAutenticadoAsync("Cliente");

        var html = await client.GetStringAsync("/Categories");

        Assert.Contains(categoria, html);
        Assert.Contains(nombre, html);
    }

    [Fact]
    public async Task Details_ConProductoInexistente_MuestraLaPaginaAmigableDeNoEncontrado()
    {
        var client = await CreateClienteAutenticadoAsync("Cliente");

        var response = await client.GetAsync("/Products/Details/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.LeerHtmlAsync();
        Assert.Contains("No encontramos esa página", html);
        Assert.Contains("Volver al inicio", html);
        Assert.DoesNotContain("Development Mode", html);
    }

    [Fact]
    public async Task RutaInexistente_MuestraLaPaginaAmigableDeNoEncontrado()
    {
        var client = _factory.CreateClientSinRedirecciones();

        var response = await client.GetAsync("/ruta-inexistente");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("No encontramos esa página", await response.LeerHtmlAsync());
    }

    [Fact]
    public async Task Agregar_ConCategoriaInexistente_NoGuardaYMuestraElError()
    {
        var nombre = $"SinCat{Guid.NewGuid():N}"[..20];
        var client = await CreateClienteAutenticadoAsync("Vendedor");

        var response = await client.PostFormularioAsync("/Products/Agregar", "/Products/Agregar", new Dictionary<string, string>
        {
            ["Nombre"] = nombre,
            ["Descripcion"] = "Producto con categoría manipulada.",
            ["Precio"] = "10",
            ["Stock"] = "1",
            ["Estado"] = "Disponible",
            ["CategoriaId"] = "99999"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("La categoría seleccionada no existe.", await response.LeerHtmlAsync());
        Assert.Equal(0, await _factory.ConsultarAsync(c => Task.FromResult(c.Productos.Count(p => p.Nombre == nombre))));
    }

    [Fact]
    public async Task Agregar_ConArchivoNoPermitido_MuestraElErrorYNoCreaElProducto()
    {
        var nombre = $"Exe{Guid.NewGuid():N}"[..20];
        var categoriaId = await _factory.CrearCategoriaAsync($"Cat{Guid.NewGuid():N}"[..10]);
        var client = await CreateClienteAutenticadoAsync("Vendedor");
        var token = await client.ObtenerTokenAsync("/Products/Agregar");
        using var contenido = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(nombre), "Nombre" },
            { new StringContent("Producto con archivo no permitido."), "Descripcion" },
            { new StringContent("10"), "Precio" },
            { new StringContent("1"), "Stock" },
            { new StringContent("Disponible"), "Estado" },
            { new StringContent(categoriaId.ToString()), "CategoriaId" },
            { new ByteArrayContent(new byte[] { 1, 2, 3 }), "imageFile", "virus.exe" }
        };

        var response = await client.PostAsync("/Products/Agregar", contenido);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("La imagen debe ser", await response.LeerHtmlAsync());
        Assert.Equal(0, await _factory.ConsultarAsync(c => Task.FromResult(c.Productos.Count(p => p.Nombre == nombre))));
    }

    private async Task<HttpClient> CreateClienteAutenticadoAsync(string rol)
    {
        var usuario = $"{rol.ToLowerInvariant()}{Guid.NewGuid():N}"[..20];
        await _factory.CrearUsuarioAsync(usuario, Password, rol);
        var client = _factory.CreateClientSinRedirecciones();
        await client.IniciarSesionAsync(usuario, Password);
        return client;
    }
}
