using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace TechStore.E2E;

[Collection(E2ECollection.Nombre)]
[Trait("Category", "E2E")]
public class ProductosE2ETests
{
    private const string Password = "Clave123!";
    private readonly E2EFixture _fixture;

    public ProductosE2ETests(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Vendedor_AgregaUnProducto_ApareceEnElListado()
    {
        var vendedor = $"vendedor{Guid.NewGuid():N}"[..15];
        var categoria = $"Cat{Guid.NewGuid():N}"[..10];
        var nombre = $"Nuevo{Guid.NewGuid():N}"[..15];
        await _fixture.CrearUsuarioAsync(vendedor, Password, "Vendedor");
        await _fixture.CrearProductoAsync($"Base{Guid.NewGuid():N}"[..15], categoria);
        await using var context = await _fixture.NuevoContextoAsync();
        var page = await context.NewPageAsync();
        await page.IniciarSesionAsync(vendedor, Password);

        await page.GotoAsync("/Products/Agregar");
        await page.Locator("#Nombre").FillAsync(nombre);
        await page.Locator("#Descripcion").FillAsync("Producto creado desde una prueba E2E.");
        await page.Locator("#Precio").FillAsync("250.5");
        await page.Locator("#Stock").FillAsync("7");
        await page.Locator("#CategoriaId").SelectOptionAsync(new SelectOptionValue { Label = categoria });
        await page.GetByRole(AriaRole.Button, new() { Name = "Guardar producto" }).ClickAsync();

        await page.WaitForURLAsync("**/Products");
        await Assertions.Expect(page.GetByText(nombre)).ToBeVisibleAsync();
        Assert.Equal(1, await _fixture.ConsultarAsync(c => Task.FromResult(c.Productos.Count(p => p.Nombre == nombre))));
    }

    [Fact]
    public async Task Vendedor_ConFormularioIncompleto_VeLosErroresYNoSeGuarda()
    {
        var vendedor = $"vendedor{Guid.NewGuid():N}"[..15];
        var nombre = $"Incompleto{Guid.NewGuid():N}"[..15];
        await _fixture.CrearUsuarioAsync(vendedor, Password, "Vendedor");
        await using var context = await _fixture.NuevoContextoAsync();
        var page = await context.NewPageAsync();
        await page.IniciarSesionAsync(vendedor, Password);

        await page.GotoAsync("/Products/Agregar");
        await page.Locator("#Nombre").FillAsync(nombre);
        await page.GetByRole(AriaRole.Button, new() { Name = "Guardar producto" }).ClickAsync();

        await Assertions.Expect(page.GetByText("La descripción es obligatoria.")).ToBeVisibleAsync();
        Assert.EndsWith("/Products/Agregar", new Uri(page.Url).AbsolutePath);
        Assert.Equal(0, await _fixture.ConsultarAsync(c => Task.FromResult(c.Productos.Count(p => p.Nombre == nombre))));
    }

    [Fact]
    public async Task Cliente_NoVeLasOpcionesDeGestionYNoPuedeAbrirElFormularioDeAgregar()
    {
        var cliente = $"cliente{Guid.NewGuid():N}"[..15];
        await _fixture.CrearUsuarioAsync(cliente, Password, "Cliente");
        await using var context = await _fixture.NuevoContextoAsync();
        var page = await context.NewPageAsync();
        await page.IniciarSesionAsync(cliente, Password);

        await page.GotoAsync("/Products");

        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Nuevo producto" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Agregar producto" })).ToHaveCountAsync(0);

        await page.GotoAsync("/Products/Agregar");

        await page.WaitForURLAsync("**/Cuenta/AccesoDenegado**");
    }

    [Fact]
    public async Task Administrador_EliminaUnProductoConConfirmacion_DesapareceDelListado()
    {
        var nombre = $"Borrar{Guid.NewGuid():N}"[..15];
        await _fixture.CrearProductoAsync(nombre, $"Cat{Guid.NewGuid():N}"[..10]);
        await using var context = await _fixture.NuevoContextoAsync();
        var page = await context.NewPageAsync();
        await page.IniciarSesionAsync(E2EFixture.AdminUsuario, E2EFixture.AdminPassword);

        await page.GotoAsync("/Products");
        await page.GetByText(nombre).First.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Eliminar producto" }).ClickAsync();
        await page.ConfirmarAlertaAsync();

        await page.WaitForURLAsync("**/Products");
        await Assertions.Expect(page.GetByText(nombre)).ToHaveCountAsync(0);
        Assert.Equal(0, await _fixture.ConsultarAsync(c => Task.FromResult(c.Productos.Count(p => p.Nombre == nombre))));
    }
}
