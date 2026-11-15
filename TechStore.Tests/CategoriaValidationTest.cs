using System.ComponentModel.DataAnnotations;
using TechStore.Models;

namespace TechStore.Tests;

public class CategoriaValidationTests
{
    [Fact]
    public void CategoriaCompletaEsValida()
    {
        var categoria = CreateCategoriaValida();

        var errores = Validate(categoria);

        Assert.Empty(errores);
    }

    [Fact]
    public void NombreEsObligatorio()
    {
        var categoria = CreateCategoriaValida();
        categoria.Nombre = string.Empty;

        var errores = Validate(categoria);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(Categoria.Nombre)));
    }

    [Fact]
    public void NombreNoPuedeSuperarLos100Caracteres()
    {
        var categoria = CreateCategoriaValida();
        categoria.Nombre = new string('A', 101);

        var errores = Validate(categoria);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(Categoria.Nombre)));
    }

    [Fact]
    public void NombreCon100CaracteresEsValido()
    {
        var categoria = CreateCategoriaValida();
        categoria.Nombre = new string('A', 100);

        var errores = Validate(categoria);

        Assert.DoesNotContain(errores, e => e.MemberNames.Contains(nameof(Categoria.Nombre)));
    }

    [Fact]
    public void DescripcionNoPuedeSuperarLos500Caracteres()
    {
        var categoria = CreateCategoriaValida();
        categoria.Descripcion = new string('A', 501);

        var errores = Validate(categoria);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(Categoria.Descripcion)));
    }

    [Fact]
    public void DescripcionCon500CaracteresEsValida()
    {
        var categoria = CreateCategoriaValida();
        categoria.Descripcion = new string('A', 500);

        var errores = Validate(categoria);

        Assert.DoesNotContain(errores, e => e.MemberNames.Contains(nameof(Categoria.Descripcion)));
    }

    [Fact]
    public void DescripcionEsOpcional()
    {
        var categoria = CreateCategoriaValida();
        categoria.Descripcion = null;

        var errores = Validate(categoria);

        Assert.Empty(errores);
    }

    [Fact]
    public void CategoriaNuevaTieneValoresPorDefecto()
    {
        var categoria = new Categoria();

        Assert.Equal(string.Empty, categoria.Nombre);
        Assert.Null(categoria.Descripcion);
        Assert.Empty(categoria.Productos);
    }

    private static Categoria CreateCategoriaValida()
    {
        return new Categoria
        {
            Nombre = "Tarjetas de video",
            Descripcion = "GPUs para gaming y trabajo profesional."
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
