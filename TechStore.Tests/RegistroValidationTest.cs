using System.ComponentModel.DataAnnotations;
using TechStore.Models;

namespace TechStore.Tests;

public class RegistroValidationTests
{
    [Fact]
    public void RegistroCompletoEsValido()
    {
        var model = CreateRegistroValido();

        var errores = Validate(model);

        Assert.Empty(errores);
    }

    [Theory]
    [InlineData(nameof(RegistroViewModel.NombreUsuario))]
    [InlineData(nameof(RegistroViewModel.Email))]
    [InlineData(nameof(RegistroViewModel.Password))]
    [InlineData(nameof(RegistroViewModel.ConfirmarPassword))]
    public void CamposObligatoriosNoPuedenEstarVacios(string propiedad)
    {
        var model = CreateRegistroValido();
        typeof(RegistroViewModel).GetProperty(propiedad)!.SetValue(model, string.Empty);

        var errores = Validate(model);

        Assert.Contains(errores, e => e.MemberNames.Contains(propiedad));
    }

    [Fact]
    public void NombreUsuarioNoPuedeSuperarLos50Caracteres()
    {
        var model = CreateRegistroValido();
        model.NombreUsuario = new string('A', 51);

        var errores = Validate(model);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(RegistroViewModel.NombreUsuario)));
    }

    [Fact]
    public void NombreUsuarioCon50CaracteresEsValido()
    {
        var model = CreateRegistroValido();
        model.NombreUsuario = new string('A', 50);

        var errores = Validate(model);

        Assert.DoesNotContain(errores, e => e.MemberNames.Contains(nameof(RegistroViewModel.NombreUsuario)));
    }

    [Theory]
    [InlineData("correo-sin-arroba")]
    [InlineData("usuario@")]
    public void EmailConFormatoInvalidoEsRechazado(string email)
    {
        var model = CreateRegistroValido();
        model.Email = email;

        var errores = Validate(model);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(RegistroViewModel.Email)));
    }

    [Fact]
    public void ContrasenasDistintasSonRechazadas()
    {
        var model = CreateRegistroValido();
        model.ConfirmarPassword = "OtraClave456!";

        var errores = Validate(model);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(RegistroViewModel.ConfirmarPassword)));
    }

    private static RegistroViewModel CreateRegistroValido()
    {
        return new RegistroViewModel
        {
            NombreUsuario = "fabioguardado",
            Email = "fabio@correo.com",
            Password = "Clave123!",
            ConfirmarPassword = "Clave123!"
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
