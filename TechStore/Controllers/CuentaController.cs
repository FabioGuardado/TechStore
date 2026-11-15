using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TechStore.Models;

namespace TechStore.Controllers
{
    // Login, Registro, Cierre de sesión y Acceso denegado con ASP.NET Core Identity.
    public class CuentaController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<CuentaController> _logger;

        public CuentaController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, ILogger<CuentaController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        public IActionResult Login(string? returnUrl = null)
        {
            // Si ya inició sesión no tiene sentido mostrar el formulario.
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Permite ingresar con el nombre de usuario o con el correo electrónico.
            var usuario = model.UsuarioOCorreo.Contains('@')
                ? await _userManager.FindByEmailAsync(model.UsuarioOCorreo)
                : await _userManager.FindByNameAsync(model.UsuarioOCorreo);

            if (usuario == null)
            {
                _logger.LogWarning(
                    "Intento de inicio de sesión fallido para el identificador {Identificador}.",
                    model.UsuarioOCorreo);

                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
                return View(model);
            }

            // lockoutOnFailure: true cuenta los intentos fallidos para bloquear la cuenta.
            var resultado = await _signInManager.PasswordSignInAsync(usuario, model.Password, model.Recordarme, lockoutOnFailure: true);

            if (resultado.Succeeded)
            {
                _logger.LogInformation("El usuario {Usuario} inició sesión.", usuario.UserName);
                TempData["SuccessMessage"] = $"Bienvenido, {usuario.UserName}.";

                // Solo se redirige a URLs locales para evitar redirecciones a sitios externos.
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return LocalRedirect(model.ReturnUrl);
                }

                return RedirectToAction("Index", "Home");
            }

            if (resultado.IsLockedOut)
            {
                // Se distingue el bloqueo hecho por el Administrador del bloqueo por intentos fallidos.
                var mensaje = usuario.LockoutEnd == DateTimeOffset.MaxValue
                    ? "Su cuenta fue bloqueada por un administrador."
                    : "Cuenta bloqueada por 5 intentos fallidos. Intente de nuevo en 15 minutos.";
                _logger.LogWarning(
                    "La cuenta del usuarios {Usuario} fue bloqueada", usuario.UserName);
                ModelState.AddModelError(string.Empty, mensaje);
                return View(model);
            }

            _logger.LogWarning(
                "La autenticación falló para el usuario {Usuario}. Resultado: {Resultado}.",
                usuario.UserName,
                resultado.ToString());

            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View(model);
        }

        public IActionResult Registro()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(RegistroViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var usuario = new IdentityUser
            {
                UserName = model.NombreUsuario,
                Email = model.Email
            };

            var resultado = await _userManager.CreateAsync(usuario, model.Password);

            if (resultado.Succeeded)
            {
                // Todo usuario que se registra desde la página queda con el rol Cliente.
                await _userManager.AddToRoleAsync(usuario, "Cliente");
                _logger.LogInformation("Nuevo usuario registrado: {Usuario}.", usuario.UserName);
                TempData["SuccessMessage"] = "Cuenta creada correctamente. Ya puede iniciar sesión.";
                return RedirectToAction(nameof(Login));
            }

            // Muestra los errores que devuelve Identity (contraseña débil, usuario repetido, etc.).
            foreach (var error in resultado.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            _logger.LogWarning(
                "No se pudo registrar el usuario {Usuario}. Errores: {Errores}.",
                model.NombreUsuario,
                string.Join("; ", resultado.Errors.Select(error => error.Code)));

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            TempData["SuccessMessage"] = "Sesión cerrada correctamente.";
            return RedirectToAction("Index", "Home");
        }

        // Se muestra cuando el usuario no tiene el rol necesario.
        public IActionResult AccesoDenegado()
        {
            return View();
        }
    }
}
