using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using TechStore.Data;
using TechStore.Models;

namespace TechStore.E2E;

public class E2EFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUsuario = "admin";
    public const string AdminPassword = "Admin123!";

    private readonly string _databaseName = Guid.NewGuid().ToString();
    private int _hostsConfigurados;
    private IHost? _kestrelHost;
    private IPlaywright? _playwright;

    public string BaseUrl { get; private set; } = string.Empty;

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _ = Services;
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
    }

    public new async Task DisposeAsync()
    {
        if (Browser != null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();
        _kestrelHost?.Dispose();
        await base.DisposeAsync();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        builder.ConfigureWebHost(webHost => webHost.UseKestrel().UseUrls("http://127.0.0.1:0"));
        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        var server = _kestrelHost.Services.GetRequiredService<IServer>();
        BaseUrl = server.Features.Get<IServerAddressesFeature>()!.Addresses.First().Replace("127.0.0.1", "localhost");

        testHost.Start();
        return testHost;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("AdminInicial:Usuario", AdminUsuario);
        builder.UseSetting("AdminInicial:Email", "admin@techstore.com");
        builder.UseSetting("AdminInicial:Password", AdminPassword);

        builder.ConfigureServices(services =>
        {
            var descriptores = services
                .Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(TechStoreContext)))
                .ToList();
            foreach (var descriptor in descriptores)
            {
                services.Remove(descriptor);
            }

            // Program.Main se ejecuta una vez por host: el de Kestrel arranca primero y usa la BD real,
            // el host de TestServer usa una BD descartable para que el seeding no se duplique.
            var databaseName = Interlocked.Increment(ref _hostsConfigurados) == 1 ? _databaseName : Guid.NewGuid().ToString();
            services.AddDbContext<TechStoreContext>(options => options.UseInMemoryDatabase(databaseName));
        });
    }

    public async Task CrearUsuarioAsync(string usuario, string password, string rol)
    {
        using var scope = _kestrelHost!.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var identityUser = new IdentityUser { UserName = usuario, Email = $"{usuario}@techstore.com", EmailConfirmed = true };

        var resultado = await userManager.CreateAsync(identityUser, password);
        Assert.True(resultado.Succeeded, string.Join(" ", resultado.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(identityUser, rol);
    }

    public async Task CrearProductoAsync(string nombre, string categoria)
    {
        using var scope = _kestrelHost!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TechStoreContext>();
        context.Productos.Add(new Producto
        {
            Nombre = nombre,
            Descripcion = $"Descripción de {nombre}.",
            Precio = 100m,
            Stock = 5,
            Estado = "Disponible",
            Categoria = new Categoria { Nombre = categoria }
        });
        await context.SaveChangesAsync();
    }

    public async Task<T> ConsultarAsync<T>(Func<TechStoreContext, Task<T>> consulta)
    {
        using var scope = _kestrelHost!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TechStoreContext>();
        return await consulta(context);
    }
}
