using System.Net;

namespace TechStore.IntegrationTests;

public class AutorizacionTests : IClassFixture<TechStoreFactory>
{
    private const string Password = "Clave123!";
    private readonly TechStoreFactory _factory;

    public AutorizacionTests(TechStoreFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/About")]
    [InlineData("/Contact")]
    [InlineData("/Cuenta/Login")]
    [InlineData("/Cuenta/Registro")]
    public async Task Get_ConPaginaPublicaYSinSesion_DevuelveOk(string url)
    {
        var client = _factory.CreateClientSinRedirecciones();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/Products")]
    [InlineData("/Categories")]
    [InlineData("/Usuarios")]
    public async Task Get_ConPaginaProtegidaYSinSesion_RedirigeAlLoginConReturnUrl(string url)
    {
        var client = _factory.CreateClientSinRedirecciones();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Cuenta/Login?ReturnUrl={Uri.EscapeDataString(url)}", response.Ruta());
    }

    [Theory]
    [InlineData("Cliente")]
    [InlineData("Vendedor")]
    [InlineData("Administrador")]
    public async Task GetProducts_ConCualquierRol_DevuelveOk(string rol)
    {
        var client = await CreateClienteAutenticadoAsync(rol);

        var response = await client.GetAsync("/Products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAgregar_ConRolCliente_RedirigeAAccesoDenegado()
    {
        var client = await CreateClienteAutenticadoAsync("Cliente");

        var response = await client.GetAsync("/Products/Agregar");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Cuenta/AccesoDenegado", response.Ruta());
    }

    [Theory]
    [InlineData("Vendedor")]
    [InlineData("Administrador")]
    public async Task GetAgregar_ConRolVendedorOAdministrador_DevuelveOk(string rol)
    {
        var client = await CreateClienteAutenticadoAsync(rol);

        var response = await client.GetAsync("/Products/Agregar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Cliente")]
    [InlineData("Vendedor")]
    public async Task GetUsuarios_ConRolDistintoDeAdministrador_RedirigeAAccesoDenegado(string rol)
    {
        var client = await CreateClienteAutenticadoAsync(rol);

        var response = await client.GetAsync("/Usuarios");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Cuenta/AccesoDenegado", response.Ruta());
    }

    [Fact]
    public async Task GetUsuarios_ConRolAdministrador_DevuelveOk()
    {
        var client = await CreateClienteAutenticadoAsync("Administrador");

        var response = await client.GetAsync("/Usuarios");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostEliminar_ConRolVendedor_RedirigeAAccesoDenegadoYNoEliminaElProducto()
    {
        var productoId = await _factory.CrearProductoAsync($"Protegido {Guid.NewGuid()}");
        var client = await CreateClienteAutenticadoAsync("Vendedor");

        var response = await client.PostFormularioAsync(
            $"/Products/Eliminar/{productoId}",
            "/Products/Agregar",
            new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Cuenta/AccesoDenegado", response.Ruta());
        Assert.Equal(1, await _factory.ConsultarAsync(c => Task.FromResult(c.Productos.Count(p => p.ID == productoId))));
    }

    [Fact]
    public async Task PostEliminar_ConRolAdministrador_EliminaElProductoYRedirigeAlIndice()
    {
        var productoId = await _factory.CrearProductoAsync($"Borrable {Guid.NewGuid()}");
        var client = await CreateClienteAutenticadoAsync("Administrador");

        var response = await client.PostFormularioAsync(
            $"/Products/Eliminar/{productoId}",
            $"/Products/Details/{productoId}",
            new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Products", response.Ruta());
        Assert.Equal(0, await _factory.ConsultarAsync(c => Task.FromResult(c.Productos.Count(p => p.ID == productoId))));
    }

    [Fact]
    public async Task PostEliminar_SinTokenAntifalsificacion_DevuelveBadRequest()
    {
        var productoId = await _factory.CrearProductoAsync($"Sin token {Guid.NewGuid()}");
        var client = await CreateClienteAutenticadoAsync("Administrador");

        var response = await client.PostAsync($"/Products/Eliminar/{productoId}", new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, await _factory.ConsultarAsync(c => Task.FromResult(c.Productos.Count(p => p.ID == productoId))));
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
