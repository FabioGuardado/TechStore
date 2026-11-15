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
builder.Services.AddScoped<IImagenService, ImagenService>();

var app = builder.Build();

// Creación de los roles y del usuario Administrador inicial. Si falla se informa y no se inicia la app.
using (var scope = app.Services.CreateScope())
{
    try
    {
        await DbInicializador.SembrarAsync(scope.ServiceProvider, app.Configuration, app.Logger);
    }
    catch (InicializacionException ex)
    {
        app.Logger.LogCritical(ex, "La aplicación no pudo inicializarse: {Mensaje}", ex.Message);
        Console.Error.WriteLine($"ERROR DE ARRANQUE: {ex.Message}");
        Environment.ExitCode = 1;
        return;
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Las respuestas 400, 403 y 404 sin contenido se muestran con una página amigable.
app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

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
