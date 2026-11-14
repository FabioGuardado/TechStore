using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace TechStore.IntegrationTests;

public class CuentaFlowTests : IClassFixture<TechStoreFactory>
{
    private const string Password = "Clave123!";
    private readonly TechStoreFactory _factory;

    public CuentaFlowTests(TechStoreFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Registro_ConDatosValidos_CreaUsuarioConRolClienteYRedirigeALogin()
    {
        var usuario = $"nuevo{Guid.NewGuid():N}"[..15];
        var client = _factory.CreateClientSinRedirecciones();

        var response = await client.PostFormularioAsync("/Cuenta/Registro", "/Cuenta/Registro", new Dictionary<string, string>
        {
            ["NombreUsuario"] = usuario,
            ["Email"] = $"{usuario}@correo.com",
            ["Password"] = Password,
            ["ConfirmarPassword"] = Password
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Cuenta/Login", response.Ruta());
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var creado = await userManager.FindByNameAsync(usuario);
        Assert.NotNull(creado);
        Assert.Contains("Cliente", await userManager.GetRolesAsync(creado));
    }

    [Fact]
    public async Task Registro_ConContrasenaDebil_DevuelveLaVistaConErroresEnEspanol()
    {
        var usuario = $"debil{Guid.NewGuid():N}"[..15];
        var client = _factory.CreateClientSinRedirecciones();

        var response = await client.PostFormularioAsync("/Cuenta/Registro", "/Cuenta/Registro", new Dictionary<string, string>
        {
            ["NombreUsuario"] = usuario,
            ["Email"] = $"{usuario}@correo.com",
            ["Password"] = "abc",
            ["ConfirmarPassword"] = "abc"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.LeerHtmlAsync();
        Assert.Contains("La contraseña debe tener al menos", html);
    }

    [Fact]
    public async Task Registro_ConUsuarioDuplicado_DevuelveLaVistaConElError()
    {
        var usuario = $"dup{Guid.NewGuid():N}"[..15];
        await _factory.CrearUsuarioAsync(usuario, Password, "Cliente");
        var client = _factory.CreateClientSinRedirecciones();

        var response = await client.PostFormularioAsync("/Cuenta/Registro", "/Cuenta/Registro", new Dictionary<string, string>
        {
            ["NombreUsuario"] = usuario,
            ["Email"] = $"otro{usuario}@correo.com",
            ["Password"] = Password,
            ["ConfirmarPassword"] = Password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ya está registrado", await response.LeerHtmlAsync());
    }

    [Fact]
    public async Task Login_ConCredencialesValidas_RedirigeAHome()
    {
        var usuario = $"login{Guid.NewGuid():N}"[..15];
        await _factory.CrearUsuarioAsync(usuario, Password, "Cliente");
        var client = _factory.CreateClientSinRedirecciones();

        var response = await client.PostFormularioAsync("/Cuenta/Login", "/Cuenta/Login", new Dictionary<string, string>
        {
            ["UsuarioOCorreo"] = usuario,
            ["Password"] = Password
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Ruta());
    }

    [Fact]
    public async Task Login_ConCorreoElectronico_RedirigeAHome()
    {
        var usuario = $"mail{Guid.NewGuid():N}"[..15];
        await _factory.CrearUsuarioAsync(usuario, Password, "Cliente");
        var client = _factory.CreateClientSinRedirecciones();

        var response = await client.PostFormularioAsync("/Cuenta/Login", "/Cuenta/Login", new Dictionary<string, string>
        {
            ["UsuarioOCorreo"] = $"{usuario}@techstore.com",
            ["Password"] = Password
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Login_ConReturnUrlLocal_RedirigeALaUrlSolicitada()
    {
        var usuario = $"ret{Guid.NewGuid():N}"[..15];
        await _factory.CrearUsuarioAsync(usuario, Password, "Cliente");
        var client = _factory.CreateClientSinRedirecciones();

        var response = await client.PostFormularioAsync("/Cuenta/Login", "/Cuenta/Login", new Dictionary<string, string>
        {
            ["UsuarioOCorreo"] = usuario,
            ["Password"] = Password,
            ["ReturnUrl"] = "/Products"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Products", response.Ruta());
    }

    [Fact]
    public async Task Login_ConReturnUrlExterno_RedirigeAHome()
    {
        var usuario = $"ext{Guid.NewGuid():N}"[..15];
        await _factory.CrearUsuarioAsync(usuario, Password, "Cliente");
        var client = _factory.CreateClientSinRedirecciones();

        var response = await client.PostFormularioAsync("/Cuenta/Login", "/Cuenta/Login", new Dictionary<string, string>
        {
            ["UsuarioOCorreo"] = usuario,
            ["Password"] = Password,
            ["ReturnUrl"] = "https://sitio-malicioso.com"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Ruta());
    }

    [Fact]
    public async Task Login_ConContrasenaIncorrecta_DevuelveLaVistaConMensajeGenerico()
    {
        var usuario = $"mal{Guid.NewGuid():N}"[..15];
        await _factory.CrearUsuarioAsync(usuario, Password, "Cliente");
        var client = _factory.CreateClientSinRedirecciones();

        var response = await client.PostFormularioAsync("/Cuenta/Login", "/Cuenta/Login", new Dictionary<string, string>
        {
            ["UsuarioOCorreo"] = usuario,
            ["Password"] = "Incorrecta1!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Usuario o contraseña incorrectos.", await response.LeerHtmlAsync());
    }

    [Fact]
    public async Task Login_ConCincoIntentosFallidos_BloqueaLaCuentaAunqueLaContrasenaSeaCorrecta()
    {
        var usuario = $"lock{Guid.NewGuid():N}"[..15];
        await _factory.CrearUsuarioAsync(usuario, Password, "Cliente");
        var client = _factory.CreateClientSinRedirecciones();
        for (var i = 0; i < 5; i++)
        {
            await client.PostFormularioAsync("/Cuenta/Login", "/Cuenta/Login", new Dictionary<string, string>
            {
                ["UsuarioOCorreo"] = usuario,
                ["Password"] = "Incorrecta1!"
            });
        }

        var response = await client.PostFormularioAsync("/Cuenta/Login", "/Cuenta/Login", new Dictionary<string, string>
        {
            ["UsuarioOCorreo"] = usuario,
            ["Password"] = Password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cuenta bloqueada por 5 intentos fallidos", await response.LeerHtmlAsync());
    }

    [Fact]
    public async Task Login_ConUsuarioAutenticado_RedirigeAHomeAlVisitarElLogin()
    {
        var usuario = $"auth{Guid.NewGuid():N}"[..15];
        await _factory.CrearUsuarioAsync(usuario, Password, "Cliente");
        var client = _factory.CreateClientSinRedirecciones();
        await client.IniciarSesionAsync(usuario, Password);

        var response = await client.GetAsync("/Cuenta/Login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Ruta());
    }

    [Fact]
    public async Task Logout_ConSesionIniciada_CierraLaSesionYBloqueaLasPaginasProtegidas()
    {
        var usuario = $"out{Guid.NewGuid():N}"[..15];
        await _factory.CrearUsuarioAsync(usuario, Password, "Cliente");
        var client = _factory.CreateClientSinRedirecciones();
        await client.IniciarSesionAsync(usuario, Password);

        var logout = await client.PostFormularioAsync("/Cuenta/Logout", "/Products", new Dictionary<string, string>());
        var despues = await client.GetAsync("/Products");

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, despues.StatusCode);
        Assert.StartsWith("/Cuenta/Login", despues.Ruta());
    }

    [Fact]
    public async Task Login_ConAdministradorInicial_AccedeAlListadoDeUsuarios()
    {
        var client = _factory.CreateClientSinRedirecciones();
        await client.IniciarSesionAsync(TechStoreFactory.AdminUsuario, TechStoreFactory.AdminPassword);

        var response = await client.GetAsync("/Usuarios");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
