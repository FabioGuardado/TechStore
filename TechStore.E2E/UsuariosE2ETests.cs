using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace TechStore.E2E;

[Collection(E2ECollection.Nombre)]
[Trait("Category", "E2E")]
public class UsuariosE2ETests
{
    private const string Password = "Clave123!";
    private readonly E2EFixture _fixture;

    public UsuariosE2ETests(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Administrador_CambiaElRolDeUnUsuario_ElRolQuedaGuardado()
    {
        var usuario = $"usuario{Guid.NewGuid():N}"[..15];
        await _fixture.CrearUsuarioAsync(usuario, Password, "Cliente");
        await using var context = await _fixture.NuevoContextoAsync();
        var page = await context.NewPageAsync();
        await page.IniciarSesionAsync(E2EFixture.AdminUsuario, E2EFixture.AdminPassword);

        await page.GotoAsync($"/Usuarios?buscar={usuario}");
        await page.GetByLabel($"Rol de {usuario}").SelectOptionAsync("Vendedor");
        await page.GetByTitle("Guardar rol").ClickAsync();

        await page.WaitForURLAsync("**/Usuarios");
        await Assertions.Expect(page.GetByText($"El usuario {usuario} ahora tiene el rol Vendedor.")).ToBeVisibleAsync();
        Assert.Equal("Vendedor", await ObtenerRolAsync(usuario));
    }

    [Fact]
    public async Task Administrador_BloqueaAUnUsuario_ElUsuarioNoPuedeIniciarSesion()
    {
        var usuario = $"usuario{Guid.NewGuid():N}"[..15];
        await _fixture.CrearUsuarioAsync(usuario, Password, "Cliente");
        await using var adminContext = await _fixture.NuevoContextoAsync();
        var adminPage = await adminContext.NewPageAsync();
        await adminPage.IniciarSesionAsync(E2EFixture.AdminUsuario, E2EFixture.AdminPassword);

        await adminPage.GotoAsync($"/Usuarios?buscar={usuario}");
        await adminPage.GetByRole(AriaRole.Button, new() { Name = "Bloquear" }).ClickAsync();
        await adminPage.ConfirmarAlertaAsync();
        await adminPage.WaitForURLAsync("**/Usuarios");

        await using var usuarioContext = await _fixture.NuevoContextoAsync();
        var usuarioPage = await usuarioContext.NewPageAsync();
        await usuarioPage.GotoAsync("/Cuenta/Login");
        await usuarioPage.Locator("#UsuarioOCorreo").FillAsync(usuario);
        await usuarioPage.Locator("#Password").FillAsync(Password);
        await usuarioPage.GetByRole(AriaRole.Button, new() { Name = "Ingresar" }).ClickAsync();

        await Assertions.Expect(usuarioPage.GetByText("Su cuenta fue bloqueada por un administrador.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Administrador_NoPuedeCambiarSuPropioRol_ElSelectorNoEstaDisponible()
    {
        await using var context = await _fixture.NuevoContextoAsync();
        var page = await context.NewPageAsync();
        await page.IniciarSesionAsync(E2EFixture.AdminUsuario, E2EFixture.AdminPassword);

        await page.GotoAsync($"/Usuarios?buscar={E2EFixture.AdminUsuario}");

        await Assertions.Expect(page.GetByLabel($"Rol de {E2EFixture.AdminUsuario}")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByText("Tú")).ToBeVisibleAsync();
    }

    private async Task<string?> ObtenerRolAsync(string usuario)
    {
        return await _fixture.ConsultarAsync(async c =>
        {
            var id = c.Users.Single(u => u.UserName == usuario).Id;
            var rolId = c.UserRoles.Single(ur => ur.UserId == id).RoleId;
            return await Task.FromResult(c.Roles.Single(r => r.Id == rolId).Name);
        });
    }
}
