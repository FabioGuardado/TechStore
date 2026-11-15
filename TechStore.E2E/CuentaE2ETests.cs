using Microsoft.Playwright;

namespace TechStore.E2E;

[Collection(E2ECollection.Nombre)]
[Trait("Category", "E2E")]
public class CuentaE2ETests
{
    private const string Password = "Clave123!";
    private readonly E2EFixture _fixture;

    public CuentaE2ETests(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Registro_ConDatosValidos_PermiteIniciarYCerrarSesion()
    {
        var usuario = $"cliente{Guid.NewGuid():N}"[..15];
        await using var context = await _fixture.NuevoContextoAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync("/Cuenta/Registro");
        await page.Locator("#NombreUsuario").FillAsync(usuario);
        await page.Locator("#Email").FillAsync($"{usuario}@correo.com");
        await page.Locator("#Password").FillAsync(Password);
        await page.Locator("#ConfirmarPassword").FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Crear cuenta" }).ClickAsync();
        await page.WaitForURLAsync("**/Cuenta/Login");
        await page.IniciarSesionAsync(usuario, Password);

        await Assertions.Expect(page.Locator(".nav-link.dropdown-toggle")).ToContainTextAsync(usuario);

        await page.CerrarSesionAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Iniciar sesión" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Login_ConContrasenaIncorrecta_MuestraElMensajeDeError()
    {
        var usuario = $"cliente{Guid.NewGuid():N}"[..15];
        await _fixture.CrearUsuarioAsync(usuario, Password, "Cliente");
        await using var context = await _fixture.NuevoContextoAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync("/Cuenta/Login");
        await page.Locator("#UsuarioOCorreo").FillAsync(usuario);
        await page.Locator("#Password").FillAsync("Incorrecta1!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Ingresar" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Usuario o contraseña incorrectos.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Productos_SinSesion_RedirigeAlLoginYLuegoRegresaAProductos()
    {
        var usuario = $"cliente{Guid.NewGuid():N}"[..15];
        await _fixture.CrearUsuarioAsync(usuario, Password, "Cliente");
        await using var context = await _fixture.NuevoContextoAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync("/Products");
        await page.WaitForURLAsync("**/Cuenta/Login**");
        await page.Locator("#UsuarioOCorreo").FillAsync(usuario);
        await page.Locator("#Password").FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Ingresar" }).ClickAsync();

        await page.WaitForURLAsync("**/Products");
        Assert.EndsWith("/Products", new Uri(page.Url).AbsolutePath);
    }
}
