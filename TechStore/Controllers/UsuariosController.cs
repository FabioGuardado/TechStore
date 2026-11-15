using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechStore.Models;

namespace TechStore.Controllers
{
    // Administración de usuarios: solo el Admin puede ver el listado, cambiar roles
    // y bloquear o desbloquear cuentas. Los roles son fijos (Administrador, Vendedor, Cliente).
    [Authorize(Roles = "Administrador")]
    public class UsuariosController : Controller
    {
        public static readonly string[] RolesDisponibles = { "Administrador", "Vendedor", "Cliente" };

        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<UsuariosController> _logger;

        public UsuariosController(UserManager<IdentityUser> userManager, ILogger<UsuariosController> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? buscar, string? rol)
        {
            var idActual = _userManager.GetUserId(User);
            var usuarios = await _userManager.Users.OrderBy(u => u.UserName).ToListAsync();

            var lista = new List<UsuarioViewModel>();
            foreach (var usuario in usuarios)
            {
                var roles = await _userManager.GetRolesAsync(usuario);
                lista.Add(new UsuarioViewModel
                {
                    Id = usuario.Id,
                    NombreUsuario = usuario.UserName ?? string.Empty,
                    Email = usuario.Email ?? string.Empty,
                    Rol = roles.FirstOrDefault() ?? "Sin rol",
                    Bloqueado = usuario.LockoutEnd.HasValue && usuario.LockoutEnd > DateTimeOffset.UtcNow,
                    EsUsuarioActual = usuario.Id == idActual
                });
            }

            // Filtros por texto (usuario o correo) y por rol.
            if (!string.IsNullOrWhiteSpace(buscar))
            {
                lista = lista.Where(u => u.NombreUsuario.Contains(buscar, StringComparison.OrdinalIgnoreCase)
                                      || u.Email.Contains(buscar, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(rol))
            {
                lista = lista.Where(u => u.Rol == rol).ToList();
            }

            ViewBag.Buscar = buscar;
            ViewBag.Rol = rol;
            return View(lista);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarRol(string id, string rol)
        {
            if (!RolesDisponibles.Contains(rol))
            {
                return BadRequest();
            }

            // El Administrador no puede cambiar su propio rol para no quedarse sin acceso.
            if (id == _userManager.GetUserId(User))
            {
                TempData["ErrorMessage"] = "No puede cambiar su propio rol.";
                return RedirectToAction(nameof(Index));
            }

            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null) return NotFound();

            var rolesActuales = await _userManager.GetRolesAsync(usuario);
            var resultadoEliminarRoles =
                await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);

            if (!resultadoEliminarRoles.Succeeded)
            {
                _logger.LogError(
                    "No se pudieron eliminar los roles del usuario {UsuarioId}: {Errores}.",
                    usuario.Id,
                    string.Join("; ", resultadoEliminarRoles.Errors.Select(error => error.Code)));

                TempData["ErrorMessage"] = "No se pudieron actualizar los roles del usuario.";
                return RedirectToAction(nameof(Index));
            }

            var resultadoAgregarRol = await _userManager.AddToRoleAsync(usuario, rol);

            if (!resultadoAgregarRol.Succeeded)
            {
                _logger.LogError(
                    "No se pudo asignar el rol {Rol} al usuario {UsuarioId}: {Errores}.",
                    rol,
                    usuario.Id,
                    string.Join("; ", resultadoAgregarRol.Errors.Select(error => error.Code)));

                // Se restauran los roles anteriores para no dejar al usuario sin rol.
                var resultadoRestaurar = rolesActuales.Count == 0
                    ? IdentityResult.Success
                    : await _userManager.AddToRolesAsync(usuario, rolesActuales);

                if (resultadoRestaurar.Succeeded)
                {
                    _logger.LogWarning(
                        "Se restauraron los roles {Roles} del usuario {UsuarioId} tras fallar el cambio a {Rol}.",
                        string.Join(", ", rolesActuales),
                        usuario.Id,
                        rol);
                }
                else
                {
                    _logger.LogCritical(
                        "No se pudieron restaurar los roles {Roles} del usuario {UsuarioId}; la cuenta quedó sin rol: {Errores}.",
                        string.Join(", ", rolesActuales),
                        usuario.Id,
                        string.Join("; ", resultadoRestaurar.Errors.Select(error => error.Code)));
                }

                TempData["ErrorMessage"] = "No se pudo actualizar el rol del usuario.";
                return RedirectToAction(nameof(Index));
            }

            await _userManager.UpdateSecurityStampAsync(usuario);

            _logger.LogInformation(
                "El administrador {Administrador} cambió el rol del usuario {UsuarioId} a {Rol}.",
                User.Identity?.Name ?? "desconocido",
                usuario.Id,
                rol);
            TempData["SuccessMessage"] = $"El usuario {usuario.UserName} ahora tiene el rol {rol}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Bloquear(string id)
        {
            if (id == _userManager.GetUserId(User))
            {
                TempData["ErrorMessage"] = "No puede bloquear su propia cuenta.";
                return RedirectToAction(nameof(Index));
            }

            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null) return NotFound();

            // Bloqueo indefinido hasta que un Administrador lo desbloquee.
            await _userManager.SetLockoutEnabledAsync(usuario, true);
            var resultadoLockout = await _userManager.SetLockoutEndDateAsync(
    usuario,
    DateTimeOffset.MaxValue);

if (!resultadoLockout.Succeeded)
{
    _logger.LogError(
        "No se pudo bloquear al usuario {UsuarioId}: {Errores}.",
        usuario.Id,
        string.Join("; ", resultadoLockout.Errors.Select(error => error.Code)));

    TempData["ErrorMessage"] = "No se pudo bloquear la cuenta.";
    return RedirectToAction(nameof(Index));
}

await _userManager.UpdateSecurityStampAsync(usuario);

_logger.LogWarning(
    "El administrador {Administrador} bloqueó al usuario {UsuarioId}.",
    User.Identity?.Name ?? "desconocido",
    usuario.Id);

            _logger.LogInformation("Usuario {Usuario} bloqueado.", usuario.UserName);
            TempData["SuccessMessage"] = $"El usuario {usuario.UserName} fue bloqueado.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Desbloquear(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null) return NotFound();

            var resultadoDesbloqueo =
    await _userManager.SetLockoutEndDateAsync(usuario, null);

if (!resultadoDesbloqueo.Succeeded)
{
    _logger.LogError(
        "No se pudo desbloquear al usuario {UsuarioId}: {Errores}.",
        usuario.Id,
        string.Join("; ", resultadoDesbloqueo.Errors.Select(error => error.Code)));

    TempData["ErrorMessage"] = "No se pudo desbloquear la cuenta.";
    return RedirectToAction(nameof(Index));
}

await _userManager.ResetAccessFailedCountAsync(usuario);

_logger.LogInformation(
    "El administrador {Administrador} desbloqueó al usuario {UsuarioId}.",
    User.Identity?.Name ?? "desconocido",
    usuario.Id);
            TempData["SuccessMessage"] = $"El usuario {usuario.UserName} fue desbloqueado.";
            return RedirectToAction(nameof(Index));
        }
    }
}
