using System.ComponentModel.DataAnnotations;
using TechStore.Models;

namespace TechStore.Tests;

public class LoginValidationTests
{
    [Fact]
    public void LoginCompletoEsValido()
    {
        var model = CreateLoginValido();

        var errores = Validate(model);

        Assert.Empty(errores);
    }

    [Theory]
    [InlineData(nameof(LoginViewModel.UsuarioOCorreo))]
    [InlineData(nameof(LoginViewModel.Password))]
    public void CamposObligatoriosNoPuedenEstarVacios(string propiedad)
    {
        var model = CreateLoginValido();
        typeof(LoginViewModel).GetProperty(propiedad)!.SetValue(model, string.Empty);

        var errores = Validate(model);

        Assert.Contains(errores, e => e.MemberNames.Contains(propiedad));
    }

    [Fact]
    public void ReturnUrlEsOpcional()
    {
        var model = CreateLoginValido();
        model.ReturnUrl = null;

        var errores = Validate(model);

        Assert.Empty(errores);
    }

    [Fact]
    public void LoginNuevoTieneValoresPorDefecto()
    {
        var model = new LoginViewModel();

        Assert.False(model.Recordarme);
        Assert.Null(model.ReturnUrl);
    }

    private static LoginViewModel CreateLoginValido()
    {
        return new LoginViewModel
        {
            UsuarioOCorreo = "fabio@correo.com",
            Password = "Clave123!"
        };
    }

    private static IReadOnlyCollection<ValidationResult> Validate(object modelo)
    {
        var context = new ValidationContext(modelo);
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, context, errores, validateAllProperties: true);
        return errores;
    }
}
