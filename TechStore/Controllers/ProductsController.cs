using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TechStore.Models;
using TechStore.Services.Interfaces;

namespace TechStore.Controllers
{
    // Solo usuarios autenticados con alguno de los tres roles pueden consultar productos.
    // Administrador: acceso completo. Vendedor: consultar, agregar y editar.
    // Cliente: únicamente consultar.
    [Authorize(Roles = "Administrador,Vendedor,Cliente")]
    public class ProductsController : Controller
    {
        private readonly IProductoService _productoService;
        private readonly ICategoriaService _categoriaService;
        private readonly IImagenService _imagenService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            IProductoService productoService,
            ICategoriaService categoriaService,
            IImagenService imagenService,
            ILogger<ProductsController> logger)
        {
            _productoService = productoService;
            _categoriaService = categoriaService;
            _imagenService = imagenService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Consultando el listado de productos por el usuario {Usuario}.",
                User.Identity?.Name ?? "anónimo");

            var products = await _productoService.ObtenerTodosAsync();

            _logger.LogInformation("Se consultaron {Cantidad} productos.", products.Count());

            return View(products);
        }

        // cards
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Se solicitó el detalle de un producto sin proporcionar un ID.");
                return NotFound();
            }

            var producto = await _productoService.ObtenerPorIdAsync(id.Value);

            if (producto == null)
            {
                _logger.LogWarning("No se encontró el producto con ID {ProductoId}.", id.Value);
                return NotFound();
            }

            _logger.LogInformation(
                "El usuario {Usuario} consultó el producto {ProductoId}.",
                User.Identity?.Name ?? "anónimo",
                producto.ID);

            var model = new CardViewModel
            {
                Id = producto.ID,
                Title = producto.Nombre,
                Description = producto.Descripcion,
                ImageUrl = producto.ImagenUrl ?? string.Empty,
                Price = (float)producto.Precio,
                Category = producto.Categoria?.Nombre ?? string.Empty,
                State = producto.Estado,
                Stock = producto.Stock
            };

            return View(model);
        }

        [Authorize(Roles = "Administrador,Vendedor")]
        public async Task<IActionResult> Agregar()
        {
            ViewBag.Categorias = (await _categoriaService.ObtenerTodasAsync()).OrderBy(c => c.Nombre).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Vendedor")]
        public async Task<IActionResult> Agregar(Producto producto, IFormFile? imageFile)
        {
            if (!ModelState.IsValid)
            {
                return await VistaConCategoriasAsync(producto);
            }

            if (!await ValidarCategoriaAsync(producto))
            {
                return await VistaConCategoriasAsync(producto);
            }

            // La imagen solo la define el archivo subido, nunca un valor enviado por el formulario.
            producto.ImagenUrl = null;

            if (imageFile != null && imageFile.Length > 0)
            {
                _logger.LogInformation(
                    "El usuario {Usuario} está subiendo una imagen para un producto nuevo.",
                    User.Identity?.Name ?? "anónimo");

                var imagen = await _imagenService.GuardarAsync(imageFile);
                if (!imagen.Exito)
                {
                    ModelState.AddModelError(string.Empty, imagen.Error!);
                    return await VistaConCategoriasAsync(producto);
                }

                producto.ImagenUrl = imagen.NombreArchivo;
            }

            bool guardado;
            try
            {
                guardado = await _productoService.AgregarAsync(producto);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al agregar el producto {Nombre}; se descarta la imagen {Imagen}.",
                    producto.Nombre,
                    producto.ImagenUrl ?? "(sin imagen)");
                _imagenService.Eliminar(producto.ImagenUrl);
                throw;
            }

            if (!guardado)
            {
                // El producto no se guardó: se elimina la imagen recién escrita para no dejarla huérfana.
                _logger.LogWarning(
                    "No se pudo agregar el producto {Nombre}; se descarta la imagen {Imagen}.",
                    producto.Nombre,
                    producto.ImagenUrl ?? "(sin imagen)");
                _imagenService.Eliminar(producto.ImagenUrl);
                producto.ImagenUrl = null;
                ModelState.AddModelError(string.Empty, "No se pudo guardar el producto. Intenta de nuevo.");
                return await VistaConCategoriasAsync(producto);
            }

            _logger.LogInformation(
                "El usuario {Usuario} agregó el producto {ProductoId}.",
                User.Identity?.Name ?? "anónimo",
                producto.ID);

            TempData["SuccessMessage"] = "Producto agregado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Administrador,Vendedor")]
        public async Task<IActionResult> Editar(int id)
        {
            var producto = await _productoService.ObtenerPorIdAsync(id);
            if (producto == null) return NotFound();
            ViewBag.Categorias = (await _categoriaService.ObtenerTodasAsync()).OrderBy(c => c.Nombre).ToList();
            return View(producto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Vendedor")]
        public async Task<IActionResult> Editar(int id, Producto producto, IFormFile? imageFile)
        {
            if (id != producto.ID) return NotFound();

            if (!ModelState.IsValid)
            {
                return await VistaConCategoriasAsync(producto);
            }

            if (!await ValidarCategoriaAsync(producto))
            {
                return await VistaConCategoriasAsync(producto);
            }

            var existingProduct = await _productoService.ObtenerPorIdAsync(id);

            if (existingProduct == null)
            {
                _logger.LogWarning("No se encontró el producto {ProductoId} para editarlo.", id);
                return NotFound();
            }

            var oldImage = existingProduct.ImagenUrl;
            string? newImage = null;

            if (imageFile != null && imageFile.Length > 0)
            {
                var imagen = await _imagenService.GuardarAsync(imageFile);
                if (!imagen.Exito)
                {
                    producto.ImagenUrl = oldImage;
                    ModelState.AddModelError(string.Empty, imagen.Error!);
                    return await VistaConCategoriasAsync(producto);
                }

                newImage = imagen.NombreArchivo;
            }

            // Si no se subió imagen nueva se conserva la anterior.
            producto.ImagenUrl = newImage ?? oldImage;

            bool success;
            try
            {
                success = await _productoService.EditarAsync(producto);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al editar el producto {ProductoId}; se descarta la imagen {Imagen}.",
                    id,
                    newImage ?? "(sin imagen nueva)");
                _imagenService.Eliminar(newImage);
                throw;
            }

            if (!success)
            {
                // La BD no se actualizó: se descarta la imagen nueva y se conserva la anterior.
                _logger.LogWarning(
                    "No se pudo actualizar el producto {ProductoId}; se descarta la imagen nueva {Imagen}.",
                    id,
                    newImage ?? "(sin imagen nueva)");
                _imagenService.Eliminar(newImage);
                producto.ImagenUrl = oldImage;
                ModelState.AddModelError(string.Empty, "No se pudo guardar el producto. Intenta de nuevo.");
                return await VistaConCategoriasAsync(producto);
            }

            // La imagen anterior solo se borra cuando la BD ya apunta a la nueva.
            if (newImage != null) _imagenService.Eliminar(oldImage);

            _logger.LogInformation(
                "El usuario {Usuario} editó el producto {ProductoId}.",
                User.Identity?.Name ?? "anónimo",
                id);
            TempData["SuccessMessage"] = "Producto editado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Eliminar productos es exclusivo del Administrador.
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var producto = await _productoService.ObtenerPorIdAsync(id);
            if (producto == null)
            {
                _logger.LogWarning("No se encontró el producto {ProductoId} para eliminarlo.", id);
                return NotFound();
            }

            var success = await _productoService.EliminarAsync(id);
            if (!success)
            {
                _logger.LogWarning("No se pudo eliminar el producto {ProductoId}; se conserva su imagen.", id);
                TempData["ErrorMessage"] = "No se pudo eliminar el producto. Intenta de nuevo.";
                return RedirectToAction(nameof(Index));
            }

            // La imagen solo se borra cuando la BD ya no tiene el producto; si falla, no se revierte la operación.
            _imagenService.Eliminar(producto.ImagenUrl);

            _logger.LogInformation(
                "El usuario {Usuario} eliminó el producto {ProductoId}.",
                User.Identity?.Name ?? "anónimo",
                id);
            TempData["SuccessMessage"] = "Producto eliminado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        // Evita que un CategoriaId manipulado llegue a la FK de la BD y termine en una excepción.
        private async Task<bool> ValidarCategoriaAsync(Producto producto)
        {
            if (await _categoriaService.ObtenerPorIdAsync(producto.CategoriaId) != null) return true;

            _logger.LogWarning(
                "El usuario {Usuario} intentó guardar un producto con la categoría inexistente {CategoriaId}.",
                User.Identity?.Name ?? "anónimo",
                producto.CategoriaId);
            ModelState.AddModelError(nameof(Producto.CategoriaId), "La categoría seleccionada no existe.");
            return false;
        }

        private async Task<IActionResult> VistaConCategoriasAsync(Producto producto)
        {
            ViewBag.Categorias = (await _categoriaService.ObtenerTodasAsync()).OrderBy(c => c.Nombre).ToList();
            return View(producto);
        }
    }
}