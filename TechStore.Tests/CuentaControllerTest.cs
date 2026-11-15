using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TechStore.Controllers;
using TechStore.Models;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace TechStore.Tests;

public class CuentaControllerTests
{
    [Fact]
    public void LoginGet_ConUsuarioNoAutenticado_DevuelveLaVistaConElReturnUrl()
    {
        var controller = CreateController(out _, out _);

        var result = controller.Login("/Products");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoginViewModel>(view.Model);
        Assert.Equal("/Products", model.ReturnUrl);
    }

    [Fact]
    public void LoginGet_ConUsuarioAutenticado_RedirigeAHome()
    {
        var controller = CreateController(out _, out _, authenticated: true);

        var result = controller.Login();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task LoginPost_ConModeloInvalido_NoIntentaIniciarSesionYDevuelveLaVista()
    {
        var controller = CreateController(out var userManager, out var signInManager);
        var model = CreateLogin();
        controller.ModelState.AddModelError(nameof(LoginViewModel.Password), "Ingrese su contraseña.");

        var result = await controller.Login(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        userManager.Verify(u => u.FindByNameAsync(It.IsAny<string>()), Times.Never);
        signInManager.Verify(s => s.PasswordSignInAsync(It.IsAny<IdentityUser>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task LoginPost_ConUsuarioInexistente_AgregaErrorGenericoYDevuelveLaVista()
    {
        var controller = CreateController(out var userManager, out _);
        userManager.Setup(u => u.FindByNameAsync("fabio")).ReturnsAsync((IdentityUser?)null);
        var model = CreateLogin("fabio");

        var result = await controller.Login(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        var error = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Equal("Usuario o contraseña incorrectos.", error.ErrorMessage);
    }

    [Fact]
    public async Task LoginPost_ConCorreo_BuscaAlUsuarioPorEmail()
    {
        var controller = CreateController(out var userManager, out var signInManager);
        var usuario = new IdentityUser("fabio");
        userManager.Setup(u => u.FindByEmailAsync("fabio@correo.com")).ReturnsAsync(usuario);
        signInManager.Setup(s => s.PasswordSignInAsync(usuario, "Clave123!", false, true)).ReturnsAsync(SignInResult.Success);

        var result = await controller.Login(CreateLogin("fabio@correo.com"));

        Assert.IsType<RedirectToActionResult>(result);
        userManager.Verify(u => u.FindByEmailAsync("fabio@correo.com"), Times.Once);
        userManager.Verify(u => u.FindByNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginPost_ConNombreDeUsuario_BuscaAlUsuarioPorNombre()
    {
        var controller = CreateController(out var userManager, out var signInManager);
        var usuario = new IdentityUser("fabio");
        userManager.Setup(u => u.FindByNameAsync("fabio")).ReturnsAsync(usuario);
        signInManager.Setup(s => s.PasswordSignInAsync(usuario, "Clave123!", false, true)).ReturnsAsync(SignInResult.Success);

        var result = await controller.Login(CreateLogin("fabio"));

        Assert.IsType<RedirectToActionResult>(result);
        userManager.Verify(u => u.FindByNameAsync("fabio"), Times.Once);
        userManager.Verify(u => u.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginPost_ConCredencialesValidas_RedirigeAHomeYRegistraInformacion()
    {
        var logger = new Mock<ILogger<CuentaController>>();
        var controller = CreateController(out var userManager, out var signInManager, logger: logger);
        var usuario = new IdentityUser("fabio");
        userManager.Setup(u => u.FindByNameAsync("fabio")).ReturnsAsync(usuario);
        signInManager.Setup(s => s.PasswordSignInAsync(usuario, "Clave123!", false, true)).ReturnsAsync(SignInResult.Success);

        var result = await controller.Login(CreateLogin("fabio"));

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.Equal("Bienvenido, fabio.", controller.TempData["SuccessMessage"]);
        VerifyLog(logger, LogLevel.Information, Times.Once());
    }

    [Fact]
    public async Task LoginPost_ConReturnUrlLocal_RedirigeALaUrlLocal()
    {
        var controller = CreateController(out var userManager, out var signInManager, localUrl: true);
        var usuario = new IdentityUser("fabio");
        userManager.Setup(u => u.FindByNameAsync("fabio")).ReturnsAsync(usuario);
        signInManager.Setup(s => s.PasswordSignInAsync(usuario, "Clave123!", false, true)).ReturnsAsync(SignInResult.Success);
        var model = CreateLogin("fabio");
        model.ReturnUrl = "/Products";

        var result = await controller.Login(model);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Products", redirect.Url);
    }

    [Fact]
    public async Task LoginPost_ConReturnUrlExterno_RedirigeAHome()
    {
        var controller = CreateController(out var userManager, out var signInManager, localUrl: false);
        var usuario = new IdentityUser("fabio");
        userManager.Setup(u => u.FindByNameAsync("fabio")).ReturnsAsync(usuario);
        signInManager.Setup(s => s.PasswordSignInAsync(usuario, "Clave123!", false, true)).ReturnsAsync(SignInResult.Success);
        var model = CreateLogin("fabio");
        model.ReturnUrl = "https://sitio-malicioso.com";

        var result = await controller.Login(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task LoginPost_ConCuentaBloqueadaPorAdministrador_MuestraMensajeDeBloqueoManual()
    {
        var controller = CreateController(out var userManager, out var signInManager);
        var usuario = new IdentityUser("fabio") { LockoutEnd = DateTimeOffset.MaxValue };
        userManager.Setup(u => u.FindByNameAsync("fabio")).ReturnsAsync(usuario);
        signInManager.Setup(s => s.PasswordSignInAsync(usuario, "Clave123!", false, true)).ReturnsAsync(SignInResult.LockedOut);

        var result = await controller.Login(CreateLogin("fabio"));

        Assert.IsType<ViewResult>(result);
        var error = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Equal("Su cuenta fue bloqueada por un administrador.", error.ErrorMessage);
    }

    [Fact]
    public async Task LoginPost_ConCuentaBloqueadaPorIntentosFallidos_MuestraMensajeDeBloqueoTemporal()
    {
        var controller = CreateController(out var userManager, out var signInManager);
        var usuario = new IdentityUser("fabio") { LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15) };
        userManager.Setup(u => u.FindByNameAsync("fabio")).ReturnsAsync(usuario);
        signInManager.Setup(s => s.PasswordSignInAsync(usuario, "Clave123!", false, true)).ReturnsAsync(SignInResult.LockedOut);

        var result = await controller.Login(CreateLogin("fabio"));

        Assert.IsType<ViewResult>(result);
        var error = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Equal("Cuenta bloqueada por 5 intentos fallidos. Intente de nuevo en 15 minutos.", error.ErrorMessage);
    }

    [Fact]
    public async Task LoginPost_ConContrasenaIncorrecta_AgregaErrorGenericoYDevuelveLaVista()
    {
        var logger = new Mock<ILogger<CuentaController>>();
        var controller = CreateController(out var userManager, out var signInManager, logger: logger);
        var usuario = new IdentityUser("fabio");
        userManager.Setup(u => u.FindByNameAsync("fabio")).ReturnsAsync(usuario);
        signInManager.Setup(s => s.PasswordSignInAsync(usuario, "Clave123!", false, true)).ReturnsAsync(SignInResult.Failed);
        var model = CreateLogin("fabio");

        var result = await controller.Login(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        var error = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Equal("Usuario o contraseña incorrectos.", error.ErrorMessage);
        VerifyLog(logger, LogLevel.Information, Times.Never());
    }

    [Fact]
    public void RegistroGet_ConUsuarioNoAutenticado_DevuelveLaVista()
    {
        var controller = CreateController(out _, out _);

        var result = controller.Registro();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void RegistroGet_ConUsuarioAutenticado_RedirigeAHome()
    {
        var controller = CreateController(out _, out _, authenticated: true);

        var result = controller.Registro();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task RegistroPost_ConModeloInvalido_NoCreaElUsuarioYDevuelveLaVista()
    {
        var controller = CreateController(out var userManager, out _);
        var model = CreateRegistro();
        controller.ModelState.AddModelError(nameof(RegistroViewModel.Email), "El correo electrónico es obligatorio.");

        var result = await controller.Registro(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        userManager.Verify(u => u.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegistroPost_ConDatosValidos_CreaElUsuarioConRolClienteYRedirigeALogin()
    {
        var logger = new Mock<ILogger<CuentaController>>();
        var controller = CreateController(out var userManager, out _, logger: logger);
        userManager.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), "Clave123!")).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), "Cliente")).ReturnsAsync(IdentityResult.Success);

        var result = await controller.Registro(CreateRegistro());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(CuentaController.Login), redirect.ActionName);
        userManager.Verify(u => u.CreateAsync(It.Is<IdentityUser>(x => x.UserName == "fabioguardado" && x.Email == "fabio@correo.com"), "Clave123!"), Times.Once);
        userManager.Verify(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), "Cliente"), Times.Once);
        VerifyLog(logger, LogLevel.Information, Times.Once());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegistroPost_CuandoFallaLaAsignacionDelRol_EliminaElUsuarioYDevuelveLaVista(bool eliminarTieneExito)
    {
        var logger = new Mock<ILogger<CuentaController>>();
        var controller = CreateController(out var userManager, out _, logger: logger);
        userManager.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), "Cliente"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Falla" }));
        userManager.Setup(u => u.DeleteAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(eliminarTieneExito ? IdentityResult.Success : IdentityResult.Failed(new IdentityError { Code = "Falla" }));
        var model = CreateRegistro();

        var result = await controller.Registro(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        userManager.Verify(u => u.DeleteAsync(It.IsAny<IdentityUser>()), Times.Once);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors, e => e.ErrorMessage == "No se pudo completar el registro. Intenta de nuevo.");
        Assert.Null(controller.TempData["SuccessMessage"]);
        VerifyLog(logger, LogLevel.Information, Times.Never());
        VerifyLog(logger, LogLevel.Error, Times.Once());
    }

    [Fact]
    public async Task RegistroPost_CuandoIdentityFalla_AgregaLosErroresYDevuelveLaVista()
    {
        var logger = new Mock<ILogger<CuentaController>>();
        var controller = CreateController(out var userManager, out _, logger: logger);
        userManager.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError { Description = "La contraseña es muy corta." },
                new IdentityError { Description = "El correo ya está registrado." }));
        var model = CreateRegistro();

        var result = await controller.Registro(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        var errores = controller.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage).ToList();
        Assert.Equal(new[] { "La contraseña es muy corta.", "El correo ya está registrado." }, errores);
        userManager.Verify(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
        VerifyLog(logger, LogLevel.Information, Times.Never());
    }

    [Fact]
    public async Task Logout_CierraLaSesionYRedirigeAHome()
    {
        var controller = CreateController(out _, out var signInManager, authenticated: true);

        var result = await controller.Logout();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        signInManager.Verify(s => s.SignOutAsync(), Times.Once);
        Assert.Equal("Sesión cerrada correctamente.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public void AccesoDenegado_DevuelveLaVista()
    {
        var controller = CreateController(out _, out _);

        var result = controller.AccesoDenegado();

        Assert.IsType<ViewResult>(result);
    }

    private static CuentaController CreateController(
        out Mock<UserManager<IdentityUser>> userManager,
        out Mock<SignInManager<IdentityUser>> signInManager,
        Mock<ILogger<CuentaController>>? logger = null,
        bool authenticated = false,
        bool localUrl = true)
    {
        userManager = CreateUserManager();
        signInManager = CreateSignInManager(userManager.Object);
        logger ??= new Mock<ILogger<CuentaController>>();
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.IsLocalUrl(It.IsAny<string>())).Returns(localUrl);
        var httpContext = new DefaultHttpContext();
        if (authenticated)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "fabio") }, "test"));
        }

        return new CuentaController(userManager.Object, signInManager.Object, logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
            Url = urlHelper.Object
        };
    }

    private static Mock<UserManager<IdentityUser>> CreateUserManager()
    {
        return new Mock<UserManager<IdentityUser>>(
            Mock.Of<IUserStore<IdentityUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<SignInManager<IdentityUser>> CreateSignInManager(UserManager<IdentityUser> userManager)
    {
        return new Mock<SignInManager<IdentityUser>>(
            userManager,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<ILogger<SignInManager<IdentityUser>>>(),
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<IdentityUser>>());
    }

    private static LoginViewModel CreateLogin(string usuarioOCorreo = "fabio")
    {
        return new LoginViewModel { UsuarioOCorreo = usuarioOCorreo, Password = "Clave123!" };
    }

    private static RegistroViewModel CreateRegistro()
    {
        return new RegistroViewModel
        {
            NombreUsuario = "fabioguardado",
            Email = "fabio@correo.com",
            Password = "Clave123!",
            ConfirmarPassword = "Clave123!"
        };
    }

    private static void VerifyLog(Mock<ILogger<CuentaController>> logger, LogLevel level, Times times)
    {
        logger.Verify(
            l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
