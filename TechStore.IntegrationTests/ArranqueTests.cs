using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Data;

namespace TechStore.IntegrationTests;

public class ArranqueTests
{
    [Fact]
    public async Task Arranque_SinSeccionAdminInicial_IniciaYNoCreaElAdministrador()
    {
        var baseDatos = Guid.NewGuid().ToString();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminInicial:Usuario", "");
            builder.UseSetting("AdminInicial:Email", "");
            builder.UseSetting("AdminInicial:Password", "");
            builder.ConfigureServices(services =>
            {
                var descriptores = services
                    .Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(TechStoreContext)))
                    .ToList();
                foreach (var descriptor in descriptores) services.Remove(descriptor);
                services.AddDbContext<TechStoreContext>(o => o.UseInMemoryDatabase(baseDatos));
            });
        });

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        Assert.Empty(userManager.Users);
        Assert.True(await roleManager.RoleExistsAsync("Administrador"));
    }
}
