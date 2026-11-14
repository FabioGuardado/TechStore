using System.ComponentModel.DataAnnotations;
using TechStore.Models;

namespace TechStore.Tests;

public class ProductoValidationTests
{
    [Fact]
    public void ProductoCompletoEsValido()
    {
        var producto = CreateProductoValido();

        var errores = Validate(producto);

        Assert.Empty(errores);
    }

    [Theory]
    [InlineData(nameof(Producto.Nombre))]
    [InlineData(nameof(Producto.Descripcion))]
    [InlineData(nameof(Producto.Estado))]
    public void CamposDeTextoObligatoriosNoPuedenEstarVacios(string propiedad)
    {
        var producto = CreateProductoValido();
        typeof(Producto).GetProperty(propiedad)!.SetValue(producto, string.Empty);

        var errores = Validate(producto);

        Assert.Contains(errores, e => e.MemberNames.Contains(propiedad));
    }

    [Fact]
    public void NombreNoPuedeSuperarLos150Caracteres()
    {
        var producto = CreateProductoValido();
        producto.Nombre = new string('A', 151);

        var errores = Validate(producto);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(Producto.Nombre)));
    }

    [Fact]
    public void NombreCon150CaracteresEsValido()
    {
        var producto = CreateProductoValido();
        producto.Nombre = new string('A', 150);

        var errores = Validate(producto);

        Assert.DoesNotContain(errores, e => e.MemberNames.Contains(nameof(Producto.Nombre)));
    }

    [Fact]
    public void DescripcionNoPuedeSuperarLos1000Caracteres()
    {
        var producto = CreateProductoValido();
        producto.Descripcion = new string('A', 1001);

        var errores = Validate(producto);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(Producto.Descripcion)));
    }

    [Fact]
    public void DescripcionCon1000CaracteresEsValida()
    {
        var producto = CreateProductoValido();
        producto.Descripcion = new string('A', 1000);

        var errores = Validate(producto);

        Assert.DoesNotContain(errores, e => e.MemberNames.Contains(nameof(Producto.Descripcion)));
    }

    [Fact]
    public void ImagenUrlNoPuedeSuperarLos300Caracteres()
    {
        var producto = CreateProductoValido();
        producto.ImagenUrl = new string('A', 301);

        var errores = Validate(producto);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(Producto.ImagenUrl)));
    }

    [Fact]
    public void ImagenUrlCon300CaracteresEsValida()
    {
        var producto = CreateProductoValido();
        producto.ImagenUrl = new string('A', 300);

        var errores = Validate(producto);

        Assert.DoesNotContain(errores, e => e.MemberNames.Contains(nameof(Producto.ImagenUrl)));
    }

    [Fact]
    public void EstadoNoPuedeSuperarLos50Caracteres()
    {
        var producto = CreateProductoValido();
        producto.Estado = new string('A', 51);

        var errores = Validate(producto);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(Producto.Estado)));
    }

    [Fact]
    public void EstadoCon50CaracteresEsValido()
    {
        var producto = CreateProductoValido();
        producto.Estado = new string('A', 50);

        var errores = Validate(producto);

        Assert.DoesNotContain(errores, e => e.MemberNames.Contains(nameof(Producto.Estado)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1000000")]
    public void PrecioFueraDeRangoEsInvalido(string precio)
    {
        var producto = CreateProductoValido();
        producto.Precio = decimal.Parse(precio, System.Globalization.CultureInfo.InvariantCulture);

        var errores = Validate(producto);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(Producto.Precio)));
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("999999.99")]
    public void PrecioEnLosLimitesEsValido(string precio)
    {
        var producto = CreateProductoValido();
        producto.Precio = decimal.Parse(precio, System.Globalization.CultureInfo.InvariantCulture);

        var errores = Validate(producto);

        Assert.DoesNotContain(errores, e => e.MemberNames.Contains(nameof(Producto.Precio)));
    }

    [Fact]
    public void StockNegativoEsInvalido()
    {
        var producto = CreateProductoValido();
        producto.Stock = -1;

        var errores = Validate(producto);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(Producto.Stock)));
    }

    [Fact]
    public void StockCeroEsValido()
    {
        var producto = CreateProductoValido();
        producto.Stock = 0;

        var errores = Validate(producto);

        Assert.DoesNotContain(errores, e => e.MemberNames.Contains(nameof(Producto.Stock)));
    }

    [Fact]
    public void ProductoNuevoTieneValoresPorDefecto()
    {
        var producto = new Producto();

        Assert.Equal("Disponible", producto.Estado);
        Assert.Null(producto.ImagenUrl);
        Assert.Null(producto.Categoria);
        Assert.Equal(string.Empty, producto.Nombre);
    }

    [Fact]
    public void ProductoConTildesYCaracteresEspecialesEsValido()
    {
        var producto = CreateProductoValido();
        producto.Nombre = "Teclado mecánico Ñandú 60%";
        producto.Descripcion = "Teclado inalámbrico con retroiluminación y switches rápidos.";

        var errores = Validate(producto);

        Assert.Empty(errores);
    }

    private static Producto CreateProductoValido()
    {
        return new Producto
        {
            Nombre = "Sony WH-1000XM6",
            Descripcion = "Auriculares inalámbricos con cancelación de ruido.",
            Precio = 340m,
            Stock = 8,
            Estado = "Disponible",
            CategoriaId = 4
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
