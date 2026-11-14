using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Data;
using TechStore.Models;

namespace TechStore.IntegrationTests;

public class TechStoreFactory : WebApplicationFactory<Program>
{
    public const string AdminUsuario = "admin";
    public const string AdminPassword = "Admin123!";

    private readonly string _databaseName = Guid.NewGuid().ToString();

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

            services.AddDbContext<TechStoreContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task CrearUsuarioAsync(string usuario, string password, string rol)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var identityUser = new IdentityUser { UserName = usuario, Email = $"{usuario}@techstore.com", EmailConfirmed = true };

        var resultado = await userManager.CreateAsync(identityUser, password);
        Assert.True(resultado.Succeeded, string.Join(" ", resultado.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(identityUser, rol);
    }

    public async Task<int> CrearProductoAsync(string nombre, string categoria = "Audio", int stock = 5)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TechStoreContext>();
        var producto = new Producto
        {
            Nombre = nombre,
            Descripcion = $"Descripción de {nombre}.",
            Precio = 100m,
            Stock = stock,
            Estado = "Disponible",
            Categoria = new Categoria { Nombre = categoria }
        };

        context.Productos.Add(producto);
        await context.SaveChangesAsync();
        return producto.ID;
    }

    public async Task<int> CrearCategoriaAsync(string nombre)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TechStoreContext>();
        var categoria = new Categoria { Nombre = nombre };

        context.Categorias.Add(categoria);
        await context.SaveChangesAsync();
        return categoria.ID;
    }

    public async Task<T> ConsultarAsync<T>(Func<TechStoreContext, Task<T>> consulta)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TechStoreContext>();
        return await consulta(context);
    }
}
