using TechStore.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TechStore.Services;
using TechStore.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<TechStoreContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("TechStoreDB"))
);

// ASP.NET Core Identity: usuarios y roles guardados con EF Core en TechStoreContext.
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;

        // Bloqueo de la cuenta por 15 minutos tras 5 intentos fallidos de login.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<TechStoreContext>()
    .AddErrorDescriber<ErroresIdentityEspanol>()
    .AddDefaultTokenProviders();

// Rutas de Login y de Acceso denegado (cuando el usuario no tiene el rol necesario).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/Login";
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
});

// La sesión se revalida cada minuto: si el Administrador cambia el rol de un usuario o lo
// bloquea, el cambio se aplica casi de inmediato sin esperar a que cierre sesión.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(1);
});

// Servicios de la capa de negocio (Inyección de Dependencias)
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();

var app = builder.Build();

// Creación de los roles (con RoleManager) y del usuario Administrador inicial (con UserManager).
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    // Se crea cada rol solo si todavía no existe, para evitar duplicados.
    foreach (var rol in new[] { "Administrador", "Vendedor", "Cliente" })
    {
        if (!await roleManager.RoleExistsAsync(rol))
        {
            var resultado = await roleManager.CreateAsync(new IdentityRole(rol));
            if (!resultado.Succeeded)
            {
                app.Logger.LogError("No se pudo crear el rol {Rol}.", rol);
            }
        }
    }

    // Las credenciales del Admin inicial se leen de user secrets (sección AdminInicial),
    // así no quedan escritas en el código ni en el repositorio.
    var adminUsuario = builder.Configuration["AdminInicial:Usuario"];
    var adminEmail = builder.Configuration["AdminInicial:Email"];
    var adminPassword = builder.Configuration["AdminInicial:Password"];

    if (string.IsNullOrWhiteSpace(adminUsuario) || string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
    {
        app.Logger.LogWarning("No se configuró la sección AdminInicial en user secrets; no se creó el usuario Admin inicial.");
    }
    else if (await userManager.FindByNameAsync(adminUsuario) == null)
    {
        var admin = new IdentityUser { UserName = adminUsuario, Email = adminEmail, EmailConfirmed = true };
        var resultado = await userManager.CreateAsync(admin, adminPassword);

        if (resultado.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Administrador");
            app.Logger.LogInformation("Usuario Admin inicial {Usuario} creado.", adminUsuario);
        }
        else
        {
            app.Logger.LogError("No se pudo crear el Admin inicial: {Errores}",
                string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }
    }

    // A los usuarios que no tienen un rol asignado se les asigna el rol Cliente.
    foreach (var usuario in userManager.Users.ToList())
    {
        if ((await userManager.GetRolesAsync(usuario)).Count == 0)
        {
            await userManager.AddToRoleAsync(usuario, "Cliente");
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();


// Expone Program a los proyectos de pruebas de integración y E2E (WebApplicationFactory).
public partial class Program { }
