using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace TechStore.Data
{
    /// <summary>Error de arranque con un mensaje pensado para quien despliega la aplicación.</summary>
    public class InicializacionException : Exception
    {
        public InicializacionException(string message, Exception? inner = null) : base(message, inner) { }
    }

    // Creación de los roles (con RoleManager) y del usuario Administrador inicial (con UserManager).
    public static class DbInicializador
    {
        public static readonly string[] Roles = { "Administrador", "Vendedor", "Cliente" };

        public static async Task SembrarAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
        {
            try
            {
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

                // Se crea cada rol solo si todavía no existe, para evitar duplicados.
                foreach (var rol in Roles)
                {
                    if (!await roleManager.RoleExistsAsync(rol))
                    {
                        var resultado = await roleManager.CreateAsync(new IdentityRole(rol));
                        if (!resultado.Succeeded)
                        {
                            logger.LogError("No se pudo crear el rol {Rol}: {Errores}",
                                rol, string.Join(" ", resultado.Errors.Select(e => e.Description)));
                        }
                    }
                }

                // Las credenciales del Admin inicial se leen de user secrets (sección AdminInicial),
                // así no quedan escritas en el código ni en el repositorio.
                var adminUsuario = configuration["AdminInicial:Usuario"];
                var adminEmail = configuration["AdminInicial:Email"];
                var adminPassword = configuration["AdminInicial:Password"];

                if (string.IsNullOrWhiteSpace(adminUsuario) || string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
                {
                    logger.LogWarning("No se configuró la sección AdminInicial en user secrets; no se creó el usuario Admin inicial.");
                }
                else if (await userManager.FindByNameAsync(adminUsuario) == null)
                {
                    var admin = new IdentityUser { UserName = adminUsuario, Email = adminEmail, EmailConfirmed = true };
                    var resultado = await userManager.CreateAsync(admin, adminPassword);

                    if (resultado.Succeeded)
                    {
                        var resultadoRol = await userManager.AddToRoleAsync(admin, "Administrador");
                        if (!resultadoRol.Succeeded)
                        {
                            // Sin rol el Admin no podría administrar nada: se elimina para reintentar en el próximo arranque.
                            await userManager.DeleteAsync(admin);
                            logger.LogError("No se pudo asignar el rol Administrador a {Usuario}: {Errores}",
                                adminUsuario, string.Join(" ", resultadoRol.Errors.Select(e => e.Description)));
                            throw new InicializacionException(
                                $"No se pudo asignar el rol Administrador al usuario inicial '{adminUsuario}'.");
                        }

                        logger.LogInformation("Usuario Admin inicial {Usuario} creado.", adminUsuario);
                    }
                    else
                    {
                        logger.LogError("No se pudo crear el Admin inicial: {Errores}",
                            string.Join(" ", resultado.Errors.Select(e => e.Description)));
                    }
                }

                // A los usuarios que no tienen un rol asignado se les asigna el rol Cliente.
                foreach (var usuario in userManager.Users.ToList())
                {
                    if ((await userManager.GetRolesAsync(usuario)).Count != 0) continue;

                    var resultado = await userManager.AddToRoleAsync(usuario, "Cliente");
                    if (resultado.Succeeded)
                    {
                        logger.LogInformation("Se asignó el rol Cliente al usuario {Usuario}, que no tenía rol.", usuario.UserName);
                    }
                    else
                    {
                        logger.LogError("No se pudo asignar el rol Cliente al usuario {Usuario}: {Errores}",
                            usuario.UserName, string.Join(" ", resultado.Errors.Select(e => e.Description)));
                    }
                }
            }
            catch (InicializacionException)
            {
                throw;
            }
            catch (Exception ex) when (ex is SqlException or DbUpdateException or InvalidOperationException)
            {
                throw new InicializacionException(
                    "No se pudo conectar a SQL Server o la base de datos no está lista. " +
                    "Revisa la cadena de conexión 'TechStoreDB' y ejecuta 'dotnet ef database update'.", ex);
            }
        }
    }
}
