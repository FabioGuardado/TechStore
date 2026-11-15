using Microsoft.Playwright;

namespace TechStore.E2E;

public static class PaginaExtensions
{
    public static async Task<IBrowserContext> NuevoContextoAsync(this E2EFixture fixture)
    {
        return await fixture.Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = fixture.BaseUrl });
    }

    public static async Task IniciarSesionAsync(this IPage page, string usuario, string password)
    {
        await page.GotoAsync("/Cuenta/Login");
        await page.Locator("#UsuarioOCorreo").FillAsync(usuario);
        await page.Locator("#Password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Ingresar" }).ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/Cuenta/Login"));
    }

    public static async Task CerrarSesionAsync(this IPage page)
    {
        await page.Locator(".nav-link.dropdown-toggle").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Cerrar sesión" }).ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/Cuenta"));
    }

    public static async Task ConfirmarAlertaAsync(this IPage page)
    {
        await page.Locator(".swal2-confirm").ClickAsync();
    }
}
