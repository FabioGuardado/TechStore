using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TechStore.Controllers;
using TechStore.Data;
using TechStore.Models;

namespace TechStore.Tests;

public class UsuariosControllerTests
{
    [Fact]
    public async Task Index_SinFiltros_DevuelveTodosLosUsuariosOrdenadosPorNombre()
    {
        await using var context = CreateContext();
        var controller = CreateController(out var userManager, context);
        await SeedUsuarios(context, userManager, ("zoe", "zoe@correo.com", "Cliente"), ("ana", "ana@correo.com", "Vendedor"));

        var result = await controller.Index(null, null);

        var view = Assert.IsType<ViewResult>(result);
        var lista = Assert.IsAssignableFrom<IEnumerable<UsuarioViewModel>>(view.Model).ToList();
        Assert.Equal(new[] { "ana", "zoe" }, lista.Select(u => u.NombreUsuario));
        Assert.Equal(new[] { "Vendedor", "Cliente" }, lista.Select(u => u.Rol));
    }

    [Fact]
    public async Task Index_ConUsuarioSinRol_MuestraSinRol()
    {
        await using var context = CreateContext();
        var controller = CreateController(out var userManager, context);
        await SeedUsuarios(context, userManager, ("ana", "ana@correo.com", null));

        var result = await controller.Index(null, null);

        var view = Assert.IsType<ViewResult>(result);
        var usuario = Assert.Single(Assert.IsAssignableFrom<IEnumerable<UsuarioViewModel>>(view.Model));
        Assert.Equal("Sin rol", usuario.Rol);
    }

    [Theory]
    [InlineData("ANA", "ana")]
    [InlineData("zoe@correo", "zoe")]
    public async Task Index_ConTextoDeBusqueda_FiltraPorUsuarioOCorreo(string buscar, string esperado)
    {
        await using var context = CreateContext();
        var controller = CreateController(out var userManager, context);
        await SeedUsuarios(context, userManager, ("ana", "ana@correo.com", "Cliente"), ("zoe", "zoe@correo.com", "Cliente"));

        var result = await controller.Index(buscar, null);

        var view = Assert.IsType<ViewResult>(result);
        var usuario = Assert.Single(Assert.IsAssignableFrom<IEnumerable<UsuarioViewModel>>(view.Model));
        Assert.Equal(esperado, usuario.NombreUsuario);
        Assert.Equal(buscar, (string?)controller.ViewBag.Buscar);
    }

    [Fact]
    public async Task Index_ConFiltroDeRol_DevuelveSoloLosUsuariosDeEseRol()
    {
        await using var context = CreateContext();
        var controller = CreateController(out var userManager, context);
        await SeedUsuarios(context, userManager, ("ana", "ana@correo.com", "Vendedor"), ("zoe", "zoe@correo.com", "Cliente"));

        var result = await controller.Index(null, "Vendedor");

        var view = Assert.IsType<ViewResult>(result);
        var usuario = Assert.Single(Assert.IsAssignableFrom<IEnumerable<UsuarioViewModel>>(view.Model));
        Assert.Equal("ana", usuario.NombreUsuario);
    }

    [Fact]
    public async Task Index_ConUsuarioBloqueadoYUsuarioActual_MarcaLosIndicadores()
    {
        await using var context = CreateContext();
        var controller = CreateController(out var userManager, context, usuarioActualId: "id-ana");
        await SeedUsuarios(context, userManager, ("ana", "ana@correo.com", "Administrador"), ("zoe", "zoe@correo.com", "Cliente"));
        var zoe = await context.Users.SingleAsync(u => u.UserName == "zoe");
        zoe.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1);
        await context.SaveChangesAsync();

        var result = await controller.Index(null, null);

        var view = Assert.IsType<ViewResult>(result);
        var lista = Assert.IsAssignableFrom<IEnumerable<UsuarioViewModel>>(view.Model).ToList();
        Assert.True(lista.Single(u => u.NombreUsuario == "ana").EsUsuarioActual);
        Assert.False(lista.Single(u => u.NombreUsuario == "ana").Bloqueado);
        Assert.True(lista.Single(u => u.NombreUsuario == "zoe").Bloqueado);
        Assert.False(lista.Single(u => u.NombreUsuario == "zoe").EsUsuarioActual);
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("")]
    [InlineData("administrador")]
    public async Task CambiarRol_ConRolInvalido_DevuelveBadRequest(string rol)
    {
        var controller = CreateController(out var userManager);

        var result = await controller.CambiarRol("id-zoe", rol);

        Assert.IsType<BadRequestResult>(result);
        userManager.Verify(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CambiarRol_ConElPropioUsuario_NoCambiaElRolYRedirigeAlIndice()
    {
        var controller = CreateController(out var userManager, usuarioActualId: "id-ana");

        var result = await controller.CambiarRol("id-ana", "Cliente");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(UsuariosController.Index), redirect.ActionName);
        Assert.Equal("No puede cambiar su propio rol.", controller.TempData["ErrorMessage"]);
        userManager.Verify(u => u.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CambiarRol_ConUsuarioInexistente_DevuelveNotFound()
    {
        var controller = CreateController(out var userManager);
        userManager.Setup(u => u.FindByIdAsync("id-zoe")).ReturnsAsync((IdentityUser?)null);

        var result = await controller.CambiarRol("id-zoe", "Vendedor");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CambiarRol_ConDatosValidos_ReemplazaElRolActualizaElSelloYRegistraInformacion()
    {
        var logger = new Mock<ILogger<UsuariosController>>();
        var controller = CreateController(out var userManager, logger: logger);
        var zoe = new IdentityUser("zoe") { Id = "id-zoe" };
        var rolesActuales = new List<string> { "Cliente" };
        userManager.Setup(u => u.FindByIdAsync("id-zoe")).ReturnsAsync(zoe);
        userManager.Setup(u => u.GetRolesAsync(zoe)).ReturnsAsync(rolesActuales);

        var result = await controller.CambiarRol("id-zoe", "Vendedor");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(UsuariosController.Index), redirect.ActionName);
        userManager.Verify(u => u.RemoveFromRolesAsync(zoe, rolesActuales), Times.Once);
        userManager.Verify(u => u.AddToRoleAsync(zoe, "Vendedor"), Times.Once);
        userManager.Verify(u => u.UpdateSecurityStampAsync(zoe), Times.Once);
        Assert.Equal("El usuario zoe ahora tiene el rol Vendedor.", controller.TempData["SuccessMessage"]);
        VerifyLog(logger, LogLevel.Information, Times.Once());
    }

    [Fact]
    public async Task CambiarRol_CuandoAddToRoleFalla_RestauraLosRolesAnterioresYMuestraError()
    {
        var logger = new Mock<ILogger<UsuariosController>>();
        var controller = CreateController(out var userManager, logger: logger);
        var zoe = new IdentityUser("zoe") { Id = "id-zoe" };
        var rolesActuales = new List<string> { "Cliente" };
        userManager.Setup(u => u.FindByIdAsync("id-zoe")).ReturnsAsync(zoe);
        userManager.Setup(u => u.GetRolesAsync(zoe)).ReturnsAsync(rolesActuales);
        userManager.Setup(u => u.AddToRoleAsync(zoe, "Vendedor"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Falla" }));

        var result = await controller.CambiarRol("id-zoe", "Vendedor");

        Assert.IsType<RedirectToActionResult>(result);
        userManager.Verify(u => u.AddToRolesAsync(zoe, rolesActuales), Times.Once);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
        Assert.Null(controller.TempData["SuccessMessage"]);
        userManager.Verify(u => u.UpdateSecurityStampAsync(It.IsAny<IdentityUser>()), Times.Never);
        VerifyLog(logger, LogLevel.Warning, Times.Once());
    }

    [Fact]
    public async Task CambiarRol_CuandoNoSePuedenRestaurarLosRoles_RegistraUnErrorCritico()
    {
        var logger = new Mock<ILogger<UsuariosController>>();
        var controller = CreateController(out var userManager, logger: logger);
        var zoe = new IdentityUser("zoe") { Id = "id-zoe" };
        var rolesActuales = new List<string> { "Cliente" };
        userManager.Setup(u => u.FindByIdAsync("id-zoe")).ReturnsAsync(zoe);
        userManager.Setup(u => u.GetRolesAsync(zoe)).ReturnsAsync(rolesActuales);
        userManager.Setup(u => u.AddToRoleAsync(zoe, "Vendedor"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Falla" }));
        userManager.Setup(u => u.AddToRolesAsync(zoe, rolesActuales))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Falla2" }));

        await controller.CambiarRol("id-zoe", "Vendedor");

        VerifyLog(logger, LogLevel.Critical, Times.Once());
    }

    [Fact]
    public async Task CambiarRol_CuandoRemoveFromRolesFalla_NoAgregaElRolNuevo()
    {
        var controller = CreateController(out var userManager);
        var zoe = new IdentityUser("zoe") { Id = "id-zoe" };
        var rolesActuales = new List<string> { "Cliente" };
        userManager.Setup(u => u.FindByIdAsync("id-zoe")).ReturnsAsync(zoe);
        userManager.Setup(u => u.GetRolesAsync(zoe)).ReturnsAsync(rolesActuales);
        userManager.Setup(u => u.RemoveFromRolesAsync(zoe, rolesActuales))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Falla" }));

        await controller.CambiarRol("id-zoe", "Vendedor");

        userManager.Verify(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Bloquear_ConElPropioUsuario_NoBloqueaYRedirigeAlIndice()
    {
        var controller = CreateController(out var userManager, usuarioActualId: "id-ana");

        var result = await controller.Bloquear("id-ana");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(UsuariosController.Index), redirect.ActionName);
        Assert.Equal("No puede bloquear su propia cuenta.", controller.TempData["ErrorMessage"]);
        userManager.Verify(u => u.SetLockoutEndDateAsync(It.IsAny<IdentityUser>(), It.IsAny<DateTimeOffset?>()), Times.Never);
    }

    [Fact]
    public async Task Bloquear_ConUsuarioInexistente_DevuelveNotFound()
    {
        var controller = CreateController(out var userManager);
        userManager.Setup(u => u.FindByIdAsync("id-zoe")).ReturnsAsync((IdentityUser?)null);

        var result = await controller.Bloquear("id-zoe");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Bloquear_ConUsuarioExistente_LoBloqueaIndefinidamenteYRegistraInformacion()
    {
        var logger = new Mock<ILogger<UsuariosController>>();
        var controller = CreateController(out var userManager, logger: logger);
        var zoe = new IdentityUser("zoe") { Id = "id-zoe" };
        userManager.Setup(u => u.FindByIdAsync("id-zoe")).ReturnsAsync(zoe);

        var result = await controller.Bloquear("id-zoe");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(UsuariosController.Index), redirect.ActionName);
        userManager.Verify(u => u.SetLockoutEnabledAsync(zoe, true), Times.Once);
        userManager.Verify(u => u.SetLockoutEndDateAsync(zoe, DateTimeOffset.MaxValue), Times.Once);
        userManager.Verify(u => u.UpdateSecurityStampAsync(zoe), Times.Once);
        Assert.Equal("El usuario zoe fue bloqueado.", controller.TempData["SuccessMessage"]);
        VerifyLog(logger, LogLevel.Information, Times.Once());
    }

    [Fact]
    public async Task Desbloquear_ConUsuarioInexistente_DevuelveNotFound()
    {
        var controller = CreateController(out var userManager);
        userManager.Setup(u => u.FindByIdAsync("id-zoe")).ReturnsAsync((IdentityUser?)null);

        var result = await controller.Desbloquear("id-zoe");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Desbloquear_ConUsuarioExistente_QuitaElBloqueoYReiniciaLosIntentos()
    {
        var logger = new Mock<ILogger<UsuariosController>>();
        var controller = CreateController(out var userManager, logger: logger);
        var zoe = new IdentityUser("zoe") { Id = "id-zoe" };
        userManager.Setup(u => u.FindByIdAsync("id-zoe")).ReturnsAsync(zoe);

        var result = await controller.Desbloquear("id-zoe");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(UsuariosController.Index), redirect.ActionName);
        userManager.Verify(u => u.SetLockoutEndDateAsync(zoe, null), Times.Once);
        userManager.Verify(u => u.ResetAccessFailedCountAsync(zoe), Times.Once);
        Assert.Equal("El usuario zoe fue desbloqueado.", controller.TempData["SuccessMessage"]);
        VerifyLog(logger, LogLevel.Information, Times.Once());
    }

    private static UsuariosController CreateController(
        out Mock<UserManager<IdentityUser>> userManager,
        TechStoreContext? context = null,
        Mock<ILogger<UsuariosController>>? logger = null,
        string usuarioActualId = "id-actual")
    {
        userManager = new Mock<UserManager<IdentityUser>>(
            Mock.Of<IUserStore<IdentityUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(usuarioActualId);
        userManager.Setup(u => u.RemoveFromRolesAsync(It.IsAny<IdentityUser>(), It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.AddToRolesAsync(It.IsAny<IdentityUser>(), It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.SetLockoutEndDateAsync(It.IsAny<IdentityUser>(), It.IsAny<DateTimeOffset?>())).ReturnsAsync(IdentityResult.Success);
        if (context != null)
        {
            userManager.Setup(u => u.Users).Returns(context.Users);
            userManager.Setup(u => u.GetRolesAsync(It.IsAny<IdentityUser>()))
                .ReturnsAsync((IdentityUser u) => context.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
                    .ToList());
        }

        logger ??= new Mock<ILogger<UsuariosController>>();
        var httpContext = new DefaultHttpContext();

        return new UsuariosController(userManager.Object, logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };
    }

    private static async Task SeedUsuarios(
        TechStoreContext context,
        Mock<UserManager<IdentityUser>> userManager,
        params (string Nombre, string Email, string? Rol)[] usuarios)
    {
        foreach (var (nombre, email, rol) in usuarios)
        {
            context.Users.Add(new IdentityUser(nombre) { Id = $"id-{nombre}", Email = email });
            if (rol != null)
            {
                var rolId = $"rol-{rol}";
                if (!context.Roles.Local.Any(r => r.Id == rolId))
                {
                    context.Roles.Add(new IdentityRole(rol) { Id = rolId });
                }

                context.UserRoles.Add(new IdentityUserRole<string> { UserId = $"id-{nombre}", RoleId = rolId });
            }
        }

        await context.SaveChangesAsync();
    }

    private static TechStoreContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<TechStoreContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new TechStoreContext(options);
    }

    private static void VerifyLog(Mock<ILogger<UsuariosController>> logger, LogLevel level, Times times)
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
